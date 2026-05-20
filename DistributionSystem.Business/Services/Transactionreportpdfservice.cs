using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using DistributionSystem.Business.Dtos;

namespace DistributionSystem.Business.Services
{
    public class TransactionsReportPdfService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ── Arabic font (embedded) ────────────────────────────────
        private static BaseFont GetArabicFont()
        {
            // مسار الفونت — تأكد إن الملف موجود في المشروع أو استخدم مسار مطلق
            string[] candidates = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "Cairo-Regular.ttf"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "arial.ttf"),
                @"C:\Windows\Fonts\arial.ttf",
                @"C:\Windows\Fonts\tahoma.ttf"
            };
            foreach (var path in candidates)
                if (File.Exists(path))
                    return BaseFont.CreateFont(path, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            throw new FileNotFoundException("لم يُعثر على ملف الخط. ضع Cairo-Regular.ttf في مجلد Fonts.");
        }

        // ═══════════════════════════════════════════════════════════
        //  ENTRY POINT
        // ═══════════════════════════════════════════════════════════
        /// <summary>
        /// يولد PDF للتقرير اليومي ويفتحه تلقائياً.
        /// </summary>
        /// <param name="date">تاريخ التقرير</param>
        /// <param name="transactions">كل الحركات (قبل الفلترة — الـ service بيفلترها)</param>
        public void GenerateAndOpen(DateTime date, IEnumerable<WarehouseTransactionViewDto> transactions)
        {
            var rows = transactions
                .Where(t => t.CreatedAt.Date == date.Date)
                .OrderBy(t => t.CreatedAt)
                .ToList();

            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"تقرير_السجل_{date:yyyy-MM-dd}.pdf");

            using (var doc = new Document(PageSize.A4, 36, 36, 50, 50))
            {
                var writer = PdfWriter.GetInstance(doc, new FileStream(path, FileMode.Create));
                doc.Open();

                var bf = GetArabicFont();

                // ── Fonts ─────────────────────────────────────────
                var fTitle = new Font(bf, 20, Font.BOLD, new BaseColor(26, 47, 94));
                var fSub = new Font(bf, 11, Font.NORMAL, new BaseColor(100, 116, 139));
                var fSection = new Font(bf, 13, Font.BOLD, new BaseColor(26, 47, 94));
                var fHdr = new Font(bf, 10, Font.BOLD, BaseColor.WHITE);
                var fCell = new Font(bf, 10, Font.NORMAL, new BaseColor(55, 65, 81));
                var fCellBold = new Font(bf, 10, Font.BOLD, new BaseColor(15, 23, 42));
                var fGreen = new Font(bf, 11, Font.BOLD, new BaseColor(5, 150, 105));
                var fRed = new Font(bf, 11, Font.BOLD, new BaseColor(220, 38, 38));
                var fBlue = new Font(bf, 11, Font.BOLD, new BaseColor(26, 47, 94));

                // ══════════════════════════════════════════════════
                //  HEADER BANNER
                // ══════════════════════════════════════════════════
                var headerTable = new PdfPTable(1) { WidthPercentage = 100, RunDirection = PdfWriter.RUN_DIRECTION_RTL };
                var hCell = new PdfPCell
                {
                    BackgroundColor = new BaseColor(26, 47, 94),
                    Border = Rectangle.NO_BORDER,
                    Padding = 18,
                    HorizontalAlignment = Element.ALIGN_CENTER
                };
                hCell.AddElement(new Paragraph("تقرير حركة التنقلات اليومي", fTitle)
                { Alignment = Element.ALIGN_CENTER, SpacingAfter = 4 });
                hCell.AddElement(new Paragraph(
                    $"تاريخ: {date:yyyy/MM/dd}   |   طُبع في: {DateTime.Now:HH:mm}", fSub)
                { Alignment = Element.ALIGN_CENTER });
                headerTable.AddCell(hCell);
                doc.Add(headerTable);
                doc.Add(new Paragraph(" ") { SpacingAfter = 10 });

                // ══════════════════════════════════════════════════
                //  SUMMARY CARDS  (ملخص الحركات)
                // ══════════════════════════════════════════════════
                int totalInbound = rows.Where(r => IsInbound(r.TransactionType)).Sum(r => r.Quantity);
                int totalOutbound = rows.Where(r => IsOutbound(r.TransactionType)).Sum(r => Math.Abs(r.Quantity));
                decimal valueInbound = rows.Where(r => IsInbound(r.TransactionType)).Sum(r => r.TotalValue);
                decimal valueOutbound = rows.Where(r => IsOutbound(r.TransactionType)).Sum(r => r.TotalValue);
                int totalRows = rows.Count;

                var sumTable = new PdfPTable(3) { WidthPercentage = 100, SpacingAfter = 16, RunDirection = PdfWriter.RUN_DIRECTION_RTL };
                sumTable.SetWidths(new float[] { 1f, 1f, 1f });

                AddSummaryCard(sumTable, "إجمالي الوارد", $"{totalInbound} قطعة", $"{valueInbound:N2} ج", new BaseColor(236, 253, 245), new BaseColor(5, 150, 105), fSection);
                AddSummaryCard(sumTable, "إجمالي الصادر", $"{totalOutbound} قطعة", $"{valueOutbound:N2} ج", new BaseColor(254, 242, 242), new BaseColor(220, 38, 38), fSection);
                AddSummaryCard(sumTable, "عدد الحركات", $"{totalRows} حركة", "", new BaseColor(239, 246, 255), new BaseColor(26, 47, 94), fSection);

                doc.Add(sumTable);

                // ── No Data ───────────────────────────────────────
                if (rows.Count == 0)
                {
                    var noPara = new Paragraph("لا توجد حركات مسجلة في هذا اليوم.", new Font(bf, 13, Font.BOLD, new BaseColor(100, 116, 139)))
                    { Alignment = Element.ALIGN_CENTER, SpacingBefore = 30 };
                    doc.Add(noPara);
                    doc.Close();
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    return;
                }

                // ══════════════════════════════════════════════════
                //  TRANSACTIONS TABLE
                // ══════════════════════════════════════════════════
                var secLabel = new Paragraph("تفاصيل الحركات", fSection)
                { Alignment = Element.ALIGN_RIGHT, SpacingBefore = 8, SpacingAfter = 6 };
                doc.Add(secLabel);

                var tbl = new PdfPTable(6)
                {
                    WidthPercentage = 100,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    SpacingAfter = 10
                };
                tbl.SetWidths(new float[] { 2.4f, 1.6f, 0.9f, 1.2f, 1.2f, 1.8f });

                // Column headers
                string[] headers = { "المنتج / البيان", "نوع الحركة", "الكمية", "سعر الوحدة", "الإجمالي", "التاريخ والوقت" };
                var hdrBg = new BaseColor(26, 47, 94);
                foreach (var h in headers)
                {
                    var c = new PdfPCell(new Phrase(h, fHdr))
                    {
                        BackgroundColor = hdrBg,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        Padding = 8,
                        Border = Rectangle.NO_BORDER
                    };
                    tbl.AddCell(c);
                }

                // Rows
                bool alt = false;
                foreach (var row in rows)
                {
                    var rowBg = alt ? new BaseColor(250, 251, 255) : BaseColor.WHITE;
                    alt = !alt;

                    AddCell(tbl, row.ProductName ?? "—", rowBg, fCellBold, Element.ALIGN_RIGHT);
                    AddCell(tbl, row.TransactionType ?? "—", rowBg, fCell, Element.ALIGN_CENTER);
                    AddCell(tbl, IsMoneyType(row.TransactionType) ? "—" : row.Quantity.ToString("N0", Inv),
                                                                         rowBg, fCell, Element.ALIGN_CENTER);
                    AddCell(tbl, row.UnitCost.ToString("N2", Inv) + " ج", rowBg, fCell, Element.ALIGN_CENTER);

                    // إجمالي — لون حسب الاتجاه
                    bool isOut = IsOutbound(row.TransactionType);
                    var totalFont = isOut ? fRed : (IsInbound(row.TransactionType) ? fGreen : fBlue);
                    AddCell(tbl, row.TotalValue.ToString("N2", Inv) + " ج", rowBg, totalFont, Element.ALIGN_CENTER);

                    string dateStr = row.CreatedAt.ToString("yyyy/MM/dd  HH:mm", Inv);
                    AddCell(tbl, dateStr, rowBg, fCell, Element.ALIGN_CENTER);
                }

                doc.Add(tbl);

                // ── Footer ────────────────────────────────────────
                var footer = new Paragraph(
                    $"تم إنشاء هذا التقرير بواسطة نظام التوزيع  —  {DateTime.Now:yyyy/MM/dd HH:mm}",
                    new Font(bf, 8, Font.ITALIC, new BaseColor(148, 163, 184)))
                { Alignment = Element.ALIGN_CENTER, SpacingBefore = 20 };
                doc.Add(footer);

                doc.Close();
            }

            // فتح الـ PDF تلقائياً
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════
        private static bool IsInbound(string t)
            => t == "وارد مخزن" || t == "مرتجع" || t == "إرجاع سيارة"
            || t == "Inbound" || t == "Return" || t == "CarReturn"
            || t == "إيداع خزنة";

        private static bool IsOutbound(string t)
            => t == "صادر" || t == "تحميل سيارة" || t == "خصم خزنة"
            || t == "Outbound" || t == "CarLoad" || t == "CashWithdraw"
            || t == "مصروف موظف" || t == "مصروف إداري" || t == "صرف راتب";

        private static bool IsMoneyType(string t)
            => t == "إيراد بيع" || t == "إيداع خزنة" || t == "خصم خزنة"
            || t == "مصروف موظف" || t == "مصروف إداري" || t == "صرف راتب";

        private static void AddSummaryCard(PdfPTable tbl, string title, string line1, string line2,
            BaseColor bg, BaseColor accent, Font fSection)
        {
            var bf2 = fSection.BaseFont;
            var fTitle2 = new Font(bf2, 10, Font.BOLD, accent);
            var fVal = new Font(bf2, 14, Font.BOLD, accent);
            var fSub2 = new Font(bf2, 10, Font.NORMAL, new BaseColor(100, 116, 139));

            var inner = new PdfPTable(1) { RunDirection = PdfWriter.RUN_DIRECTION_RTL };
            inner.AddCell(new PdfPCell(new Phrase(title, fTitle2))
            { Border = Rectangle.NO_BORDER, BackgroundColor = bg, HorizontalAlignment = Element.ALIGN_CENTER, PaddingTop = 4 });
            inner.AddCell(new PdfPCell(new Phrase(line1, fVal))
            { Border = Rectangle.NO_BORDER, BackgroundColor = bg, HorizontalAlignment = Element.ALIGN_CENTER });
            if (!string.IsNullOrEmpty(line2))
                inner.AddCell(new PdfPCell(new Phrase(line2, fSub2))
                { Border = Rectangle.NO_BORDER, BackgroundColor = bg, HorizontalAlignment = Element.ALIGN_CENTER, PaddingBottom = 4 });

            var cell = new PdfPCell(inner)
            {
                BackgroundColor = bg,
                Border = Rectangle.NO_BORDER,
                Padding = 6,
                HorizontalAlignment = Element.ALIGN_CENTER
            };
            tbl.AddCell(cell);
        }

        private static void AddCell(PdfPTable tbl, string text, BaseColor bg, Font f, int align)
        {
            var c = new PdfPCell(new Phrase(text, f))
            {
                BackgroundColor = bg,
                HorizontalAlignment = align,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 7,
                Border = Rectangle.BOTTOM_BORDER,
                BorderColor = new BaseColor(226, 232, 240),
                BorderWidth = 0.5f
            };
            tbl.AddCell(c);
        }
    }
}