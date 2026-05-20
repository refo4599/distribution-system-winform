using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using DistributionSystem.Business.Dtos;

namespace DistributionSystem.Business.Services
{
    // ══════════════════════════════════════════════════════════════
    //  DTO التقرير اليومي
    // ══════════════════════════════════════════════════════════════
    public class DailyReportDto
    {
        public DateTime ReportDate { get; set; } = DateTime.Today;
        public List<WarehouseTransactionViewDto> Transactions { get; set; } = new List<WarehouseTransactionViewDto>();

        // إجماليات
        public decimal TotalInbound => Transactions.Where(t => t.TransactionType == "وارد مخزن" || t.TransactionType == "Inbound").Sum(t => t.TotalValue);
        public decimal TotalOutbound => Transactions.Where(t => t.TransactionType == "صادر" || t.TransactionType == "Outbound").Sum(t => t.TotalValue);
        public decimal TotalSaleRevenue => Transactions.Where(t => t.TransactionType == "إيراد بيع" || t.TransactionType == "SaleRevenue").Sum(t => t.TotalValue);
        public decimal TotalExpenses => Transactions.Where(t => t.TransactionType == "مصروف موظف" || t.TransactionType == "EmployeeExpense"
                                                                  || t.TransactionType == "مصروف إداري" || t.TransactionType == "AdminExpense"
                                                                  || t.TransactionType == "صرف راتب" || t.TransactionType == "SalaryPayment").Sum(t => t.TotalValue);
        public decimal TotalCashIn => Transactions.Where(t => t.TransactionType == "إيداع خزنة" || t.TransactionType == "CashDeposit").Sum(t => t.TotalValue);
        public decimal TotalCashOut => Transactions.Where(t => t.TransactionType == "خصم خزنة" || t.TransactionType == "CashWithdraw").Sum(t => t.TotalValue);
        public int TotalCount => Transactions.Count;
    }

    // ══════════════════════════════════════════════════════════════
    //  DailyReportPdfService
    // ══════════════════════════════════════════════════════════════
    public class DailyReportPdfService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static string GetWindowsFont(params string[] names)
        {
            string fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            foreach (var name in names)
            {
                string path = Path.Combine(fontsFolder, name);
                if (File.Exists(path)) return path;
            }
            return Path.Combine(fontsFolder, "arial.ttf");
        }

        // تحويل أرقام لعربية
        private static string Ar(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
                sb.Append(c >= '0' && c <= '9' ? (char)(c - '0' + '\u0660') : c);
            return sb.ToString();
        }
        private static string Ar(decimal v, string fmt = "N2") => Ar(v.ToString(fmt, Inv));
        private static string Ar(int v) => Ar(v.ToString());

