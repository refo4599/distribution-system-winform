using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using DistributionSystem.Data.Data;
using DistributionSystem.Data.Entities;
using DistributionSystem.Data.Interfaces;

namespace DistributionSystem.Data.Repositories
{
    public class WarehouseTransactionRepository : BaseRepository, IWarehouseTransactionRepository
    {
        public WarehouseTransactionRepository(SqlConnectionFactory connectionFactory) : base(connectionFactory)
        {
        }

        public int AddTransaction(WarehouseTransaction tx)
        {
            using (var conn = Connection)
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        var id = AddTransaction(tx, conn, tran);
                        tran.Commit();
                        return id;
                    }
                    catch (Exception)
                    {
                        try { tran.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public int AddTransaction(WarehouseTransaction tx, SqlConnection connection, SqlTransaction transaction)
        {
            if (tx == null) throw new ArgumentNullException(nameof(tx));
            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"INSERT INTO dbo.WarehouseTransactions (ProductId, Quantity, TransactionType, ReferenceId, PurchasePrice, CreatedAt) VALUES (@ProductId, @Quantity, @TransactionType, @ReferenceId, @PurchasePrice, @CreatedAt); SELECT CAST(SCOPE_IDENTITY() AS INT);";
                    cmd.CommandType = CommandType.Text;

                    cmd.Parameters.Add(new SqlParameter("@ProductId", SqlDbType.Int) { Value = tx.ProductId });
                    cmd.Parameters.Add(new SqlParameter("@Quantity", SqlDbType.Int) { Value = tx.Quantity });
                    cmd.Parameters.Add(new SqlParameter("@TransactionType", SqlDbType.NVarChar, 50) { Value = tx.TransactionType });

                    var refParam = new SqlParameter("@ReferenceId", SqlDbType.Int);
                    if (tx.ReferenceId.HasValue) refParam.Value = tx.ReferenceId.Value; else refParam.Value = DBNull.Value;
                    cmd.Parameters.Add(refParam);

                    var priceParam = new SqlParameter("@PurchasePrice", SqlDbType.Decimal) { Precision = 18, Scale = 2 };
                    if (tx.PurchasePrice.HasValue) priceParam.Value = tx.PurchasePrice.Value; else priceParam.Value = DBNull.Value;
                    cmd.Parameters.Add(priceParam);

                    cmd.Parameters.Add(new SqlParameter("@CreatedAt", SqlDbType.DateTime) { Value = tx.CreatedAt });

                    var result = cmd.ExecuteScalar();
                    return Convert.ToInt32(result);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public int GetProductBalance(int productId)
        {
            try
            {
                using (var conn = Connection)
                using (var cmd = new SqlCommand(@"
SELECT ISNULL(SUM(
    CASE WHEN TransactionType IN ('Inbound','Return','Ê«—œ','—’Ìœ «›  «ÕÌ') THEN Quantity
         WHEN TransactionType = 'CarLoad' THEN -Quantity
         ELSE 0 END
),0) AS Balance
FROM dbo.WarehouseTransactions
WHERE ProductId = @ProductId
", conn))
                {
                    cmd.Parameters.Add(new SqlParameter("@ProductId", SqlDbType.Int) { Value = productId });
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    return Convert.ToInt32(result);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public IEnumerable<Tuple<int,int>> GetAllBalances()
        {
            var list = new List<Tuple<int,int>>();
            try
            {
                using (var conn = Connection)
                using (var cmd = new SqlCommand(@"
SELECT p.Id, ISNULL(SUM(
    CASE WHEN wt.TransactionType IN ('Inbound','Return','Ê«—œ','—’Ìœ «›  «ÕÌ') THEN wt.Quantity
         WHEN wt.TransactionType = 'CarLoad' THEN -wt.Quantity
         ELSE 0 END
),0) AS Balance
FROM dbo.Products p
LEFT JOIN dbo.WarehouseTransactions wt ON wt.ProductId = p.Id
GROUP BY p.Id
ORDER BY p.Id
", conn))
                {
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var pid = reader.GetInt32(0);
                            var balance = reader.GetInt32(1);
                            list.Add(Tuple.Create(pid, balance));
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return list;
        }

        // New: Get weighted average cost for a product
        public decimal? GetProductAverageCost(int productId)
        {
            try
            {
                using (var conn = Connection)
                using (var cmd = new SqlCommand(@"
SELECT
    CASE WHEN SUM(Quantity) = 0 THEN NULL
         ELSE CAST(SUM(Quantity * ISNULL(PurchasePrice,0.0)) / SUM(Quantity) AS DECIMAL(18,6)) END AS AvgCost
FROM dbo.WarehouseTransactions
WHERE ProductId = @ProductId
", conn))
                {
                    cmd.Parameters.Add(new SqlParameter("@ProductId", SqlDbType.Int) { Value = productId });
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    if (result == DBNull.Value || result == null) return null;
                    return Convert.ToDecimal(result);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        // New: Get total inventory value = SUM(Quantity * UnitCost)
        public decimal GetTotalInventoryValue()
        {
            try
            {
                using (var conn = Connection)
                using (var cmd = new SqlCommand(@"
SELECT ISNULL(SUM(Quantity * ISNULL(PurchasePrice,0.0)),0) FROM dbo.WarehouseTransactions
", conn))
                {
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    return Convert.ToDecimal(result);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Safe method to create outbound transaction: calculates avg cost and inserts outbound tx within supplied transaction
        public int CreateOutboundTransaction(int productId, int quantity, string transactionType, int? referenceId, SqlConnection connection, SqlTransaction transaction)
        {
            if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
            if (string.IsNullOrWhiteSpace(transactionType)) throw new ArgumentException("Transaction type required.", nameof(transactionType));

            // Compute weighted average cost using the same connection/transaction to avoid race conditions
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
-- Calculate average cost using an update lock to serialize reads/writes for this product
SELECT CASE WHEN SUM(Quantity) = 0 THEN NULL ELSE CAST(SUM(Quantity * ISNULL(PurchasePrice,0.0)) / SUM(Quantity) AS DECIMAL(18,6)) END AS AvgCost
FROM dbo.WarehouseTransactions WITH (UPDLOCK, HOLDLOCK)
WHERE ProductId = @ProductId
";
                cmd.Parameters.Add(new SqlParameter("@ProductId", SqlDbType.Int) { Value = productId });
                cmd.CommandType = CommandType.Text;

                var avgObj = cmd.ExecuteScalar();
                decimal? avgCost = null;
                if (avgObj != DBNull.Value && avgObj != null) avgCost = Convert.ToDecimal(avgObj);

                // If there's no stock / average cannot be computed, prevent outbound
                if (!avgCost.HasValue)
                {
                    throw new InvalidOperationException("Cannot create outbound transaction: no stock or average cost unavailable for product.");
                }

                // For outbound we store negative quantity
                var tx = new WarehouseTransaction
                {
                    ProductId = productId,
                    Quantity = -quantity,
                    TransactionType = transactionType,
                    ReferenceId = referenceId,
                    PurchasePrice = avgCost,
                    CreatedAt = DateTime.UtcNow
                };

                return AddTransaction(tx, connection, transaction);
            }
        }

        public decimal? GetTransactionUnitCost(int transactionId)
        {
            try
            {
                using (var conn = Connection)
                using (var cmd = new SqlCommand(@"SELECT PurchasePrice FROM dbo.WarehouseTransactions WHERE Id = @Id", conn))
                {
                    cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = transactionId });
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    if (result == DBNull.Value || result == null) return null;
                    return Convert.ToDecimal(result);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public int GetDistinctProductCount()
        {
            try
            {
                using (var conn = Connection)
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(DISTINCT ProductId) FROM dbo.WarehouseTransactions";
                    conn.Open();
                    var res = cmd.ExecuteScalar();
                    return (res == null || res == DBNull.Value) ? 0 : Convert.ToInt32(res);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public int GetTotalQuantity()
        {
            try
            {
                using (var conn = Connection)
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ISNULL(SUM(Quantity), 0) FROM dbo.WarehouseTransactions";
                    conn.Open();
                    var res = cmd.ExecuteScalar();
                    return (res == null || res == DBNull.Value) ? 0 : Convert.ToInt32(res);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public decimal GetOverallAverageCost()
        {
            try
            {
                using (var conn = Connection)
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT
    CASE WHEN ISNULL(SUM(Quantity),0) = 0 THEN 0
         ELSE SUM(CAST(Quantity AS DECIMAL(18,4)) * CAST(ISNULL(PurchasePrice,0.0) AS DECIMAL(18,4))) / SUM(Quantity)
    END
FROM dbo.WarehouseTransactions
WHERE Quantity > 0";
                    conn.Open();
                    var res = cmd.ExecuteScalar();
                    return (res == null || res == DBNull.Value) ? 0m : Convert.ToDecimal(res);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
