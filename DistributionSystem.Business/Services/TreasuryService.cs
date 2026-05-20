using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using DistributionSystem.Data.Data;

namespace DistributionSystem.Business.Services
{
    public class TreasurySummaryDto
    {
        public decimal InventoryValue { get; set; }
        public decimal InvoicesRevenue { get; set; }
        public decimal ManualBalance { get; set; }
        public decimal EmployeeExpenses { get; set; }
        public decimal TotalBalance { get; set; }
        public int TotalProducts { get; set; }
        public int TotalInvoices { get; set; }
    }

    public class ManualBalanceEntryDto
    {
        public decimal Amount { get; set; }
        public string Note { get; set; }
        public DateTime AddedAt { get; set; }
    }

    // ? DTO „Õ”Û¯‰ ó ﬂ· «·ÕﬁÊ· «··Ì ‰Õ «ÃÂ« ··⁄—÷ Ê«· ﬁ—Ì—
    public class TreasuryMovementDto
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; }       // «·”ÿ— «·—∆Ì”Ì
        public string SubDetail { get; set; } = ""; //  ›’Ì·… ≈÷«›Ì… («”„ „Ê—œ / —ﬁ„ ›« Ê—…...)
        public string Reference { get; set; } = ""; // —ﬁ„ „—Ã⁄Ì (ID)

        // ‰Ê⁄ «·Õ—ﬂ…: invoice | inbound | employee_loan | employee_expense | manual_in | manual_out
        public string Category { get; set; }

        public string CategoryLabel =>
            Category == "invoice" ? "œ›⁄… ›« Ê—…" :
            Category == "inbound" ? "„‘ —Ì«  Ê«—œ" :
            Category == "employee_loan" ? "”·›… „ÊŸ›" :
            Category == "employee_expense" ? "„’—Ê› ≈œ«—Ì" :
            Category == "manual_in" ? "≈Ìœ«⁄" :
            Category == "manual_out" ? "”Õ»" :
                                             "Õ—ﬂ…";

        public string TypeIcon =>
            Category == "invoice" ? "??" :
            Category == "inbound" ? "??" :
            Category == "employee_loan" ? "??" :
            Category == "employee_expense" ? "??" :
            Category == "manual_in" ? "?" :
            Category == "manual_out" ? "?" :
                                             "??";

        // œ«∆‰ / „œÌ‰
        public bool IsDebit =>
            Category == "employee_loan" ||
            Category == "employee_expense" ||
            Category == "manual_out" ||
            Amount < 0;
    }

    public class TreasuryService : BaseService
    {
        private readonly SqlConnectionFactory _factory;

        public TreasuryService()
        {
            _factory = new SqlConnectionFactory();
            EnsureManualBalanceTable();
        }

        // ??????????????????????????????????????????????????????
        //   √ﬂœ „‰ ÊÃÊœ ÃœÊ· TreasuryManualEntries
        // ??????????????????????????????????????????????????????
        private void EnsureManualBalanceTable()
        {
            try
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            IF NOT EXISTS (
                                SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                                WHERE TABLE_NAME = 'TreasuryManualEntries'
                            )
                            CREATE TABLE TreasuryManualEntries (
                                Id        INT           IDENTITY(1,1) PRIMARY KEY,
                                Amount    DECIMAL(18,2) NOT NULL,
                                Note      NVARCHAR(500) NOT NULL DEFAULT '',
                                AddedAt   DATETIME      NOT NULL DEFAULT GETDATE()
                            )";
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        // ??????????????????????????????????????????????????????
        //  GET FULL TREASURY SUMMARY
        // ??????????????????????????????????????????????????????
        public TreasurySummaryDto GetSummary()
        {
            return Execute(() =>
            {
                var dto = new TreasurySummaryDto();

                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();

                    // 1. ﬁÌ„… «·„Œ“Ê‰
                    // ? ‰›” „⁄«œ·… ’›Õ… «·„Œ“‰ »«·Ÿ»ÿ:
                    //    Ê«—œ + „— Ã⁄ ⁄„·«¡ + ≈—Ã«⁄ ”Ì«—… -  Õ„Ì· ”Ì«—… (CarLoad)

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT
                                ISNULL(SUM(CASE WHEN stock.NetQty > 0
                                               THEN stock.NetQty * ISNULL(p.PurchasePrice, 0)
                                               ELSE 0 END), 0) AS InventoryValue,
                                COUNT(DISTINCT p.Id)             AS TotalProducts
                            FROM (
                                SELECT
                                    ProductId,
                                    SUM(CASE
                                        WHEN TransactionType IN ('Inbound','Return','CarReturn') THEN  Quantity
                                        WHEN TransactionType IN ('CarLoad')                      THEN -Quantity
                                        ELSE 0
                                    END) AS NetQty
                                FROM WarehouseTransactions
                                GROUP BY ProductId
                            ) stock
                            JOIN Products p ON p.Id = stock.ProductId
                            WHERE stock.NetQty > 0";
                        cmd.CommandType = CommandType.Text;
                        using (var rdr = cmd.ExecuteReader())
                            if (rdr.Read())
                            {
                                dto.InventoryValue = rdr.IsDBNull(0) ? 0m : rdr.GetDecimal(0);
                                dto.TotalProducts = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                            }
                    }

                    // 2. ≈Ì—«œ«  «·›Ê« Ì—
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT ISNULL(SUM(PaidAmount), 0), COUNT(Id) FROM SalesInvoices";
                        cmd.CommandType = CommandType.Text;
                        try
                        {
                            using (var rdr = cmd.ExecuteReader())
                                if (rdr.Read())
                                {
                                    dto.InvoicesRevenue = rdr.IsDBNull(0) ? 0m : rdr.GetDecimal(0);
                                    dto.TotalInvoices = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                                }
                        }
                        catch { }
                    }

                    // 3. ”·› «·„ÊŸ›Ì‰
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT ISNULL(SUM(Amount), 0) FROM EmployeeLoans";
                        cmd.CommandType = CommandType.Text;
                        try { dto.EmployeeExpenses += Convert.ToDecimal(cmd.ExecuteScalar()); } catch { }
                    }

                    // 4. «·„’«—Ì› «·≈œ«—Ì…
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT ISNULL(SUM(Amount), 0) FROM AdminExpenses";
                        cmd.CommandType = CommandType.Text;
                        try { dto.EmployeeExpenses += Convert.ToDecimal(cmd.ExecuteScalar()); } catch { }
                    }
                }

                dto.ManualBalance = GetManualBalanceTotal();

                // «·—’Ìœ «·ﬂ·Ì = ›Ê« Ì— + „÷«› - „’«—Ì›
                dto.TotalBalance = dto.InvoicesRevenue
                                 + dto.ManualBalance
                                 - dto.EmployeeExpenses;

                return dto;
            });
        }

        // ??????????????????????????????????????????????????????
        //  MANUAL BALANCE ó „Œ“¯‰ ›Ì «·œ« «»Ì“ (TreasuryManualEntries)
        // ??????????????????????????????????????????????????????
        public List<ManualBalanceEntryDto> GetManualEntries()
        {
            var list = new List<ManualBalanceEntryDto>();
            try
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT Amount, Note, AddedAt
                            FROM   TreasuryManualEntries
                            ORDER  BY AddedAt DESC";
                        cmd.CommandType = CommandType.Text;
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                                list.Add(new ManualBalanceEntryDto
                                {
                                    Amount = rdr.GetDecimal(0),
                                    Note = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                                    AddedAt = rdr.GetDateTime(2)
                                });
                    }
                }
            }
            catch { }
            return list;
        }

        public decimal GetManualBalanceTotal()
        {
            try
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT ISNULL(SUM(Amount), 0) FROM TreasuryManualEntries";
                        cmd.CommandType = CommandType.Text;
                        var result = cmd.ExecuteScalar();
                        return result == null || result == DBNull.Value ? 0m : Convert.ToDecimal(result);
                    }
                }
            }
            catch { return 0m; }
        }

        public void AddManualEntry(decimal amount, string note)
        {
            if (amount == 0) return;
            try
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO TreasuryManualEntries (Amount, Note, AddedAt)
                            VALUES (@Amount, @Note, @AddedAt)";
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new SqlParameter("@Amount", amount));
                        cmd.Parameters.Add(new SqlParameter("@Note", (note ?? "").Trim()));
                        cmd.Parameters.Add(new SqlParameter("@AddedAt", DateTime.Now));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        // ??????????????????????????????????????????????????????
        //  GET ALL MOVEMENTS
        //  1. ›Ê« Ì— «·»Ì⁄     ? SalesInvoices
        //  2. √Ê«„— «·Ê«—œ     ? InboundOrders
        //  3. ”·› «·„ÊŸ›Ì‰    ? EmployeeLoans
        //  4. „’«—Ì› ≈œ«—Ì…   ? AdminExpenses
        //  5. —’Ìœ ÌœÊÌ        ? TreasuryManualEntries
        // ??????????????????????????????????????????????????????
        public List<TreasuryMovementDto> GetAllMovements()
        {
            var list = new List<TreasuryMovementDto>();

            using (var conn = _factory.CreateConnection())
            {
                conn.Open();

                // ?? 1. ›Ê« Ì— «·»Ì⁄ „‰ SalesInvoices „»«‘—… ??????????
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT
                                si.Id                               AS InvoiceId,
                                ISNULL(si.PaidAmount, 0)            AS PaidAmount,
                                ISNULL(si.TotalAmount, 0)           AS TotalAmount,
                                ISNULL(si.PaymentType, '')         AS PayType,
                                ISNULL(c.Name, '⁄„Ì· €Ì— „Õœœ')    AS CustomerName,
                                si.CreatedAt
                            FROM SalesInvoices si
                            LEFT JOIN Customers c ON c.Id = si.CustomerId
                            WHERE ISNULL(si.PaidAmount, 0) > 0
                            ORDER BY si.CreatedAt DESC";
                        cmd.CommandType = CommandType.Text;
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                            {
                                int invId = Convert.ToInt32(rdr["InvoiceId"]);
                                decimal paid = rdr.GetDecimal(rdr.GetOrdinal("PaidAmount"));
                                decimal total = rdr.GetDecimal(rdr.GetOrdinal("TotalAmount"));
                                string cust = rdr["CustomerName"]?.ToString() ?? "";
                                string payType = rdr["PayType"]?.ToString() ?? "";
                                string sub = $"≈Ã„«·Ì «·›« Ê—…: {total:N2} Ã";
                                if (!string.IsNullOrEmpty(payType)) sub += $"  ï  {payType}";
                                list.Add(new TreasuryMovementDto
                                {
                                    Date = rdr.GetDateTime(rdr.GetOrdinal("CreatedAt")),
                                    Amount = paid,
                                    Note = $"›« Ê—… »Ì⁄ ó {cust}",
                                    SubDetail = sub,
                                    Reference = $"›« Ê—… #{invId}",
                                    Category = "invoice"
                                });
                            }
                    }
                }
                catch { }

                // ?? 2. √Ê«„— «·Ê«—œ ???????????????????????????????????
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT
                                io.Id                                                       AS OrderId,
                                io.CreatedAt,
                                ISNULL(SUM(ioi.Quantity * ISNULL(ioi.PurchasePrice, 0)), 0) AS TotalCost,
                                COUNT(ioi.Id)                                               AS ItemCount
                            FROM InboundOrders io
                            LEFT JOIN InboundOrderItems ioi ON ioi.InboundOrderId = io.Id
                            GROUP BY io.Id, io.CreatedAt
                            ORDER BY io.CreatedAt DESC";
                        cmd.CommandType = CommandType.Text;
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                            {
                                int orderId = Convert.ToInt32(rdr["OrderId"]);
                                decimal cost = rdr.GetDecimal(rdr.GetOrdinal("TotalCost"));
                                int itemCount = Convert.ToInt32(rdr["ItemCount"]);
                                string sub = $"{itemCount} ’‰›  ï  ≈Ã„«·Ì: {cost:N2} Ã";
                                list.Add(new TreasuryMovementDto
                                {
                                    Date = rdr.GetDateTime(rdr.GetOrdinal("CreatedAt")),
                                    Amount = -cost,
                                    Note = $"√„— Ê«—œ #{orderId}",
                                    SubDetail = sub,
                                    Reference = $"Ê«—œ #{orderId}",
                                    Category = "inbound"
                                });
                            }
                    }
                }
                catch { }

                // ?? 3. ”·› «·„ÊŸ›Ì‰ ???????????????????????????????????
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT el.Id, el.Amount, el.CreatedAt,
                                ISNULL(e.Name, '') AS EmpName
                            FROM EmployeeLoans el
                            LEFT JOIN Employees e ON e.Id = el.EmployeeId
                            ORDER BY el.CreatedAt DESC";
                        cmd.CommandType = CommandType.Text;
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                            {
                                string empName = rdr["EmpName"]?.ToString() ?? "";
                                int loanId = Convert.ToInt32(rdr["Id"]);
                                list.Add(new TreasuryMovementDto
                                {
                                    Date = rdr.GetDateTime(rdr.GetOrdinal("CreatedAt")),
                                    Amount = -rdr.GetDecimal(rdr.GetOrdinal("Amount")),
                                    Note = string.IsNullOrEmpty(empName)
                                                ? $"”·›… „ÊŸ› #{loanId}"
                                                : $"”·›… „ÊŸ› ó {empName}",
                                    SubDetail = "",
                                    Reference = $"”·›… #{loanId}",
                                    Category = "employee_loan"
                                });
                            }
                    }
                }
                catch { }

                // ?? 4. «·„’«—Ì› «·≈œ«—Ì… ??????????????????????????????
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT Id, Amount, CreatedAt FROM AdminExpenses ORDER BY CreatedAt DESC";
                        cmd.CommandType = CommandType.Text;
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                            {
                                int expId = Convert.ToInt32(rdr["Id"]);
                                decimal amt = rdr.GetDecimal(rdr.GetOrdinal("Amount"));
                                list.Add(new TreasuryMovementDto
                                {
                                    Date = rdr.GetDateTime(rdr.GetOrdinal("CreatedAt")),
                                    Amount = -amt,
                                    Note = $"„’—Ê› ≈œ«—Ì #{expId}",
                                    SubDetail = "",
                                    Reference = $"„’—Ê› #{expId}",
                                    Category = "employee_expense"
                                });
                            }
                    }
                }
                catch { }
            }

            // ?? 5. —’Ìœ „÷«› / „Œ’Ê„ ÌœÊÌ« ?????????????????????????
            foreach (var m in GetManualEntries())
            {
                bool isOut = m.Amount < 0;
                list.Add(new TreasuryMovementDto
                {
                    Date = m.AddedAt,
                    Amount = m.Amount,
                    Note = isOut ? $"”Õ» ó {m.Note}" : $"≈Ìœ«⁄ ó {m.Note}",
                    SubDetail = "",
                    Reference = "ÌœÊÌ",
                    Category = isOut ? "manual_out" : "manual_in"
                });
            }

            list.Sort((a, b) => b.Date.CompareTo(a.Date));
            return list;
        }

        // ??????????????????????????????????????????????????????
        //  GET PROFIT TOTAL ó ’«›Ì «·—»Õ «· ‘€Ì·Ì
        // ??????????????????????????????????????????????????????
        public decimal GetProfitTotal()
        {
            return Execute(() =>
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            WITH AvgCost AS (
                                SELECT
                                    ProductId,
                                    CASE
                                        WHEN SUM(Quantity) = 0 THEN 0
                                        ELSE SUM(Quantity * PurchasePrice) / SUM(Quantity)
                                    END AS AvgUnitCost
                                FROM WarehouseTransactions
                                WHERE TransactionType = 'Inbound'
                                GROUP BY ProductId
                            )
                            SELECT
                                ISNULL(SUM(sii.Quantity * ISNULL(sii.SalePrice,       0)), 0) AS TotalRevenue,
                                ISNULL(SUM(sii.Quantity * ISNULL(ac.AvgUnitCost,      0)), 0) AS TotalCost
                            FROM SalesInvoiceItems sii
                            LEFT JOIN AvgCost ac ON ac.ProductId = sii.ProductId";
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                decimal revenue = rdr.IsDBNull(0) ? 0m : rdr.GetDecimal(0);
                                decimal cost = rdr.IsDBNull(1) ? 0m : rdr.GetDecimal(1);
                                return revenue - cost;
                            }
                        }
                        return 0m;
                    }
                }
            });
        }

        // ??????????????????????????????????????????????????????
        //  GET INBOUND TOTAL ó ≈Ã„«·Ì ﬁÌ„… √Ê«„— «·Ê«—œ
        // ??????????????????????????????????????????????????????
        public decimal GetInboundTotal()
        {
            return Execute(() =>
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT ISNULL(SUM(ioi.Quantity * ISNULL(ioi.PurchasePrice, 0)), 0)
                            FROM   InboundOrders io
                            JOIN   InboundOrderItems ioi ON ioi.InboundOrderId = io.Id";
                        cmd.CommandType = System.Data.CommandType.Text;
                        var result = cmd.ExecuteScalar();
                        return result == null || result == DBNull.Value ? 0m : Convert.ToDecimal(result);
                    }
                }
            });
        }
    }
}