        // ── خريطة ترجمة أنواع الحركات ──────────────────────
        private static readonly Dictionary<string, string> TypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Inbound",         "وارد مخزن"    }, { "CarLoad",       "تحميل سيارة"  },
            { "Return",          "مرتجع"         }, { "CarReturn",     "إرجاع سيارة"  },
            { "Outbound",        "صادر"           }, { "SaleRevenue",   "إيراد بيع"    },
            { "OpeningBalance",  "رصيد افتتاحي"  }, { "وارد",          "وارد مخزن"    },
            { "EmployeeExpense", "مصروف موظف"    }, { "AdminExpense",  "مصروف إداري"  },
            { "CashDeposit",     "إيداع خزنة"   }, { "CashWithdraw",  "خصم خزنة"     },
            { "SalaryPayment",   "صرف راتب"      },
        };
        private static string Translate(string raw)
            => TypeMap.TryGetValue(raw ?? "", out var v) ? v : (raw ?? "—");

        // ── لون badge كل نوع ────────────────────────────────
        private static (BaseColor text, BaseColor bg) GetTypeBadge(string val)
        {
            switch (val)
            {
                case "وارد مخزن": return (new BaseColor(5, 150, 105), new BaseColor(236, 253, 245));
                case "تحميل سيارة": return (new BaseColor(217, 119, 6), new BaseColor(255, 251, 235));
                case "مرتجع": return (new BaseColor(124, 58, 237), new BaseColor(245, 243, 255));
                case "صادر": return (new BaseColor(220, 38, 38), new BaseColor(254, 242, 242));
                case "إيراد بيع": return (new BaseColor(3, 105, 161), new BaseColor(240, 249, 255));
                case "مصروف موظف": return (new BaseColor(180, 83, 9), new BaseColor(255, 247, 237));
                case "مصروف إداري": return (new BaseColor(190, 24, 93), new BaseColor(253, 242, 248));
                case "إيداع خزنة": return (new BaseColor(4, 120, 87), new BaseColor(236, 253, 245));
                case "خصم خزنة": return (new BaseColor(153, 27, 27), new BaseColor(255, 241, 242));
                case "صرف راتب": return (new BaseColor(29, 78, 216), new BaseColor(239, 246, 255));
                default: return (new BaseColor(107, 114, 128), new BaseColor(249, 250, 251));
            }
        }

        // ════════════════════════════════════════════════════
        //  GenerateDailyReport
        // ════════════════════════════════════════════════════
        public byte[] GenerateDailyReport(DailyReportDto report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36, 36, 36, 36);
                var writer = PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // ── fonts ────────────────────────────────────
                var bfR = BaseFont.CreateFont(
                    GetWindowsFont("Cairo-Regular.ttf", "Cairo-VariableFont_slnt,wght.ttf", "arial.ttf"),
                    BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                var bfB = BaseFont.CreateFont(
                    GetWindowsFont("Cairo-Bold.ttf", "Cairo-VariableFont_slnt,wght.ttf", "arialbd.ttf"),
                    BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                // ── ألوان ────────────────────────────────────
                var darkBlue = new BaseColor(26, 47, 94);
                var midBlue = new BaseColor(21, 101, 192);
                var lightBg = new BaseColor(239, 246, 255);
                var altRow = new BaseColor(248, 250, 255);
                var borderC = new BaseColor(180, 180, 180);
                var greenC = new BaseColor(5, 150, 105);
                var redC = new BaseColor(220, 38, 38);
                var orangeC = new BaseColor(217, 119, 6);

                string dateStr = Ar(report.ReportDate.ToString("yyyy/MM/dd", Inv));
                string dayStr = GetDayName(report.ReportDate.DayOfWeek);

                // ════════════════════════════════════════════
                //  1. لوجو + اسم الشركة
                // ════════════════════════════════════════════
                var logoTbl = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 4f };
                var circleCell = new PdfPCell
                { Border = Rectangle.NO_BORDER, FixedHeight = 80f, HorizontalAlignment = Element.ALIGN_CENTER };
                circleCell.CellEvent = new DailyReportLogoCellEvent(bfB, darkBlue, midBlue);
                logoTbl.AddCell(circleCell);
                logoTbl.AddCell(new PdfPCell(
                    new Phrase("شركة بصوص للتوزيع", new Font(bfB, 11, Font.BOLD, darkBlue)))
                {
                    Border = Rectangle.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    PaddingBottom = 2f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL
                });
                doc.Add(logoTbl);
                doc.Add(DrawRule(darkBlue, midBlue));

                // ════════════════════════════════════════════
                //  2. عنوان التقرير
                // ════════════════════════════════════════════
                var titleTbl = new PdfPTable(1)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 100,
                    SpacingBefore = 8f,
                    SpacingAfter = 4f
                };
                titleTbl.AddCell(new PdfPCell(
                    new Phrase("التقرير اليومي", new Font(bfB, 17, Font.BOLD, darkBlue)))
                {
                    Border = Rectangle.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL
                });
                titleTbl.AddCell(new PdfPCell(
                    new Phrase($"\u200Fيوم  {dayStr}  —  {dateStr}", new Font(bfR, 11, Font.NORMAL, new BaseColor(80, 80, 80))))
                {
                    Border = Rectangle.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    PaddingBottom = 4f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL
                });
                doc.Add(titleTbl);
                doc.Add(DrawRule(darkBlue, midBlue));

                // ════════════════════════════════════════════
                //  3. ملخص الإجماليات (6 بطاقات)
                // ════════════════════════════════════════════
                doc.Add(SectionHeader("ملخص اليوم", bfB, darkBlue));

                var summaryTbl = new PdfPTable(3)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 100,
                    SpacingBefore = 4f,
                    SpacingAfter = 8f
                };
                summaryTbl.SetWidths(new float[] { 1f, 1f, 1f });

                void AddSummaryCard(string label, string value, BaseColor textColor, BaseColor bgColor)
                {
                    var cell = new PdfPCell
                    {
                        BackgroundColor = bgColor,
                        Border = Rectangle.BOX,
                        BorderColor = new BaseColor(200, 210, 230),
                        BorderWidth = 0.8f,
                        Padding = 10f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL
                    };
                    cell.AddElement(new Phrase(value, new Font(bfB, 13, Font.BOLD, textColor)) { });
                    cell.AddElement(new Phrase("\n" + label, new Font(bfR, 9, Font.NORMAL, new BaseColor(100, 100, 100))));
                    summaryTbl.AddCell(cell);
                }

                AddSummaryCard("وارد المخزن", Ar(report.TotalInbound) + " ج", greenC, new BaseColor(240, 253, 244));
                AddSummaryCard("إيراد البيع", Ar(report.TotalSaleRevenue) + " ج", midBlue, lightBg);
                AddSummaryCard("المبيعات الصادرة", Ar(report.TotalOutbound) + " ج", redC, new BaseColor(254, 242, 242));
                AddSummaryCard("المصاريف", Ar(report.TotalExpenses) + " ج", orangeC, new BaseColor(255, 251, 235));
                AddSummaryCard("إيداع الخزنة", Ar(report.TotalCashIn) + " ج", greenC, new BaseColor(240, 253, 244));
                AddSummaryCard("خصم الخزنة", Ar(report.TotalCashOut) + " ج", redC, new BaseColor(254, 242, 242));
                doc.Add(summaryTbl);

                // إجمالي الحركات
                var cntTbl = new PdfPTable(1)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 40,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    SpacingAfter = 10f
                };
                cntTbl.AddCell(new PdfPCell(
                    new Phrase($"\u200Fإجمالي الحركات:  {Ar(report.TotalCount)}  حركة",
                        new Font(bfB, 11, Font.BOLD, darkBlue)))
                {
                    BackgroundColor = lightBg,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 8f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    Border = Rectangle.BOX,
                    BorderColor = new BaseColor(190, 210, 240),
                    BorderWidth = 0.8f
                });
                doc.Add(cntTbl);

                doc.Add(DrawRule(borderC, borderC));

                // ════════════════════════════════════════════
                //  4. جدول تفاصيل الحركات
                // ════════════════════════════════════════════
                doc.Add(SectionHeader("تفاصيل الحركات", bfB, darkBlue));

                if (report.Transactions.Count == 0)
                {
                    var emptyTbl = new PdfPTable(1)
                    { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 8f };
                    emptyTbl.AddCell(new PdfPCell(
                        new Phrase("لا توجد حركات في هذا اليوم", new Font(bfR, 11, Font.NORMAL, new BaseColor(150, 150, 150))))
                    {
                        Border = Rectangle.NO_BORDER,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 16f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL
                    });
                    doc.Add(emptyTbl);
                }
                else
                {
                    // رأس الجدول
                    float[] colW = { 0.35f, 2.2f, 1.5f, 1.0f, 1.2f, 1.2f, 1.6f };
                    var detailTbl = new PdfPTable(7)
                    {
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        WidthPercentage = 100,
                        SpacingBefore = 4f,
                        SpacingAfter = 6f
                    };
                    detailTbl.SetWidths(colW);

                    // هيدر
                    string[] headers = { "#", "المنتج / البيان", "نوع الحركة", "الكمية", "سعر الوحدة", "الإجمالي", "الوقت" };
                    foreach (var h in headers)
                        detailTbl.AddCell(new PdfPCell(
                            new Phrase(h, new Font(bfB, 9.5f, Font.BOLD, BaseColor.WHITE)))
                        {
                            BackgroundColor = darkBlue,
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            Padding = 7f,
                            RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                            BorderColor = borderC,
                            BorderWidth = 0.5f
                        });

                    // صفوف البيانات
                    var sorted = report.Transactions
                        .OrderBy(t => t.CreatedAt)
                        .ToList();

                    for (int r = 0; r < sorted.Count; r++)
                    {
                        var tx = sorted[r];
                        var bg = r % 2 == 0 ? BaseColor.WHITE : altRow;
                        string typeTr = Translate(tx.TransactionType);
                        var (typeText, typeBg) = GetTypeBadge(typeTr);
                        string timeStr2 = tx.CreatedAt == DateTime.MinValue ? "—"
                            : Ar(tx.CreatedAt.ToString("HH:mm", Inv));

                        // #
                        detailTbl.AddCell(DC2(Ar(r + 1), new Font(bfR, 9, Font.NORMAL, new BaseColor(120, 120, 120)), Element.ALIGN_CENTER, bg, borderC));
                        // المنتج
                        detailTbl.AddCell(DC2(tx.ProductName ?? "—", new Font(bfB, 10, Font.BOLD, darkBlue), Element.ALIGN_RIGHT, bg, borderC));
                        // نوع الحركة (badge ملوّن)
                        detailTbl.AddCell(new PdfPCell(
                            new Phrase(typeTr, new Font(bfB, 8.5f, Font.BOLD, typeText)))
                        {
                            BackgroundColor = typeBg,
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            Padding = 5f,
                            RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                            BorderColor = borderC,
                            BorderWidth = 0.5f
                        });
                        // الكمية
                        bool isMoney = typeTr == "إيراد بيع" || typeTr == "إيداع خزنة" || typeTr == "خصم خزنة"
                                    || typeTr == "مصروف موظف" || typeTr == "مصروف إداري" || typeTr == "صرف راتب";
                        string qtyStr = isMoney ? "—" : Ar(Math.Abs(tx.Quantity));
                        detailTbl.AddCell(DC2(qtyStr, new Font(bfR, 10, Font.NORMAL, new BaseColor(60, 60, 60)), Element.ALIGN_CENTER, bg, borderC));
                        // سعر الوحدة
                        detailTbl.AddCell(DC2(tx.UnitCost > 0 ? Ar(tx.UnitCost) + " ج" : "—", new Font(bfR, 10, Font.NORMAL, new BaseColor(60, 60, 60)), Element.ALIGN_CENTER, bg, borderC));
                        // الإجمالي
                        bool isOut2 = typeTr == "صادر" || typeTr == "تحميل سيارة" || typeTr == "خصم خزنة"
                                   || typeTr == "مصروف موظف" || typeTr == "مصروف إداري" || typeTr == "صرف راتب";
                        var totColor = isOut2 ? redC : greenC;
                        detailTbl.AddCell(DC2(Ar(tx.TotalValue) + " ج", new Font(bfB, 10, Font.BOLD, totColor), Element.ALIGN_CENTER, bg, borderC));
                        // الوقت
                        detailTbl.AddCell(DC2(timeStr2, new Font(bfR, 9, Font.NORMAL, new BaseColor(100, 100, 100)), Element.ALIGN_CENTER, bg, borderC));
                    }

                    doc.Add(detailTbl);

                    // ── إجماليات نهاية الجدول ──────────────────
                    var totRowTbl = new PdfPTable(7)
                    {
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        WidthPercentage = 100,
                        SpacingAfter = 8f
                    };
                    totRowTbl.SetWidths(colW);
                    totRowTbl.AddCell(new PdfPCell(
                        new Phrase("إجمالي الإيرادات", new Font(bfB, 10, Font.BOLD, greenC)))
                    {
                        Colspan = 5,
                        BackgroundColor = new BaseColor(240, 253, 244),
                        HorizontalAlignment = Element.ALIGN_RIGHT,
                        Padding = 8f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BorderColor = new BaseColor(167, 243, 208),
                        BorderWidth = 0.8f
                    });
                    decimal totalIn = report.TotalInbound + report.TotalSaleRevenue + report.TotalCashIn;
                    totRowTbl.AddCell(new PdfPCell(
                        new Phrase(Ar(totalIn) + " ج", new Font(bfB, 11, Font.BOLD, greenC)))
                    {
                        Colspan = 2,
                        BackgroundColor = new BaseColor(240, 253, 244),
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 8f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BorderColor = new BaseColor(167, 243, 208),
                        BorderWidth = 0.8f
                    });

                    totRowTbl.AddCell(new PdfPCell(
                        new Phrase("إجمالي المصاريف والخارج", new Font(bfB, 10, Font.BOLD, redC)))
                    {
                        Colspan = 5,
                        BackgroundColor = new BaseColor(254, 242, 242),
                        HorizontalAlignment = Element.ALIGN_RIGHT,
                        Padding = 8f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BorderColor = new BaseColor(254, 202, 202),
                        BorderWidth = 0.8f
                    });
                    decimal totalOut = report.TotalOutbound + report.TotalExpenses + report.TotalCashOut;
                    totRowTbl.AddCell(new PdfPCell(
                        new Phrase(Ar(totalOut) + " ج", new Font(bfB, 11, Font.BOLD, redC)))
                    {
                        Colspan = 2,
                        BackgroundColor = new BaseColor(254, 242, 242),
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 8f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BorderColor = new BaseColor(254, 202, 202),
                        BorderWidth = 0.8f
                    });
                    doc.Add(totRowTbl);
                }

                doc.Add(DrawRule(borderC, borderC));

                // ════════════════════════════════════════════
                //  5. فوتر
                // ════════════════════════════════════════════
                var footTbl = new PdfPTable(2)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 6f };
                footTbl.SetWidths(new float[] { 1f, 1f });
                footTbl.AddCell(new PdfPCell(
                    new Phrase("تاريخ الطباعة: " + Ar(DateTime.Now.ToString("yyyy/MM/dd  HH:mm", Inv)),
                        new Font(bfR, 8, Font.NORMAL, new BaseColor(150, 150, 150))))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                footTbl.AddCell(new PdfPCell(
                    new Phrase("1  |  Page", new Font(bfR, 8, Font.NORMAL, new BaseColor(150, 150, 150))))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
                doc.Add(footTbl);

                // ════════════════════════════════════════════
                //  6. علامة مائية
                // ════════════════════════════════════════════
                var cbWM = writer.DirectContentUnder;
                cbWM.SaveState();
                var gs = new PdfGState { FillOpacity = 0.04f };
                cbWM.SetGState(gs);
                cbWM.SetColorFill(darkBlue);
                cbWM.SetFontAndSize(bfB, 90f);
                float pw2 = doc.PageSize.Width, ph2 = doc.PageSize.Height;
                cbWM.BeginText();
                cbWM.SetTextMatrix(
                    (float)Math.Cos(35 * Math.PI / 180), (float)Math.Sin(35 * Math.PI / 180),
                   -(float)Math.Sin(35 * Math.PI / 180), (float)Math.Cos(35 * Math.PI / 180),
                    pw2 / 2f - 100f, ph2 / 2f - 30f);
                cbWM.ShowText("بصوص");
                cbWM.EndText();
                cbWM.RestoreState();

                doc.Close();
                return ms.ToArray();
            }
        }

        // ── helpers ──────────────────────────────────────────
        private static PdfPTable DrawRule(BaseColor c1, BaseColor c2)
        {
            var t = new PdfPTable(1) { WidthPercentage = 100, SpacingBefore = 2f, SpacingAfter = 2f };
            t.AddCell(new PdfPCell { Border = Rectangle.BOTTOM_BORDER, BorderColor = c1, BorderWidth = 1.5f, FixedHeight = 3f });
            t.AddCell(new PdfPCell { Border = Rectangle.BOTTOM_BORDER, BorderColor = c2, BorderWidth = 0.5f, FixedHeight = 2f });
            return t;
        }

        private static PdfPTable SectionHeader(string text, BaseFont bfB, BaseColor color)
        {
            var t = new PdfPTable(1)
            { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 8f, SpacingAfter = 4f };
            t.AddCell(new PdfPCell(new Phrase(text, new Font(bfB, 12, Font.BOLD, color)))
            {
                BackgroundColor = new BaseColor(239, 246, 255),
                Border = Rectangle.LEFT_BORDER,
                BorderColor = color,
                BorderWidth = 4f,
                Padding = 7f,
                PaddingRight = 12f,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                HorizontalAlignment = Element.ALIGN_RIGHT
            });
            return t;
        }

        private static PdfPCell DC2(string text, Font font, int align, BaseColor bg, BaseColor border, float pad = 6f)
            => new PdfPCell(new Phrase(text, font))
            {
                HorizontalAlignment = align,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = pad,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                BackgroundColor = bg,
                BorderColor = border,
                BorderWidth = 0.5f
            };

        private static string GetDayName(DayOfWeek d)
        {
            switch (d)
            {
                case DayOfWeek.Saturday: return "السبت";
                case DayOfWeek.Sunday: return "الأحد";
                case DayOfWeek.Monday: return "الاثنين";
                case DayOfWeek.Tuesday: return "الثلاثاء";
                case DayOfWeek.Wednesday: return "الأربعاء";
                case DayOfWeek.Thursday: return "الخميس";
                case DayOfWeek.Friday: return "الجمعة";
                default: return "";
            }
        }
    }

    // ── Logo Cell Event ──────────────────────────────────────────
    internal class DailyReportLogoCellEvent : IPdfPCellEvent
    {
        private readonly BaseFont _bf;
        private readonly BaseColor _dark, _mid;
        public DailyReportLogoCellEvent(BaseFont bf, BaseColor dark, BaseColor mid)
        { _bf = bf; _dark = dark; _mid = mid; }

        public void CellLayout(PdfPCell cell, iTextSharp.text.Rectangle pos, PdfContentByte[] canvases)
        {
            float cx = (pos.Left + pos.Right) / 2f;
            float cy = (pos.Bottom + pos.Top) / 2f;
            float r = 30f;
            var bgCb = canvases[PdfPTable.BACKGROUNDCANVAS];
            bgCb.SaveState();
            bgCb.SetColorFill(_dark); bgCb.Circle(cx, cy, r); bgCb.Fill();
            bgCb.SetLineWidth(2.5f); bgCb.SetColorStroke(_mid);
            bgCb.Circle(cx, cy, r + 3f); bgCb.Stroke();
            bgCb.RestoreState();
            var ct = new ColumnText(canvases[PdfPTable.TEXTCANVAS]);
            ct.RunDirection = PdfWriter.RUN_DIRECTION_RTL;
            ct.SetSimpleColumn(cx - 32f, cy - 9f, cx + 32f, cy + 15f);
            ct.AddText(new Phrase("بصوص", new Font(_bf, 16f, Font.BOLD, BaseColor.WHITE)));
            ct.Alignment = Element.ALIGN_CENTER;
            ct.Go();
        }
    }
}