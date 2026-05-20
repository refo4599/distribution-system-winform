using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Data.Data;
using DistributionSystem.Data.Entities;
using DistributionSystem.Data.Repositories;

namespace DistributionSystem.Business.Services
{
    public class WarehouseService : BaseService
    {
        private readonly WarehouseTransactionRepository _repository;
        private readonly ProductRepository _productRepository;

        private static readonly string[] ValidTypes = new[] { "Inbound", "CarLoad", "Return" };

        public WarehouseService()
        {
            var factory = new SqlConnectionFactory();
            _repository = new WarehouseTransactionRepository(factory);
            _productRepository = new ProductRepository(factory);
        }

        public int AddTransaction(WarehouseTransactionDto dto)
        {
            return Execute(() =>
            {
                Validate(dto);
                var entity = new WarehouseTransaction
                {
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity,
                    TransactionType = dto.TransactionType,
                    ReferenceId = dto.ReferenceId,
                    CreatedAt = DateTime.Now
                };
                return _repository.AddTransaction(entity);
            });
        }

        public int GetProductBalance(int productId)
            => Execute(() => _repository.GetProductBalance(productId));

        public decimal? GetProductAverageCost(int productId)
            => Execute(() => _repository.GetProductAverageCost(productId));

        public decimal GetTotalInventoryValue()
            => Execute(() => _repository.GetTotalInventoryValue());

        public IEnumerable<WarehouseBalanceDto> GetAllBalances()
        {
            return Execute(() =>
            {
                var balances = _repository.GetAllBalances().ToList();
                var products = _productRepository.GetAll().ToDictionary(p => p.Id, p => p);
                return balances.Select(b =>
                {
                    var prod = products.ContainsKey(b.Item1) ? products[b.Item1] : null;
                    return new WarehouseBalanceDto
                    {
                        ProductId = b.Item1,
                        ProductName = prod?.Name ?? string.Empty,
                        Balance = b.Item2,
                        BoxesPerCarton = 1   // À«»  ó «·„‰ Ã »«·ﬁÿ⁄…
                    };
                }).ToList();
            });
        }

