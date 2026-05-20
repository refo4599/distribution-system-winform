using DistributionSystem.Business.Dtos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace DistributionSystem.Business.Services
{
    public class MonthlyReportScheduler : IDisposable
    {
        private readonly System.Timers.Timer _timer;
        private readonly PdfReportService _pdfService;
        private readonly VehicleService _vehicleService;
        private bool _disposed;

        public MonthlyReportScheduler()
        {
            _pdfService = new PdfReportService();
            _vehicleService = new VehicleService();

            _timer = new System.Timers.Timer(24 * 60 * 60 * 1000);
            _timer.AutoReset = true;
            _timer.Elapsed += (s, e) => SafeCheckAndRun();
            _timer.Start();

            ThreadPool.QueueUserWorkItem(_ => SafeCheckAndRun());
        }

        private void SafeCheckAndRun() { try { CheckAndRun(); } catch { } }

        private void CheckAndRun()
        {
            var today = DateTime.Today;
            if (today.Day != 1) return;

            var prev = today.AddMonths(-1);
            int month = prev.Month;
            int year = prev.Year;

            var vehicles = _vehicleService.GetAllVehicles()
                ?.Where(v => v.IsActive).ToList()
                ?? new List<VehicleDto>();

            int generated = 0;
            foreach (var v in vehicles)
            {
                try
                {
                    var orders = _vehicleService.GetAllDispatchOrders()
                        ?.Where(o => o.VehicleId == v.Id
                                  && o.CreatedAt.Month == month
                                  && o.CreatedAt.Year == year)
                        .ToList() ?? new List<DispatchOrderDto>();

                    if (orders.Count == 0) continue;

                    var pdf = _pdfService.GenerateVehicleMonthlyReport(v, orders, month, year);
                    if (pdf == null || pdf.Length == 0) continue;

                    string folder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "DistributionReports", SanitizeFileName(v.Name));
                    Directory.CreateDirectory(folder);

                    string filePath = Path.Combine(folder,
                        year + "-" + GetArabicMonthName(month) + ".pdf");
                    File.WriteAllBytes(filePath, pdf);
                    generated++;
                }
                catch { }
            }

            if (generated > 0) ShowNotification(generated);
        }

        private void ShowNotification(int count)
        {
            try
            {
                string root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "DistributionReports");
                Directory.CreateDirectory(root);

                string log = Path.Combine(root, "generation-log.txt");
                File.AppendAllText(log,
                    $"{DateTime.Now:yyyy/MM/dd HH:mm} - Êã ÅäÔÇÁ {count} ÊÞÑíÑ ÔåÑí{Environment.NewLine}");

                try { Process.Start("explorer.exe", root); } catch { }
            }
            catch { }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "vehicle";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private static readonly string[] _arabicMonths =
        {
            "", "íäÇíÑ", "ÝÈÑÇíÑ", "ãÇÑÓ", "ÇÈÑíá", "ãÇíæ", "íæäíæ",
            "íæáíæ", "ÇÛÓØÓ", "ÓÈÊãÈÑ", "ÇßÊæÈÑ", "äæÝãÈÑ", "ÏíÓãÈÑ"
        };

        private static string GetArabicMonthName(int m) =>
            (m >= 1 && m <= 12) ? _arabicMonths[m] : m.ToString();

        public void Dispose()
        {
            if (_disposed) return;
            try { _timer?.Stop(); _timer?.Dispose(); } catch { }
            _disposed = true;
        }
    }
}