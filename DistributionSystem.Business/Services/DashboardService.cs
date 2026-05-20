using System;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Business.Services;
using DistributionSystem.Data.Data;

namespace DistributionSystem.Business.Services
{
    public class DashboardService
    {
        private readonly SqlConnectionFactory _connectionFactory;

        public DashboardService()
        {
            _connectionFactory = new SqlConnectionFactory();
        }

        public DashboardStats GetStats()
        {
            var stats = new DashboardStats();

            using (var conn = _connectionFactory.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();

                // Products
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.Products";
                stats.TotalProducts = Convert.ToInt32(cmd.ExecuteScalar());

                // Customers
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.Customers";
                stats.TotalCustomers = Convert.ToInt32(cmd.ExecuteScalar());

                // Sales
                try
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.SalesInvoices";
                    stats.TotalSales = Convert.ToInt32(cmd.ExecuteScalar());
                }
                catch { stats.TotalSales = 0; }

                // Purchases
                try
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM dbo.InboundOrders";
                    stats.TotalPurchases = Convert.ToInt32(cmd.ExecuteScalar());
                }
                catch { stats.TotalPurchases = 0; }

                // Low stock alerts
                try
                {
                    cmd.CommandText = @"
                        SELECT ISNULL(SUM(CASE WHEN Balance <= 5 THEN 1 ELSE 0 END), 0)
                        FROM (
                            SELECT p.Id,
                                ISNULL(SUM(CASE
                                    WHEN wt.TransactionType IN ('Inbound','Return')   THEN  wt.Quantity
                                    WHEN wt.TransactionType IN ('CarLoad','Outbound') THEN -wt.Quantity
                                    ELSE 0
                                END), 0) AS Balance
                            FROM dbo.Products p
                            LEFT JOIN dbo.WarehouseTransactions wt ON wt.ProductId = p.Id
                            GROUP BY p.Id
                        ) x";
                    stats.LowStockAlerts = Convert.ToInt32(cmd.ExecuteScalar());
                }
                catch { stats.LowStockAlerts = 0; }
            }

            // ? ÇáÑÕíÏ Çáßáí — äÝÓ ÇáãÚÇÏáÉ ÈÇáÙÈØ Òí TreasuryForm.RefreshAsync()
            try
            {
                var treasury = new TreasuryService();
                var summary = treasury.GetSummary();

                decimal inboundTotal = 0m;
                try { inboundTotal = treasury.GetInboundTotal(); } catch { }

                // äÝÓ ÇáÓØÑ ÈÇáÙÈØ ãä TreasuryForm:
                // decimal total = summary.ManualBalance + summary.InvoicesRevenue - inboundTotal - summary.EmployeeExpenses;
                stats.TreasuryBalance = summary.ManualBalance
                                      + summary.InvoicesRevenue
                                      - inboundTotal
                                      - summary.EmployeeExpenses;
            }
            catch { stats.TreasuryBalance = 0; }

            return stats;
        }
    }
}