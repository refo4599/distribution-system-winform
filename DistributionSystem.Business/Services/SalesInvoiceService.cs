using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Data.Data;

namespace DistributionSystem.Business.Services
{
    public class SalesInvoiceService : BaseService
    {
        private readonly SqlConnectionFactory _factory;

        public SalesInvoiceService()
        {
            _factory = new SqlConnectionFactory();
            EnsureBoxesPerCartonColumn();
        }

        public void LogException(Exception ex) => LogError(ex);

        private void EnsureBoxesPerCartonColumn()
        {
            try
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    bool exists = false;
                    using (var chk = conn.CreateCommand())
                    {
                        chk.CommandText = @"
                            SELECT COUNT(1)
                            FROM   INFORMATION_SCHEMA.COLUMNS
                            WHERE  TABLE_NAME  = 'SalesInvoiceItems'
                              AND  COLUMN_NAME = 'BoxesPerCarton'";
                        exists = Convert.ToInt32(chk.ExecuteScalar()) > 0;
                    }
                    if (!exists)
                        using (var alter = conn.CreateCommand())
                        {
                            alter.CommandText =
                                "ALTER TABLE SalesInvoiceItems ADD BoxesPerCarton INT NOT NULL DEFAULT 1";
                            alter.ExecuteNonQuery();
                        }
                }
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════
        //  رصيد المنتج في المخزن الرئيسي
        // ══════════════════════════════════════════════════════
        public int GetWarehouseProductBalance(int productId)
        {
            return Execute(() =>
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT ISNULL(SUM(CASE
                                WHEN TransactionType IN ('Inbound','Return','CarReturn') THEN  Quantity
                                WHEN TransactionType IN ('CarLoad','Outbound')           THEN -Quantity
                                ELSE 0
                            END), 0)
                            FROM WarehouseTransactions
                            WHERE ProductId = @ProductId";
                        AddParam(cmd, "@ProductId", productId);
                        var res = cmd.ExecuteScalar();
                        return (res == null || res == DBNull.Value) ? 0 : Convert.ToInt32(res);
                    }
                }
            });
        }

        // رصيد داخل transaction
        private int GetWarehouseBalance(IDbConnection conn, IDbTransaction tran, int productId)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandText = @"
                    SELECT ISNULL(SUM(CASE
                        WHEN TransactionType IN ('Inbound','Return','CarReturn') THEN  Quantity
                        WHEN TransactionType IN ('CarLoad','Outbound')           THEN -Quantity
                        ELSE 0
                    END), 0)
                    FROM WarehouseTransactions
                    WHERE ProductId = @ProductId";
                AddParam(cmd, "@ProductId", productId);
                var res = cmd.ExecuteScalar();
                return (res == null || res == DBNull.Value) ? 0 : Convert.ToInt32(res);
            }
        }

        // ══════════════════════════════════════════════════════
        //  GET ALL INVOICES
        // ══════════════════════════════════════════════════════
        public List<SalesInvoiceDto> GetAllInvoices()
        {
            return Execute(() =>
            {
                var list = new List<SalesInvoiceDto>();
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT
                                si.Id, si.VehicleId,
                                ISNULL(v.Name,'')    AS VehicleName,
                                ISNULL(v.RepName,'') AS RepName,
                                si.CustomerId,
                                ISNULL(c.Name,'')    AS CustomerName,
                                si.TotalAmount, si.PaidAmount,
                                si.PaymentType, si.Status, si.CreatedAt,
                                sii.ProductId,
                                ISNULL(p.Name,'')    AS ProductName,
                                ISNULL(sii.Quantity,0)   AS Quantity,
                                ISNULL(sii.SalePrice,0)  AS SalePrice,
                                1                        AS BoxesPerCarton
                            FROM SalesInvoices si
                            LEFT JOIN Vehicles  v  ON v.Id  = si.VehicleId
                            LEFT JOIN Customers c  ON c.Id  = si.CustomerId
                            LEFT JOIN SalesInvoiceItems sii ON sii.InvoiceId = si.Id
                            LEFT JOIN Products p ON p.Id = sii.ProductId
                            ORDER BY si.CreatedAt DESC";

                        using (var rdr = cmd.ExecuteReader())
                        {
                            var map = new Dictionary<int, SalesInvoiceDto>();
                            while (rdr.Read())
                            {
                                int id = rdr.GetInt32(0);
                                if (!map.ContainsKey(id))
                                    map[id] = new SalesInvoiceDto
                                    {
                                        Id = id,
                                        VehicleId = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1),
                                        VehicleName = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                                        RepName = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                                        CustomerId = rdr.IsDBNull(4) ? 0 : rdr.GetInt32(4),
                                        CustomerName = rdr.IsDBNull(5) ? "" : rdr.GetString(5),
                                        TotalAmount = rdr.IsDBNull(6) ? 0m : rdr.GetDecimal(6),
                                        PaidAmount = rdr.IsDBNull(7) ? 0m : rdr.GetDecimal(7),
                                        PaymentType = rdr.IsDBNull(8) ? "" : rdr.GetString(8),
                                        Status = rdr.IsDBNull(9) ? "" : rdr.GetString(9),
                                        CreatedAt = rdr.IsDBNull(10) ? DateTime.Now : rdr.GetDateTime(10)
                                    };
                                if (!rdr.IsDBNull(11))
                                    map[id].Items.Add(new SalesInvoiceItemDto
                                    {
                                        ProductId = rdr.GetInt32(11),
                                        ProductName = rdr.IsDBNull(12) ? "" : rdr.GetString(12),
                                        Quantity = rdr.IsDBNull(13) ? 0 : rdr.GetInt32(13),
                                        SalePrice = rdr.IsDBNull(14) ? 0m : rdr.GetDecimal(14),
                                        BoxesPerCarton = 1
                                    });
                            }
                            list = map.Values.ToList();
                        }
                    }
                }
                return list;
            });
        }

        // ══════════════════════════════════════════════════════
        //  SAVE INVOICE — يخصم من المخزن الرئيسي مباشر
        // ══════════════════════════════════════════════════════
        public int SaveInvoice(SalesInvoiceDto dto)
        {
            return Execute(() =>
            {
                if (dto.Items == null || dto.Items.Count == 0)
                    throw new ArgumentException("يجب إضافة منتج واحد على الأقل.");
                if (dto.PaidAmount < 0)
                    throw new ArgumentException("المبلغ المدفوع لا يمكن أن يكون سالباً.");

                decimal totalAmount = dto.Items.Sum(i => i.TotalPrice);
                decimal paidAmount = dto.PaymentType == "Cash" ? totalAmount : dto.PaidAmount;
                string status = paidAmount >= totalAmount ? "Completed" : "Pending";

                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();

                    bool hasBpc = false;
                    using (var chk = conn.CreateCommand())
                    {
                        chk.CommandText = @"SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME='SalesInvoiceItems' AND COLUMN_NAME='BoxesPerCarton'";
                        hasBpc = Convert.ToInt32(chk.ExecuteScalar()) > 0;
                    }

                    // تحقق من وجود عمود PurchasePrice في WarehouseTransactions
                    bool hasPurchasePrice = false;
                    using (var chk2 = conn.CreateCommand())
                    {
                        chk2.CommandText = @"SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME='WarehouseTransactions' AND COLUMN_NAME='PurchasePrice'";
                        hasPurchasePrice = Convert.ToInt32(chk2.ExecuteScalar()) > 0;
                    }

                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            // التحقق من رصيد المخزن الرئيسي لكل منتج
                            foreach (var item in dto.Items)
                            {
                                int balance = GetWarehouseBalance(conn, tran, item.ProductId);
                                if (item.Quantity > balance)
                                {
                                    string prod = item.ProductName ?? $"منتج #{item.ProductId}";
                                    throw new InvalidOperationException(
                                        $"الكمية المطلوبة ({item.Quantity} قطعة) تتجاوز رصيد المخزن " +
                                        $"من [{prod}] — المتاح: {balance} قطعة فقط.");
                                }
                            }

                            // إنشاء الفاتورة
                            int invoiceId;
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = @"
                                    INSERT INTO SalesInvoices
                                        (VehicleId, CustomerId, TotalAmount, PaidAmount, PaymentType, Status)
                                    VALUES
                                        (@VehicleId, @CustomerId, @TotalAmount, @PaidAmount, @PaymentType, @Status);
                                    SELECT SCOPE_IDENTITY();";
                                AddParam(cmd, "@VehicleId", (object)DBNull.Value);
                                AddParam(cmd, "@CustomerId", dto.CustomerId);
                                AddParam(cmd, "@TotalAmount", totalAmount);
                                AddParam(cmd, "@PaidAmount", paidAmount);
                                AddParam(cmd, "@PaymentType", dto.PaymentType);
                                AddParam(cmd, "@Status", status);
                                invoiceId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // حفظ بنود الفاتورة + خصم من المخزن
                            foreach (var item in dto.Items)
                            {
                                // بند الفاتورة
                                using (var iCmd = conn.CreateCommand())
                                {
                                    iCmd.Transaction = tran;
                                    if (hasBpc)
                                    {
                                        iCmd.CommandText = @"
                                            INSERT INTO SalesInvoiceItems
                                                (InvoiceId, ProductId, Quantity, SalePrice, BoxesPerCarton)
                                            VALUES (@InvoiceId, @ProductId, @Qty, @SalePrice, 1)";
                                    }
                                    else
                                    {
                                        iCmd.CommandText = @"
                                            INSERT INTO SalesInvoiceItems
                                                (InvoiceId, ProductId, Quantity, SalePrice)
                                            VALUES (@InvoiceId, @ProductId, @Qty, @SalePrice)";
                                    }
                                    AddParam(iCmd, "@InvoiceId", invoiceId);
                                    AddParam(iCmd, "@ProductId", item.ProductId);
                                    AddParam(iCmd, "@Qty", item.Quantity);
                                    AddParam(iCmd, "@SalePrice", item.SalePrice);
                                    iCmd.ExecuteNonQuery();
                                }

                                // خصم من المخزن الرئيسي مباشر
                                using (var wtCmd = conn.CreateCommand())
                                {
                                    wtCmd.Transaction = tran;

                                    // نضيف PurchasePrice=0 لو العمود موجود في الجدول
                                    if (hasPurchasePrice)
                                    {
                                        wtCmd.CommandText = @"
                                            INSERT INTO WarehouseTransactions
                                                (ProductId, Quantity, TransactionType, ReferenceId, CreatedAt, PurchasePrice)
                                            VALUES
                                                (@ProductId, @Qty, 'Outbound', @RefId, GETDATE(), 0)";
                                    }
                                    else
                                    {
                                        wtCmd.CommandText = @"
                                            INSERT INTO WarehouseTransactions
                                                (ProductId, Quantity, TransactionType, ReferenceId, CreatedAt)
                                            VALUES
                                                (@ProductId, @Qty, 'Outbound', @RefId, GETDATE())";
                                    }

                                    AddParam(wtCmd, "@ProductId", item.ProductId);
                                    AddParam(wtCmd, "@Qty", item.Quantity);
                                    AddParam(wtCmd, "@RefId", invoiceId);
                                    wtCmd.ExecuteNonQuery();
                                }
                            }

                            // تسجيل الدفعة
                            if (paidAmount > 0)
                            {
                                using (var payCmd = conn.CreateCommand())
                                {
                                    payCmd.Transaction = tran;
                                    payCmd.CommandText = @"
                                        INSERT INTO InvoicePayments (InvoiceId, Amount, Notes)
                                        VALUES (@InvoiceId, @Amount, @Notes)";
                                    AddParam(payCmd, "@InvoiceId", invoiceId);
                                    AddParam(payCmd, "@Amount", paidAmount);
                                    AddParam(payCmd, "@Notes",
                                        dto.PaymentType == "Cash" ? "دفع كاش" : "دفعة أولى");
                                    payCmd.ExecuteNonQuery();
                                }
                            }

                            tran.Commit();
                            return invoiceId;
                        }
                        catch
                        {
                            try { tran.Rollback(); } catch { }
                            throw;
                        }
                    }
                }
            });
        }

        // ══════════════════════════════════════════════════════
        //  GET INVOICE PAYMENTS — سجل الدفعات بالتواريخ
        // ══════════════════════════════════════════════════════
        public List<InvoicePaymentDto> GetInvoicePayments(int invoiceId)
        {
            return Execute(() =>
            {
                var list = new List<InvoicePaymentDto>();
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT Id, InvoiceId, Amount, ISNULL(Notes,'') AS Notes, CreatedAt
                            FROM InvoicePayments
                            WHERE InvoiceId = @InvoiceId
                            ORDER BY CreatedAt ASC";
                        AddParam(cmd, "@InvoiceId", invoiceId);
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                DateTime dt = rdr.IsDBNull(4) ? DateTime.Now : rdr.GetDateTime(4);
                                if (dt.Kind == DateTimeKind.Utc) dt = dt.ToLocalTime();
                                else dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
                                list.Add(new InvoicePaymentDto
                                {
                                    Id = rdr.GetInt32(0),
                                    InvoiceId = rdr.GetInt32(1),
                                    Amount = rdr.IsDBNull(2) ? 0m : rdr.GetDecimal(2),
                                    Notes = rdr.GetString(3),
                                    CreatedAt = dt
                                });
                            }
                        }
                    }
                }
                return list;
            });
        }

        // ══════════════════════════════════════════════════════
        //  ADD PAYMENT
        // ══════════════════════════════════════════════════════
        public void AddPayment(int invoiceId, decimal amount, string notes = "")
        {
            Execute<object>(() =>
            {
                if (amount <= 0) throw new ArgumentException("المبلغ يجب أن يكون أكبر من صفر.");
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            decimal totalAmount = 0, paidAmount = 0;
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = "SELECT TotalAmount, PaidAmount FROM SalesInvoices WHERE Id=@Id";
                                AddParam(cmd, "@Id", invoiceId);
                                using (var rdr = cmd.ExecuteReader())
                                    if (rdr.Read()) { totalAmount = rdr.GetDecimal(0); paidAmount = rdr.GetDecimal(1); }
                            }
                            decimal remaining = totalAmount - paidAmount;
                            decimal actualPaid = Math.Min(amount, remaining);
                            if (actualPaid <= 0) throw new InvalidOperationException("الفاتورة مكتملة بالفعل.");

                            decimal newPaid = paidAmount + actualPaid;
                            string newStatus = newPaid >= totalAmount ? "Completed" : "Pending";

                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = "UPDATE SalesInvoices SET PaidAmount=@Paid, Status=@Status WHERE Id=@Id";
                                AddParam(cmd, "@Paid", newPaid);
                                AddParam(cmd, "@Status", newStatus);
                                AddParam(cmd, "@Id", invoiceId);
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = @"INSERT INTO InvoicePayments (InvoiceId, Amount, Notes)
                                                    VALUES (@InvoiceId, @Amount, @Notes)";
                                AddParam(cmd, "@InvoiceId", invoiceId);
                                AddParam(cmd, "@Amount", actualPaid);
                                AddParam(cmd, "@Notes", notes);
                                cmd.ExecuteNonQuery();
                            }
                            tran.Commit();
                        }
                        catch { try { tran.Rollback(); } catch { } throw; }
                    }
                }
                return null;
            });
        }

        // ══════════════════════════════════════════════════════
        //  DELETE INVOICE — يرجع الكمية للمخزن
        // ══════════════════════════════════════════════════════
        public void DeleteInvoice(int id)
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
                            // حذف حركات الخصم من المخزن
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = @"DELETE FROM WarehouseTransactions
                                                    WHERE TransactionType='Outbound' AND ReferenceId=@Id";
                                AddParam(cmd, "@Id", id);
                                cmd.ExecuteNonQuery();
                            }
                            foreach (var tbl in new[] { "InvoicePayments", "SalesInvoiceItems" })
                            {
                                using (var cmd = conn.CreateCommand())
                                {
                                    cmd.Transaction = tran;
                                    cmd.CommandText = $"DELETE FROM {tbl} WHERE InvoiceId=@Id";
                                    AddParam(cmd, "@Id", id);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = "DELETE FROM SalesInvoices WHERE Id=@Id";
                                AddParam(cmd, "@Id", id);
                                cmd.ExecuteNonQuery();
                            }
                            tran.Commit();
                        }
                        catch { try { tran.Rollback(); } catch { } throw; }
                    }
                }
                return null;
            });
        }

        // ══════════════════════════════════════════════════════
        //  HELPER
        // ══════════════════════════════════════════════════════
        private static void AddParam(IDbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}