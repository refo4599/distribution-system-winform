using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Data.Data;
using DistributionSystem.Data.Repositories;
using DistributionSystem.Data.Entities;

namespace DistributionSystem.Business.Services
{
    public class WrongDatabaseException : Exception
    {
        public WrongDatabaseException(string message) : base(message) { }
    }

    public class MissingTablesException : Exception
    {
        public MissingTablesException(string message) : base(message) { }
    }

    public class InboundService : BaseService
    {
        private readonly SqlConnectionFactory _factory;
        private readonly InboundRepository _inboundRepo;
        private readonly WarehouseTransactionRepository _warehouseRepo;

        public InboundService()
        {
            _factory = new SqlConnectionFactory();
            _inboundRepo = new InboundRepository(_factory);
            _warehouseRepo = new WarehouseTransactionRepository(_factory);
        }

        public void LogException(Exception ex) => LogError(ex);

        // ??????????????????????????????????????????????????????
        //  GET ALL ó «·ﬂ„Ì… »«·ﬁÿ⁄ ›ﬁÿ° BoxesPerCarton = 1 À«» 
        // ??????????????????????????????????????????????????????
        public List<InboundOrderDto> GetAllInboundOrders()
        {
            return Execute(() =>
            {
                var result = new List<InboundOrderDto>();
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT
                                io.Id,
                                io.CustomerId,
                                ISNULL(c.Name, N'€Ì— „⁄—Ê›')                        AS CustomerName,
                                ioi.ProductId,
                                ISNULL(p.Name, N'€Ì— „⁄—Ê›')                        AS ProductName,
                                ISNULL(ioi.Quantity, 0)                             AS Quantity,
                                ISNULL(ioi.PurchasePrice, 0)                        AS PurchasePrice,
                                ISNULL(ioi.Quantity * ioi.PurchasePrice, 0)         AS TotalValue,
                                io.CreatedAt,
                                ISNULL(
                                    (SELECT SUM(wt2.Quantity)
                                     FROM WarehouseTransactions wt2
                                     WHERE wt2.ProductId = p.Id),
                                0)                                                  AS StockQuantity
                            FROM InboundOrders io
                            LEFT JOIN InboundOrderItems ioi ON ioi.InboundOrderId = io.Id
                            LEFT JOIN Customers         c   ON c.Id  = io.CustomerId
                            LEFT JOIN Products          p   ON p.Id  = ioi.ProductId
                            ORDER BY io.CreatedAt DESC";

                        cmd.CommandType = CommandType.Text;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                result.Add(new InboundOrderDto
                                {
                                    Id = rdr.IsDBNull(rdr.GetOrdinal("Id")) ? 0 : rdr.GetInt32(rdr.GetOrdinal("Id")),
                                    CustomerId = rdr.IsDBNull(rdr.GetOrdinal("CustomerId")) ? 0 : rdr.GetInt32(rdr.GetOrdinal("CustomerId")),
                                    CustomerName = rdr["CustomerName"]?.ToString() ?? string.Empty,
                                    ProductId = rdr.IsDBNull(rdr.GetOrdinal("ProductId")) ? 0 : rdr.GetInt32(rdr.GetOrdinal("ProductId")),
                                    ProductName = rdr["ProductName"]?.ToString() ?? string.Empty,
                                    BoxesPerCarton = 1,   // À«»  ó «·ﬂ„Ì… »«·ﬁÿ⁄ ›ﬁÿ
                                    Quantity = rdr.IsDBNull(rdr.GetOrdinal("Quantity")) ? 0 : rdr.GetInt32(rdr.GetOrdinal("Quantity")),
                                    PurchasePrice = rdr.IsDBNull(rdr.GetOrdinal("PurchasePrice")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("PurchasePrice")),
                                    TotalValue = rdr.IsDBNull(rdr.GetOrdinal("TotalValue")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("TotalValue")),
                                    StockQuantity = rdr.IsDBNull(rdr.GetOrdinal("StockQuantity")) ? 0 : rdr.GetInt32(rdr.GetOrdinal("StockQuantity")),
                                    CreatedAt = rdr.IsDBNull(rdr.GetOrdinal("CreatedAt")) ? DateTime.Now : rdr.GetDateTime(rdr.GetOrdinal("CreatedAt")),
                                });
                            }
                        }
                    }
                }
                return result;
            });
        }

        // ??????????????????????????????????????????????????????
        //  DELETE
        // ??????????????????????????????????????????????????????
        public void DeleteInboundOrder(int id)
        {
            Execute<object>(() =>
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd0 = conn.CreateCommand())
                            {
                                cmd0.Transaction = tran;
                                cmd0.CommandText = "DELETE FROM WarehouseTransactions WHERE ReferenceId = @Id AND TransactionType = 'Inbound'";
                                cmd0.CommandType = CommandType.Text;
                                var p0 = cmd0.CreateParameter(); p0.ParameterName = "@Id"; p0.Value = id;
                                cmd0.Parameters.Add(p0); cmd0.ExecuteNonQuery();
                            }
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = "DELETE FROM InboundOrderItems WHERE InboundOrderId = @Id";
                                cmd.CommandType = CommandType.Text;
                                var p = cmd.CreateParameter(); p.ParameterName = "@Id"; p.Value = id;
                                cmd.Parameters.Add(p); cmd.ExecuteNonQuery();
                            }
                            using (var cmd2 = conn.CreateCommand())
                            {
                                cmd2.Transaction = tran;
                                cmd2.CommandText = "DELETE FROM InboundOrders WHERE Id = @Id";
                                cmd2.CommandType = CommandType.Text;
                                var p = cmd2.CreateParameter(); p.ParameterName = "@Id"; p.Value = id;
                                cmd2.Parameters.Add(p); cmd2.ExecuteNonQuery();
                            }
                            tran.Commit();
                        }
                        catch (Exception ex)
                        {
                            try { tran.Rollback(); } catch { }
                            LogError(ex);
                            throw new InvalidOperationException("›‘· Õ–› «·√„— «·Ê«—œ.", ex);
                        }
                    }
                }
                return null;
            });
        }

        // ??????????????????????????????????????????????????????
        //  UPDATE
        // ??????????????????????????????????????????????????????
        public void UpdateInboundOrder(InboundOrderDto dto)
        {
            Execute<object>(() =>
            {
                if (dto == null) throw new ArgumentNullException(nameof(dto));
                if (dto.Id <= 0) throw new ArgumentException("„⁄—¯› «·√„— €Ì— ’«·Õ.");
                if (dto.Items == null || dto.Items.Count == 0) throw new ArgumentException("ÌÃ» √‰ ÌÕ ÊÌ «·√„— ⁄·Ï ⁄‰’— Ê«Õœ ⁄·Ï «·√ﬁ·.");

                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = "UPDATE InboundOrders SET CustomerId = @CustomerId WHERE Id = @Id";
                                cmd.CommandType = CommandType.Text;
                                var pCust = cmd.CreateParameter(); pCust.ParameterName = "@CustomerId"; pCust.Value = dto.CustomerId; cmd.Parameters.Add(pCust);
                                var pId = cmd.CreateParameter(); pId.ParameterName = "@Id"; pId.Value = dto.Id; cmd.Parameters.Add(pId);
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = "DELETE FROM WarehouseTransactions WHERE ReferenceId = @Id AND TransactionType = 'Inbound'";
                                cmd.CommandType = CommandType.Text;
                                var p = cmd.CreateParameter(); p.ParameterName = "@Id"; p.Value = dto.Id; cmd.Parameters.Add(p);
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = "DELETE FROM InboundOrderItems WHERE InboundOrderId = @Id";
                                cmd.CommandType = CommandType.Text;
                                var p = cmd.CreateParameter(); p.ParameterName = "@Id"; p.Value = dto.Id; cmd.Parameters.Add(p);
                                cmd.ExecuteNonQuery();
                            }
                            foreach (var item in dto.Items)
                            {
                                _inboundRepo.InsertInboundItem(conn, tran, dto.Id,
                                    item.ProductId, item.Quantity, item.PurchasePrice,
                                    boxesPerCarton: 1);   // À«» 

                                var wt = new WarehouseTransaction
                                {
                                    ProductId = item.ProductId,
                                    Quantity = item.Quantity,
                                    TransactionType = "Inbound",
                                    ReferenceId = dto.Id,
                                    PurchasePrice = item.PurchasePrice,
                                    CreatedAt = DateTime.Now
                                };
                                try { _warehouseRepo.AddTransaction(wt, conn, tran); }
                                catch { _warehouseRepo.AddTransaction(wt); }
                            }
                            tran.Commit();
                        }
                        catch (Exception ex)
                        {
                            try { tran.Rollback(); } catch { }
                            LogError(ex);
                            throw new InvalidOperationException("›‘·  ÕœÌÀ «·√„— «·Ê«—œ.", ex);
                        }
                    }
                }
                return null;
            });
        }

        // ??????????????????????????????????????????????????????
        //  SAVE (INSERT)
        // ??????????????????????????????????????????????????????
        public int SaveInboundOrder(InboundOrderDto dto)
        {
            return Execute<int>(() =>
            {
                if (dto == null) throw new ArgumentNullException(nameof(dto));
                if (dto.Items == null || dto.Items.Count == 0) throw new ArgumentException("Inbound order must contain at least one item.");

                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();

                    var requiredTables = new[] { "InboundOrders", "InboundOrderItems", "WarehouseTransactions" };
                    var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var checkCmd = conn.CreateCommand())
                    {
                        checkCmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME IN ('InboundOrders','InboundOrderItems','WarehouseTransactions')";
                        checkCmd.CommandType = CommandType.Text;
                        using (var rdr = checkCmd.ExecuteReader())
                            while (rdr.Read()) existing.Add(rdr.GetString(0));
                    }
                    var missing = requiredTables.Where(t => !existing.Contains(t)).ToArray();
                    if (missing.Length > 0)
                    {
                        var msg = "Database is missing required tables: " + string.Join(", ", missing);
                        LogError(new InvalidOperationException(msg));
                        throw new MissingTablesException(msg);
                    }

                    var productRepo = new ProductRepository(_factory);
                    foreach (var item in dto.Items)
                    {
                        if (item.ProductId <= 0) throw new ArgumentException("Invalid ProductId in items.");
                        var prod = productRepo.GetById(item.ProductId);
                        if (prod == null) throw new ArgumentException($"Product with Id {item.ProductId} does not exist.");
                        if (item.Quantity <= 0) throw new ArgumentException("Item quantity must be greater than zero.");
                        if (item.PurchasePrice <= 0) throw new ArgumentException("Item purchase price must be greater than zero.");
                    }

                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            var inboundId = _inboundRepo.InsertInboundOrder(conn, tran, dto.CustomerId, dto.CreatedAt);
                            foreach (var item in dto.Items)
                            {
                                _inboundRepo.InsertInboundItem(conn, tran, inboundId,
                                    item.ProductId, item.Quantity, item.PurchasePrice,
                                    boxesPerCarton: 1);   // À«»  ó «·ﬂ„Ì… »«·ﬁÿ⁄ ›ﬁÿ

                                var wt = new WarehouseTransaction
                                {
                                    ProductId = item.ProductId,
                                    Quantity = item.Quantity,
                                    TransactionType = "Inbound",
                                    ReferenceId = inboundId,
                                    PurchasePrice = item.PurchasePrice,
                                    CreatedAt = DateTime.Now
                                };
                                try { _warehouseRepo.AddTransaction(wt, conn, tran); }
                                catch { _warehouseRepo.AddTransaction(wt); }
                            }
                            tran.Commit();

                            try
                            {
                                var logPath = Path.Combine(Path.GetTempPath(), "DistributionSystem.log");
                                File.AppendAllText(logPath, $"{DateTime.Now:u} - Inbound saved. Id={inboundId}, Items={dto.Items.Count}\n");
                            }
                            catch { }

                            return inboundId;
                        }
                        catch (SqlException sqlEx) { try { tran.Rollback(); } catch { } LogError(sqlEx); throw new InvalidOperationException("Failed to save inbound order. Please try again.", sqlEx); }
                        catch (MissingTablesException) { try { tran.Rollback(); } catch { } throw; }
                        catch (Exception ex) { try { tran.Rollback(); } catch { } LogError(ex); throw; }
                    }
                }
            });
        }
    }
}