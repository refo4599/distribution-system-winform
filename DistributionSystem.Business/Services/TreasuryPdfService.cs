using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace DistributionSystem.Business.Services
{
    public class TreasuryPdfService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly BaseColor C_Dark = new BaseColor(26, 47, 94);
        private static readonly BaseColor C_Mid = new BaseColor(21, 101, 192);
        private static readonly BaseColor C_AccBlue = new BaseColor(78, 115, 223);
        private static readonly BaseColor C_Border = new BaseColor(226, 232, 240);
        private static readonly BaseColor C_AltRow = new BaseColor(248, 250, 255);
        private static readonly BaseColor C_TotBg = new BaseColor(219, 234, 254);
        private static readonly BaseColor C_TotBorder = new BaseColor(147, 197, 253);
        private static readonly BaseColor C_SubText = new BaseColor(100, 116, 139);
        private static readonly BaseColor C_MutedText = new BaseColor(148, 163, 184);
        private static readonly BaseColor C_BodyText = new BaseColor(30, 41, 59);
        private static readonly BaseColor C_Green = new BaseColor(22, 163, 74);
        private static readonly BaseColor C_Red = new BaseColor(220, 38, 38);
        private static readonly BaseColor C_Amber = new BaseColor(146, 64, 14);
        private static readonly BaseColor C_InfoBord = new BaseColor(190, 210, 240);

        private static string GetFont(params string[] names)
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            foreach (var n in names) { string p = Path.Combine(folder, n); if (File.Exists(p)) return p; }
            return Path.Combine(folder, "arial.ttf");
        }

        private static string Ar(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) sb.Append(c >= '0' && c <= '9' ? (char)(c - '0' + '\u0660') : c);
            return sb.ToString();
        }
        private static string Fmt(decimal v) => Ar(v.ToString("N2", Inv)) + " ج";
        private static string FmtAbs(decimal v, bool isDebit) =>
            (isDebit ? "- " : "+ ") + Ar(Math.Abs(v).ToString("N2", Inv)) + " ج";

        public byte[] GenerateDailyReport(
            TreasurySummaryDto summary,
            List<TreasuryMovementDto> movements,
            DateTime? selectedDate = null,
            decimal inboundTotal = 0m,
            decimal profitTotal = 0m)
        {
            DateTime rDate = selectedDate ?? DateTime.Today;

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36, 36, 36, 36);
                var writer = PdfWriter.GetInstance(doc, ms);
                doc.Open();

                var bfR = BaseFont.CreateFont(
                    GetFont("Cairo-Regular.ttf", "Cairo Regular.ttf", "tahoma.ttf"),
                    BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                var bfB = BaseFont.CreateFont(
                    GetFont("Cairo-Bold.ttf", "Cairo Bold.ttf", "tahomabd.ttf"),
                    BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                Font FN(float sz, BaseColor c) => new Font(bfR, sz, Font.NORMAL, c);
                Font FB(float sz, BaseColor c) => new Font(bfB, sz, Font.BOLD, c);

                decimal balance = summary.ManualBalance + summary.InvoicesRevenue
                                - inboundTotal - summary.EmployeeExpenses;

                string rDateAr = Ar(rDate.ToString("yyyy/MM/dd", Inv));
                string nowAr = Ar(DateTime.Now.ToString("yyyy/MM/dd  HH:mm", Inv));

                var fBoldWhite = FB(11f, BaseColor.WHITE);
                var fSection = FB(13f, C_Dark);

                // ══ 1. لوجو ══════════════════════════════════════
                var logoTbl = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 4f };
                var circleCell = new PdfPCell
                { Border = Rectangle.NO_BORDER, FixedHeight = 80f, HorizontalAlignment = Element.ALIGN_CENTER };
                circleCell.CellEvent = new TreasuryLogoCellEvent(bfB, C_Dark, C_Mid);
                logoTbl.AddCell(circleCell);
                logoTbl.AddCell(new PdfPCell(
                    new Phrase("شركة بصوص للتوزيع", FB(11f, C_Dark)))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, PaddingBottom = 2f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                doc.Add(logoTbl);
                doc.Add(DrawRule(C_Dark, C_Mid));

                // ══ 2. عنوان التقرير ══════════════════════════════
                var titleTbl = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 8f, SpacingAfter = 8f };
                titleTbl.AddCell(new PdfPCell(
                    new Phrase($"\u200Fتقرير الخزنة  —  يوم  {rDateAr}  .....",
                        FB(15f, BaseColor.BLACK)))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                doc.Add(titleTbl);

                // ══ 3. بيانات السياق ══════════════════════════════
                var infoTbl = new PdfPTable(2)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_LTR,
                    WidthPercentage = 100,
                    SpacingBefore = 6f,
                    SpacingAfter = 10f
                };
                infoTbl.SetWidths(new float[] { 3.5f, 1.4f });

                PdfPCell LblCell(string text) => new PdfPCell(
                    new Phrase(text, FB(11f, BaseColor.WHITE)))
                {
                    BackgroundColor = C_Dark,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    PaddingTop = 7f,
                    PaddingBottom = 7f,
                    PaddingLeft = 4f,
                    PaddingRight = 4f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BorderColor = C_InfoBord,
                    BorderWidth = 0.5f
                };

                PdfPCell ValCell(string text, bool bold = false) => new PdfPCell(
                    new Phrase("\u200F" + text,
                        bold ? FB(11f, C_Dark) : FN(11f, new BaseColor(30, 30, 30))))
                {
                    BackgroundColor = bold ? new BaseColor(239, 246, 255) : new BaseColor(252, 253, 255),
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    PaddingTop = 7f,
                    PaddingBottom = 7f,
                    PaddingRight = 12f,
                    PaddingLeft = 4f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BorderColor = C_InfoBord,
                    BorderWidth = 0.5f
                };

                infoTbl.AddCell(ValCell(rDateAr, true)); infoTbl.AddCell(LblCell("تاريخ التقرير"));
                infoTbl.AddCell(ValCell(nowAr)); infoTbl.AddCell(LblCell("تاريخ الإنشاء"));
                infoTbl.AddCell(ValCell(Fmt(balance), true)); infoTbl.AddCell(LblCell("الرصيد الكلي"));
                doc.Add(infoTbl);
                doc.Add(DrawRule(C_Dark, C_Mid));

                // ══ 4. بطاقات ملخص الخزنة الشامل ════════════════
                var hdr1 = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 4f, SpacingAfter = 6f };
                hdr1.AddCell(new PdfPCell(new Phrase("ملخص الخزنة الشامل", fSection))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 4f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                doc.Add(hdr1);

                // صف 1: 4 كروت
                var r1 = new PdfPTable(4)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 6f };
                r1.SetWidths(new float[] { 1f, 1f, 1f, 1f });
                r1.AddCell(MakeSummaryCard(bfB, bfR, "قيمة المخزون", Fmt(summary.InventoryValue), "تكلفة البضاعة الحالية", new BaseColor(16, 185, 129)));
                r1.AddCell(MakeSummaryCard(bfB, bfR, "قيمة الوارد", Fmt(inboundTotal), "إجمالي أوامر الوارد", new BaseColor(8, 145, 178)));
                r1.AddCell(MakeSummaryCard(bfB, bfR, "رصيد مضاف", Fmt(summary.ManualBalance), "إدخال يدوي", new BaseColor(139, 92, 246)));
                r1.AddCell(MakeSummaryCard(bfB, bfR, "إيرادات الفواتير", Fmt(summary.InvoicesRevenue), "المدفوع الفعلي", C_AccBlue));
                doc.Add(r1);

                // صف 2: 3 كروت
                var r2 = new PdfPTable(3)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 10f };
                r2.SetWidths(new float[] { 1f, 1f, 1f });
                r2.AddCell(MakeSummaryCard(bfB, bfR, "مصاريف الموظفين", Fmt(summary.EmployeeExpenses), "سلف + إدارية", C_Red));
                r2.AddCell(MakeSummaryCard(bfB, bfR, "صافي الربح", Fmt(Math.Abs(profitTotal)), "إيرادات - تكلفة الشراء", profitTotal >= 0 ? C_Green : C_Red));
                r2.AddCell(MakeSummaryCard(bfB, bfR, "الرصيد الكلي", Fmt(balance), "مضاف + إيرادات - وارد - مصاريف", balance >= 0 ? C_Amber : C_Red));
                doc.Add(r2);

                // معادلة الرصيد
                string eq =
                    "الرصيد = رصيد مضاف (" + Fmt(summary.ManualBalance) + ")  +  " +
                    "إيرادات (" + Fmt(summary.InvoicesRevenue) + ")  -  " +
                    "وارد (" + Fmt(inboundTotal) + ")  -  " +
                    "مصاريف (" + Fmt(summary.EmployeeExpenses) + ")  =  " + Fmt(balance);

                var eqT = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 14f };
                eqT.AddCell(new PdfPCell(new Phrase(eq, FN(8f, C_SubText)))
                {
                    BackgroundColor = new BaseColor(248, 250, 252),
                    Padding = 8f,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    Border = Rectangle.BOX,
                    BorderColor = C_Border,
                    BorderWidth = 0.5f
                });
                doc.Add(eqT);

                // ══ 5. بطاقات ملخص اليوم ══════════════════════════
                decimal dayIn = movements.Where(m => !m.IsDebit).Sum(m => m.Amount);
                decimal dayOut = movements.Where(m => m.IsDebit).Sum(m => Math.Abs(m.Amount));
                decimal dayNet = dayIn - dayOut;
                int dayCount = movements.Count;

                var hdr2 = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 4f, SpacingAfter = 6f };
                hdr2.AddCell(new PdfPCell(new Phrase($"ملخص يوم  {rDateAr}", fSection))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 4f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                doc.Add(hdr2);

                var dr = new PdfPTable(4)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 14f };
                dr.SetWidths(new float[] { 1f, 1f, 1f, 1f });
                dr.AddCell(MakeSummaryCard(bfB, bfR, "حركات اليوم", Ar(dayCount.ToString()), "عدد الحركات", new BaseColor(14, 116, 144)));
                dr.AddCell(MakeSummaryCard(bfB, bfR, "إجمالي الدخل", Fmt(dayIn), "المبالغ الواردة", C_Green));
                dr.AddCell(MakeSummaryCard(bfB, bfR, "إجمالي الخرج", Fmt(dayOut), "المبالغ الصادرة", C_Red));
                dr.AddCell(MakeSummaryCard(bfB, bfR, "صافي اليوم", Fmt(Math.Abs(dayNet)), "دخل - خرج", dayNet >= 0 ? C_Amber : C_Red));
                doc.Add(dr);

                // ══ 6. جدول الحركات ═══════════════════════════════
                string tblTitle = dayCount > 0
                    ? $"تفاصيل حركات الخزنة  —  {Ar(dayCount.ToString())} حركة"
                    : $"لا توجد حركات ليوم  {rDateAr}";

                var hdr3 = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 4f, SpacingAfter = 6f };
                hdr3.AddCell(new PdfPCell(new Phrase(tblTitle, fSection))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 4f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                doc.Add(hdr3);

                var tbl = new PdfPTable(5)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 12f };
                tbl.SetWidths(new float[] { 0.85f, 2.3f, 1.0f, 1.1f, 1.0f });

                string[] hdrs = { "النوع", "التفاصيل", "المرجع", "التاريخ", "المبلغ" };
                foreach (var h in hdrs)
                    tbl.AddCell(new PdfPCell(new Phrase(h, fBoldWhite))
                    {
                        BackgroundColor = C_Dark,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        Padding = 8f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BorderColor = new BaseColor(50, 75, 130),
                        BorderWidth = 0.5f
                    });

                if (dayCount == 0)
                {
                    tbl.AddCell(new PdfPCell(new Phrase("لا توجد حركات ليوم " + rDateAr, FN(10f, C_SubText)))
                    {
                        Colspan = 5,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 18f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BackgroundColor = BaseColor.WHITE,
                        BorderColor = C_Border,
                        BorderWidth = 0.5f
                    });
                }
                else
                {
                    int ri = 0; decimal totIn = 0m, totOut = 0m;
                    foreach (var m in movements.OrderBy(x => x.Date))
                    {
                        var bg = ri % 2 == 0 ? BaseColor.WHITE : C_AltRow;

                        BaseColor acc;
                        switch (m.Category)
                        {
                            case "invoice": acc = C_AccBlue; break;
                            case "inbound": acc = new BaseColor(8, 145, 178); break;
                            case "employee_loan": acc = new BaseColor(245, 158, 11); break;
                            case "employee_expense":
                            case "manual_out": acc = C_Red; break;
                            default: acc = new BaseColor(139, 92, 246); break;
                        }

                        if (!m.IsDebit) totIn += m.Amount;
                        else totOut += Math.Abs(m.Amount);

                        // النوع
                        tbl.AddCell(new PdfPCell(new Phrase(m.CategoryLabel, FB(8.5f, acc)))
                        {
                            BackgroundColor = bg,
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            Padding = 6f,
                            RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                            BorderColor = C_Border,
                            BorderWidth = 0.4f
                        });

                        // التفاصيل
                        var dc = new PdfPCell
                        { BackgroundColor = bg, Padding = 5f, RunDirection = PdfWriter.RUN_DIRECTION_RTL, BorderColor = C_Border, BorderWidth = 0.4f };
                        dc.AddElement(new Paragraph(m.Note ?? "", FB(9.5f, C_BodyText)) { Alignment = Element.ALIGN_RIGHT });
                        if (!string.IsNullOrEmpty(m.SubDetail))
                            dc.AddElement(new Paragraph(m.SubDetail, FN(8f, C_SubText)) { Alignment = Element.ALIGN_RIGHT, SpacingBefore = 1f });
                        tbl.AddCell(dc);

                        // المرجع
                        tbl.AddCell(new PdfPCell(new Phrase(m.Reference ?? "", FN(8f, C_MutedText)))
                        {
                            BackgroundColor = bg,
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            Padding = 5f,
                            RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                            BorderColor = C_Border,
                            BorderWidth = 0.4f
                        });

                        // التاريخ
                        var dtc = new PdfPCell
                        { BackgroundColor = bg, Padding = 5f, RunDirection = PdfWriter.RUN_DIRECTION_RTL, BorderColor = C_Border, BorderWidth = 0.4f };
                        dtc.AddElement(new Paragraph(Ar(m.Date.ToString("yyyy/MM/dd", Inv)), FB(9f, C_BodyText)) { Alignment = Element.ALIGN_CENTER });
                        dtc.AddElement(new Paragraph(Ar(m.Date.ToString("HH:mm", Inv)), FN(8f, C_MutedText)) { Alignment = Element.ALIGN_CENTER, SpacingBefore = 1f });
                        tbl.AddCell(dtc);

                        // المبلغ
                        BaseColor amtC = m.IsDebit ? C_Red : C_Green;
                        tbl.AddCell(new PdfPCell(new Phrase(FmtAbs(m.Amount, m.IsDebit), FB(10f, amtC)))
                        {
                            BackgroundColor = bg,
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            Padding = 5f,
                            RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                            BorderColor = C_Border,
                            BorderWidth = 0.4f
                        });

                        ri++;
                    }

                    // صف الإجمالي
                    tbl.AddCell(new PdfPCell(new Phrase("إجمالي اليوم", FB(9.5f, new BaseColor(30, 58, 110))))
                    {
                        Colspan = 3,
                        BackgroundColor = C_TotBg,
                        HorizontalAlignment = Element.ALIGN_RIGHT,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        Padding = 8f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BorderColor = C_TotBorder,
                        BorderWidth = 0.7f
                    });
                    tbl.AddCell(new PdfPCell(new Phrase("+ " + Fmt(totIn), FB(9.5f, C_Green)))
                    {
                        BackgroundColor = C_TotBg,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        Padding = 8f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BorderColor = C_TotBorder,
                        BorderWidth = 0.7f
                    });
                    tbl.AddCell(new PdfPCell(new Phrase("- " + Fmt(totOut), FB(9.5f, C_Red)))
                    {
                        BackgroundColor = C_TotBg,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        Padding = 8f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BorderColor = C_TotBorder,
                        BorderWidth = 0.7f
                    });
                }
                doc.Add(tbl);

                // ══ 7. صندوق الرصيد الكلي ════════════════════════
                doc.Add(new Paragraph("\n"));
                var grandTable = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 60, HorizontalAlignment = Element.ALIGN_RIGHT, SpacingBefore = 6f };
                grandTable.AddCell(new PdfPCell(
                    new Phrase($"الرصيد الكلي للخزنة:   {Fmt(balance)}",
                        FB(13f, balance >= 0 ? C_Mid : C_Red)))
                {
                    BackgroundColor = C_TotBg,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Padding = 12f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    Border = Rectangle.BOX,
                    BorderColor = C_Mid,
                    BorderWidth = 1.5f
                });
                doc.Add(grandTable);

                // ══ 8. فوتر ════════════════════════════════════════
                doc.Add(DrawRule(new BaseColor(200, 200, 200), new BaseColor(200, 200, 200)));
                var footTbl = new PdfPTable(2)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 4f };
                footTbl.SetWidths(new float[] { 1f, 1f });
                footTbl.AddCell(new PdfPCell(new Phrase(
                    "تاريخ الإنشاء: " + Ar(DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss", Inv)),
                    FN(8f, new BaseColor(150, 150, 150))))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                footTbl.AddCell(new PdfPCell(
                    new Phrase("1  |  Page", FN(8f, new BaseColor(150, 150, 150))))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
                doc.Add(footTbl);

                // ══ 9. علامة مائية ════════════════════════════════
                var cbWM = writer.DirectContentUnder;
                cbWM.SaveState();
                var gs = new PdfGState { FillOpacity = 0.05f };
                cbWM.SetGState(gs);
                cbWM.SetColorFill(C_Dark);
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

        // ══════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════
        private static PdfPTable DrawRule(BaseColor c1, BaseColor c2)
        {
            var t = new PdfPTable(1) { WidthPercentage = 100, SpacingBefore = 2f, SpacingAfter = 2f };
            t.AddCell(new PdfPCell { Border = Rectangle.BOTTOM_BORDER, BorderColor = c1, BorderWidth = 1.5f, FixedHeight = 3f });
            t.AddCell(new PdfPCell { Border = Rectangle.BOTTOM_BORDER, BorderColor = c2, BorderWidth = 0.5f, FixedHeight = 2f });
            return t;
        }

        // بطاقة الملخص — خلفية فاتحة مشتقة من الـ accent
        private static PdfPCell MakeSummaryCard(
            BaseFont bfBold, BaseFont bfReg,
            string label, string value, string sub,
            BaseColor accentColor)
        {
            var cardBg = new BaseColor(
                Math.Min(255, accentColor.R + 200),
                Math.Min(255, accentColor.G + 200),
                Math.Min(255, accentColor.B + 200));

            var cardBorder = new BaseColor(
                Math.Min(255, accentColor.R + 130),
                Math.Min(255, accentColor.G + 130),
                Math.Min(255, accentColor.B + 130));

            var inner = new PdfPTable(1) { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100 };

            // شريط علوي ملون
            inner.AddCell(new PdfPCell
            { BackgroundColor = accentColor, FixedHeight = 7f, Border = Rectangle.NO_BORDER, Padding = 0f });

            // التسمية
            inner.AddCell(new PdfPCell(
                new Phrase("\u200F" + label, new Font(bfReg, 9f, Font.NORMAL, new BaseColor(60, 60, 60))))
            {
                Border = Rectangle.NO_BORDER,
                BackgroundColor = cardBg,
                HorizontalAlignment = Element.ALIGN_RIGHT,
                PaddingTop = 8f,
                PaddingRight = 10f,
                PaddingLeft = 10f,
                PaddingBottom = 2f,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL
            });

            // القيمة
            inner.AddCell(new PdfPCell(
                new Phrase("\u200F" + value, new Font(bfBold, 14f, Font.BOLD, accentColor)))
            {
                Border = Rectangle.NO_BORDER,
                BackgroundColor = cardBg,
                HorizontalAlignment = Element.ALIGN_RIGHT,
                PaddingRight = 10f,
                PaddingLeft = 10f,
                PaddingBottom = 2f,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL
            });

            // النص الفرعي
            if (!string.IsNullOrEmpty(sub))
                inner.AddCell(new PdfPCell(
                    new Phrase("\u200F" + sub, new Font(bfReg, 7.5f, Font.NORMAL, new BaseColor(130, 130, 130))))
                {
                    Border = Rectangle.NO_BORDER,
                    BackgroundColor = cardBg,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    PaddingRight = 10f,
                    PaddingLeft = 10f,
                    PaddingBottom = 8f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL
                });

            return new PdfPCell(inner)
            {
                BackgroundColor = cardBg,
                Padding = 0f,
                Border = Rectangle.BOX,
                BorderColor = cardBorder,
                BorderWidth = 1.2f
            };
        }
    }

    // ══ لوجو CellEvent ════════════════════════════════════════
    internal class TreasuryLogoCellEvent : IPdfPCellEvent
    {
        private readonly BaseFont _bf;
        private readonly BaseColor _dark, _mid;
        public TreasuryLogoCellEvent(BaseFont bf, BaseColor dark, BaseColor mid)
        { _bf = bf; _dark = dark; _mid = mid; }

        public void CellLayout(PdfPCell cell, Rectangle pos, PdfContentByte[] canvases)
        {
            float cx = (pos.Left + pos.Right) / 2f;
            float cy = (pos.Bottom + pos.Top) / 2f;
            float r = 30f;

            var bg = canvases[PdfPTable.BACKGROUNDCANVAS];
            bg.SaveState();
            bg.SetColorFill(_dark); bg.Circle(cx, cy, r); bg.Fill();
            bg.SetLineWidth(2.5f); bg.SetColorStroke(_mid);
            bg.Circle(cx, cy, r + 3f); bg.Stroke();
            bg.RestoreState();

            var ct = new ColumnText(canvases[PdfPTable.TEXTCANVAS]);
            ct.RunDirection = PdfWriter.RUN_DIRECTION_RTL;
            ct.SetSimpleColumn(cx - 32f, cy - 9f, cx + 32f, cy + 15f);
            ct.AddText(new Phrase("بصوص", new Font(_bf, 16f, Font.BOLD, BaseColor.WHITE)));
            ct.Alignment = Element.ALIGN_CENTER;
            ct.Go();
        }
    }
}