        public IEnumerable<WarehouseBalanceDto> GetAllBalancesWithCost()
        {
            return Execute(() =>
            {
                var result = new List<WarehouseBalanceDto>();
                var factory = new SqlConnectionFactory();
                using (var conn = factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        // ≈÷«›… 'Outbound' ﬂŒ’„ „‰ «·—’Ìœ (›Ê« Ì— «·»Ì⁄ «·„»«‘—…)
                        cmd.CommandText = @"
                            SELECT
                                p.Id                AS ProductId,
                                p.Name              AS ProductName,
                                ISNULL(SUM(CASE
                                    WHEN wt.TransactionType IN ('Inbound','Return','CarReturn') THEN  wt.Quantity
                                    WHEN wt.TransactionType IN ('CarLoad','Outbound')           THEN -wt.Quantity
                                    ELSE 0 END), 0) AS Balance,
                                ISNULL(p.PurchasePrice, 0) AS AvgCost
                            FROM Products p
                            LEFT JOIN WarehouseTransactions wt ON wt.ProductId = p.Id
                            GROUP BY p.Id, p.Name, p.PurchasePrice
                            ORDER BY p.Name";
                        cmd.CommandType = CommandType.Text;
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                int productId = rdr.GetInt32(rdr.GetOrdinal("ProductId"));
                                string name = rdr["ProductName"]?.ToString() ?? "";
                                int balance = rdr.IsDBNull(rdr.GetOrdinal("Balance")) ? 0 : rdr.GetInt32(rdr.GetOrdinal("Balance"));
                                decimal unitPrice = rdr.IsDBNull(rdr.GetOrdinal("AvgCost")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("AvgCost"));
                                result.Add(new WarehouseBalanceDto
                                {
                                    ProductId = productId,
                                    ProductName = name,
                                    BoxesPerCarton = 1,   // À«» 
                                    Balance = balance,
                                    AvgCost = unitPrice,
                                    TotalCost = balance * unitPrice
                                });
                            }
                        }
                    }
                }
                return result;
            });
        }

        // ??????????????????????????????????????????????????????
        //  GET ALL TRANSACTIONS
        // ??????????????????????????????????????????????????????
        public IEnumerable<WarehouseTransactionViewDto> GetAllTransactions()
        {
            return Execute(() =>
            {
                var result = new List<WarehouseTransactionViewDto>();
                var factory = new SqlConnectionFactory();

                using (var conn = factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            -- 1. Õ—ﬂ«  «·„Œ“‰
                            SELECT
                                wt.Id,
                                ISNULL(p.Name, 'ó')     AS ProductName,
                                wt.TransactionType      AS TransactionType,
                                ABS(wt.Quantity)         AS Quantity,
                                CASE
                                    WHEN wt.TransactionType = 'Outbound'
                                    THEN ISNULL((
                                        SELECT TOP 1 sii.SalePrice
                                        FROM SalesInvoiceItems sii
                                        WHERE sii.InvoiceId = wt.ReferenceId
                                          AND sii.ProductId = wt.ProductId
                                    ), 0)
                                    ELSE ISNULL(wt.PurchasePrice, 0)
                                END                      AS UnitCost,
                                ABS(wt.Quantity) *
                                CASE
                                    WHEN wt.TransactionType = 'Outbound'
                                    THEN ISNULL((
                                        SELECT TOP 1 sii.SalePrice
                                        FROM SalesInvoiceItems sii
                                        WHERE sii.InvoiceId = wt.ReferenceId
                                          AND sii.ProductId = wt.ProductId
                                    ), 0)
                                    ELSE ISNULL(wt.PurchasePrice, 0)
                                END                      AS TotalValue,
                                wt.CreatedAt
                            FROM WarehouseTransactions wt
                            LEFT JOIN Products p ON p.Id = wt.ProductId

                            UNION ALL

                            -- 2. „œ›Ê⁄«  «·›Ê« Ì—
                            SELECT
                                ip.Id,
                                ISNULL(c.Name, 'ó')     AS ProductName,
                                'SaleRevenue'            AS TransactionType,
                                0                        AS Quantity,
                                ip.Amount                AS UnitCost,
                                ip.Amount                AS TotalValue,
                                ip.CreatedAt
                            FROM InvoicePayments ip
                            LEFT JOIN SalesInvoices si ON si.Id = ip.InvoiceId
                            LEFT JOIN Customers c      ON c.Id  = si.CustomerId

                            UNION ALL

                            -- 3. «·„’«—Ì› «·≈œ«—Ì…
                            SELECT
                                ae.Id,
                                ISNULL(ae.Description, '„’—Ê› ≈œ«—Ì') AS ProductName,
                                'AdminExpense'           AS TransactionType,
                                0                        AS Quantity,
                                ae.Amount                AS UnitCost,
                                ae.Amount                AS TotalValue,
                                ae.CreatedAt
                            FROM AdminExpenses ae

                            UNION ALL

                            -- 4. ”·› «·„ÊŸ›Ì‰
                            SELECT
                                el.Id,
                                ISNULL(e.Name, 'ó')     AS ProductName,
                                'EmployeeExpense'        AS TransactionType,
                                0                        AS Quantity,
                                el.Amount                AS UnitCost,
                                el.Amount                AS TotalValue,
                                el.CreatedAt
                            FROM EmployeeLoans el
                            LEFT JOIN Employees e ON e.Id = el.EmployeeId

                            ORDER BY CreatedAt DESC";

                        cmd.CommandType = CommandType.Text;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                DateTime createdAt = rdr.IsDBNull(rdr.GetOrdinal("CreatedAt"))
                                    ? DateTime.MinValue
                                    : rdr.GetDateTime(rdr.GetOrdinal("CreatedAt"));

                                if (createdAt != DateTime.MinValue)
                                    createdAt = createdAt.Kind == DateTimeKind.Utc
                                        ? createdAt.ToLocalTime()
                                        : DateTime.SpecifyKind(createdAt, DateTimeKind.Local);

                                result.Add(new WarehouseTransactionViewDto
                                {
                                    Id = rdr.GetInt32(rdr.GetOrdinal("Id")),
                                    ProductName = rdr["ProductName"]?.ToString() ?? "",
                                    TransactionType = rdr["TransactionType"]?.ToString() ?? "",
                                    Quantity = rdr.IsDBNull(rdr.GetOrdinal("Quantity")) ? 0 : rdr.GetInt32(rdr.GetOrdinal("Quantity")),
                                    UnitCost = rdr.IsDBNull(rdr.GetOrdinal("UnitCost")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("UnitCost")),
                                    TotalValue = rdr.IsDBNull(rdr.GetOrdinal("TotalValue")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("TotalValue")),
                                    CreatedAt = createdAt
                                });
                            }
                        }
                    }
                }
                return result;
            });
        }

        // ??????????????????????????????????????????????????????
        //  PROCESS OUTBOUND
        // ??????????????????????????????????????????????????????
        public int ProcessOutbound(int productId, int quantity, string transactionType = "CarLoad", int? referenceId = null)
        {
            return Execute(() =>
            {
                if (productId <= 0) throw new ArgumentException("Invalid productId.", nameof(productId));
                if (quantity <= 0) throw new ArgumentException("Quantity must be > 0.", nameof(quantity));
                if (string.IsNullOrWhiteSpace(transactionType) || !ValidTypes.Contains(transactionType))
                    throw new ArgumentException("Invalid transaction type.", nameof(transactionType));

                var factory = new SqlConnectionFactory();
                using (var conn = factory.CreateConnection())
                {
                    conn.Open();
                    using (var tran = conn.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            var id = _repository.CreateOutboundTransaction(productId, quantity, transactionType, referenceId, conn, tran);
                            tran.Commit();
                            return id;
                        }
                        catch (Exception ex) { try { tran.Rollback(); } catch { } LogError(ex); throw; }
                    }
                }
            });
        }

        // ??????????????????????????????????????????????????????
        //  PROCESS RETURN FROM OUTBOUND
        // ??????????????????????????????????????????????????????
        public int ProcessReturnFromOutbound(int originalTransactionId, int quantity, string transactionType = "Return", int? referenceId = null)
        {
            return Execute(() =>
            {
                if (originalTransactionId <= 0) throw new ArgumentException("Invalid original transaction id.", nameof(originalTransactionId));
                if (quantity <= 0) throw new ArgumentException("Quantity must be > 0.", nameof(quantity));

                var unitCost = _repository.GetTransactionUnitCost(originalTransactionId);
                if (!unitCost.HasValue)
                    throw new InvalidOperationException("Original transaction does not have a recorded unit cost.");

                var factory = new SqlConnectionFactory();
                using (var conn = factory.CreateConnection())
                {
                    conn.Open();
                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            var tx = new WarehouseTransaction
                            {
                                ProductId = GetProductIdForTransaction(originalTransactionId),
                                Quantity = quantity,
                                TransactionType = transactionType,
                                ReferenceId = referenceId,
                                PurchasePrice = unitCost,
                                CreatedAt = DateTime.Now
                            };
                            var id = _repository.AddTransaction(tx, conn, tran);
                            tran.Commit();
                            return id;
                        }
                        catch (Exception ex) { try { tran.Rollback(); } catch { } LogError(ex); throw; }
                    }
                }
            });
        }

        // ??????????????????????????????????????????????????????
        //  HELPERS
        // ??????????????????????????????????????????????????????
        private int GetProductIdForTransaction(int transactionId)
        {
            var factory = new SqlConnectionFactory();
            using (var conn = factory.CreateConnection())
            using (var cmd = new SqlCommand("SELECT ProductId FROM dbo.WarehouseTransactions WHERE Id = @Id", conn))
            {
                cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = transactionId });
                conn.Open();
                var res = cmd.ExecuteScalar();
                if (res == null || res == DBNull.Value)
                    throw new ArgumentException("Transaction not found.", nameof(transactionId));
                return Convert.ToInt32(res);
            }
        }

        private static void Validate(WarehouseTransactionDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.ProductId <= 0) throw new ArgumentException("Invalid product.");
            if (dto.Quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.");
            if (string.IsNullOrWhiteSpace(dto.TransactionType) || !ValidTypes.Contains(dto.TransactionType))
                throw new ArgumentException("Invalid transaction type.");
        }
    }
}