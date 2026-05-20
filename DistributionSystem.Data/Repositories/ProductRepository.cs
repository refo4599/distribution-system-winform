using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using DistributionSystem.Data.Data;
using DistributionSystem.Data.Entities;
using DistributionSystem.Data.Interfaces;

namespace DistributionSystem.Data.Repositories
{
    public class ProductRepository : BaseRepository, IProductRepository
    {
        public ProductRepository(SqlConnectionFactory connectionFactory) : base(connectionFactory) { }

        public IEnumerable<Product> GetAll()
        {
            var products = new List<Product>();
            using (var conn = Connection)
            using (var cmd = new SqlCommand(
                "SELECT Id, Name, TireSize, PurchasePrice, SalePrice, CreatedAt FROM dbo.Products ORDER BY Id", conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        products.Add(Map(reader));
            }
            return products;
        }

        public Product GetById(int id)
        {
            using (var conn = Connection)
            using (var cmd = new SqlCommand(
                "SELECT Id, Name, TireSize, PurchasePrice, SalePrice, CreatedAt FROM dbo.Products WHERE Id = @Id", conn))
            {
                cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                    if (reader.Read()) return Map(reader);
            }
            return null;
        }

        public int Insert(Product product)
        {
            using (var conn = Connection)
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try { var id = Insert(product, conn, tran); tran.Commit(); return id; }
                    catch { try { tran.Rollback(); } catch { } throw; }
                }
            }
        }

        public int Insert(Product product, SqlConnection connection, SqlTransaction transaction)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO dbo.Products (Name, TireSize, PurchasePrice, SalePrice)
                    VALUES (@Name, @TireSize, @PurchasePrice, @SalePrice);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 150) { Value = product.Name });
                cmd.Parameters.Add(new SqlParameter("@TireSize", SqlDbType.NVarChar, 50) { Value = product.TireSize ?? string.Empty });
                cmd.Parameters.Add(new SqlParameter("@PurchasePrice", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = product.PurchasePrice });
                cmd.Parameters.Add(new SqlParameter("@SalePrice", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = product.SalePrice });
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public bool Update(Product product)
        {
            using (var conn = Connection)
            using (var cmd = new SqlCommand(@"
                UPDATE dbo.Products
                SET Name          = @Name,
                    TireSize      = @TireSize,
                    PurchasePrice = @PurchasePrice,
                    SalePrice     = @SalePrice
                WHERE Id = @Id", conn))
            {
                cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 150) { Value = product.Name });
                cmd.Parameters.Add(new SqlParameter("@TireSize", SqlDbType.NVarChar, 50) { Value = product.TireSize ?? string.Empty });
                cmd.Parameters.Add(new SqlParameter("@PurchasePrice", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = product.PurchasePrice });
                cmd.Parameters.Add(new SqlParameter("@SalePrice", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = product.SalePrice });
                cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = product.Id });
                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool Delete(int id)
        {
            using (var conn = Connection)
            using (var cmd = new SqlCommand("DELETE FROM dbo.Products WHERE Id = @Id", conn))
            {
                cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        private static Product Map(SqlDataReader r) => new Product
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            Name = r.GetString(r.GetOrdinal("Name")),
            TireSize = r.IsDBNull(r.GetOrdinal("TireSize")) ? string.Empty : r.GetString(r.GetOrdinal("TireSize")),
            PurchasePrice = r.GetDecimal(r.GetOrdinal("PurchasePrice")),
            SalePrice = r.GetDecimal(r.GetOrdinal("SalePrice")),
            CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt"))
        };
    }
}