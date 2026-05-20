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
    public class CustomerDetailsReportService
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

        private static string ToArabicNumerals(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
                sb.Append(c >= '0' && c <= '9' ? (char)(c - '0' + '\u0660') : c);
            return sb.ToString();
        }
        private static string ToAr(decimal value, string format = "N2") =>
            ToArabicNumerals(value.ToString(format, Inv));
        private static string ToAr(int value) =>
            ToArabicNumerals(value.ToString());

        // ══════════════════════════════════════════════════════
        //  ENTRY POINT
        // ══════════════════════════════════════════════════════
        public byte[] GenerateCustomerReport(CustomerFullDetailsDto data)
        {
            if (data?.Customer == null) throw new ArgumentNullException(nameof(data));

            bool isInvoice = data.Customer.CustomerType == CustomerType.Invoices;

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36, 36, 36, 36);
                var writer = PdfWriter.GetInstance(doc, ms);
                doc.Open();

                var bfReg = BaseFont.CreateFont(GetWindowsFont("Cairo-Regular.ttf", "Cairo-VariableFont_slnt,wght.ttf", "arial.ttf"), BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                var bfBold = BaseFont.CreateFont(GetWindowsFont("Cairo-Bold.ttf", "Cairo-VariableFont_slnt,wght.ttf", "arialbd.ttf"), BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                var fNormal = new Font(bfReg, 11, Font.NORMAL, BaseColor.BLACK);
                var fBold = new Font(bfBold, 11, Font.BOLD, BaseColor.BLACK);
                var fBoldWhite = new Font(bfBold, 11, Font.BOLD, BaseColor.WHITE);
                var fSmall = new Font(bfReg, 9, Font.NORMAL, new BaseColor(100, 100, 100));
                var fSection = new Font(bfBold, 13, Font.BOLD, new BaseColor(26, 47, 94));

                var darkBlue = new BaseColor(26, 47, 94);
                var midBlue = new BaseColor(21, 101, 192);
                var lightBg = new BaseColor(239, 246, 255);
                var infoBord = new BaseColor(190, 210, 240);
                var borderC = new BaseColor(180, 180, 180);

                // ══ 1. لوجو ══════════════════════════════════════
                var logoTbl = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 4f };
                var circleCell = new PdfPCell
                { Border = Rectangle.NO_BORDER, FixedHeight = 80f, HorizontalAlignment = Element.ALIGN_CENTER };
                circleCell.CellEvent = new CdrLogoCellEvent(bfBold, darkBlue, midBlue);
                logoTbl.AddCell(circleCell);
                logoTbl.AddCell(new PdfPCell(
                    new Phrase("شركة بصوص للتوزيع", new Font(bfBold, 11, Font.BOLD, darkBlue)))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, PaddingBottom = 2f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                doc.Add(logoTbl);
                doc.Add(DrawRule(darkBlue, midBlue));

                // ══ 2. عنوان التقرير ══════════════════════════════
                string typeLabel = isInvoice ? "فواتير المبيعات" : "أوامر الواردات";
                var titleTbl = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 8f, SpacingAfter = 8f };
                titleTbl.AddCell(new PdfPCell(
                    new Phrase($"\u200Fتقرير عميل \u2014 {typeLabel}  .....",
                        new Font(bfBold, 15, Font.BOLD, BaseColor.BLACK)))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                doc.Add(titleTbl);

                // ══ 3. بيانات العميل ══════════════════════════════
                string dateStr = ToArabicNumerals(DateTime.Now.ToString("yyyy/MM/dd", Inv));
                string timeStr = ToArabicNumerals(DateTime.Now.ToString("HH:mm", Inv));
                string phone = string.IsNullOrWhiteSpace(data.Customer.Phone) ? "—" : data.Customer.Phone;
                string address = string.IsNullOrWhiteSpace(data.Customer.Address) ? "—" : data.Customer.Address;

                var infoTbl = new PdfPTable(2)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_LTR,
                    WidthPercentage = 100,
                    SpacingBefore = 6f,
                    SpacingAfter = 10f
                };
                infoTbl.SetWidths(new float[] { 3.5f, 1.4f });

                PdfPCell LblCell(string text) => new PdfPCell(
                    new Phrase(text, new Font(bfBold, 11, Font.BOLD, BaseColor.WHITE)))
                {
                    BackgroundColor = darkBlue,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    PaddingTop = 7f,
                    PaddingBottom = 7f,
                    PaddingLeft = 4f,
                    PaddingRight = 4f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BorderColor = infoBord,
                    BorderWidth = 0.5f
                };

                PdfPCell ValCell(string text, bool bold = false) => new PdfPCell(
                    new Phrase("\u200F" + text,
                        bold ? new Font(bfBold, 11, Font.BOLD, darkBlue)
                             : new Font(bfReg, 11, Font.NORMAL, new BaseColor(30, 30, 30))))
                {
                    BackgroundColor = bold ? lightBg : new BaseColor(252, 253, 255),
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    PaddingTop = 7f,
                    PaddingBottom = 7f,
                    PaddingRight = 12f,
                    PaddingLeft = 4f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BorderColor = infoBord,
                    BorderWidth = 0.5f
                };

                infoTbl.AddCell(ValCell(data.Customer.Name ?? "—", true)); infoTbl.AddCell(LblCell("اسم العميل"));
                infoTbl.AddCell(ValCell(phone)); infoTbl.AddCell(LblCell("رقم الهاتف"));
                infoTbl.AddCell(ValCell(address)); infoTbl.AddCell(LblCell("العنوان"));
                infoTbl.AddCell(ValCell(typeLabel)); infoTbl.AddCell(LblCell("نوع العميل"));
                infoTbl.AddCell(ValCell($"{dateStr}   {timeStr}")); infoTbl.AddCell(LblCell("تاريخ التقرير"));
                doc.Add(infoTbl);
                doc.Add(DrawRule(darkBlue, midBlue));

                // ══ 4. بطاقات الملخص ══════════════════════════════
                if (isInvoice)
                {
                    var invList = data.Invoices ?? new List<SalesInvoiceDto>();
                    decimal tot = invList.Sum(i => i.TotalAmount);
                    decimal paid = invList.Sum(i => i.PaidAmount);
                    decimal rem = tot - paid;

                    var st = new PdfPTable(4)
                    { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 16f };
                    st.SetWidths(new float[] { 1f, 1f, 1f, 1f });
                    st.AddCell(MakeSummaryCard(bfBold, bfReg, "عدد الفواتير", ToAr(invList.Count), "فاتورة",
                        new BaseColor(26, 47, 94)));
                    st.AddCell(MakeSummaryCard(bfBold, bfReg, "إجمالي كلي", ToAr(tot, "N0"), "جنيه",
                        new BaseColor(21, 101, 192)));
                    st.AddCell(MakeSummaryCard(bfBold, bfReg, "إجمالي مدفوع", ToAr(paid, "N0"), "جنيه",
                        new BaseColor(5, 150, 105)));
                    st.AddCell(MakeSummaryCard(bfBold, bfReg, "المتبقي", ToAr(rem, "N0"), "جنيه",
                        rem > 0 ? new BaseColor(220, 38, 38) : new BaseColor(5, 150, 105)));
                    doc.Add(st);
                }
                else
                {
                    var inbList = data.Inbounds ?? new List<InboundOrderDto>();
                    decimal tot = inbList.Sum(i => i.TotalValue);

                    var st = new PdfPTable(3)
                    { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 16f };
                    st.SetWidths(new float[] { 1f, 1f, 1f });
                    st.AddCell(MakeSummaryCard(bfBold, bfReg, "عدد الأوامر", ToAr(inbList.Count), "أمر",
                        new BaseColor(124, 58, 237)));
                    st.AddCell(MakeSummaryCard(bfBold, bfReg, "إجمالي الواردات", ToAr(tot, "N0"), "جنيه",
                        new BaseColor(21, 101, 192)));
                    st.AddCell(MakeSummaryCard(bfBold, bfReg, "العميل", data.Customer.Name ?? "—", "",
                        new BaseColor(26, 47, 94)));
                    doc.Add(st);
                }

                // ══ 5. جداول التفاصيل ══════════════════════════════
                if (isInvoice)
                    AddInvoicesSection(doc, data.Invoices ?? new List<SalesInvoiceDto>(),
                        fBold, fNormal, fBoldWhite, fSmall, fSection, bfBold);
                else
                    AddInboundSection(doc, data.Inbounds ?? new List<InboundOrderDto>(),
                        fBold, fNormal, fBoldWhite, fSmall, fSection, bfBold);

                // ══ 6. فوتر ════════════════════════════════════════
                doc.Add(DrawRule(borderC, borderC));
                var footTbl = new PdfPTable(2)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 4f };
                footTbl.SetWidths(new float[] { 1f, 1f });
                footTbl.AddCell(new PdfPCell(new Phrase(
                    "تاريخ الإنشاء: " + ToArabicNumerals(DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss", Inv)),
                    new Font(bfReg, 8, Font.NORMAL, new BaseColor(150, 150, 150))))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                footTbl.AddCell(new PdfPCell(
                    new Phrase("1  |  Page", new Font(bfReg, 8, Font.NORMAL, new BaseColor(150, 150, 150))))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
                doc.Add(footTbl);

                // ══ 7. علامة مائية ════════════════════════════════
                var cbWM = writer.DirectContentUnder;
                cbWM.SaveState();
                var gs = new PdfGState { FillOpacity = 0.05f };
                cbWM.SetGState(gs);
                cbWM.SetColorFill(darkBlue);
                cbWM.SetFontAndSize(bfBold, 90f);
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
        //  قسم الفواتير
        // ══════════════════════════════════════════════════════
        private void AddInvoicesSection(
            Document doc, List<SalesInvoiceDto> invoices,
            Font fBold, Font fNormal, Font fBoldWhite, Font fSmall, Font fSection, BaseFont bfBold)
        {
            var darkBlue = new BaseColor(26, 47, 94);
            var midBlue = new BaseColor(30, 58, 110);
            var altRowBg = new BaseColor(248, 250, 255);
            var lightBg = new BaseColor(239, 246, 255);

            var hdr1 = new PdfPTable(1)
            { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 6f, SpacingAfter = 6f };
            hdr1.AddCell(new PdfPCell(new Phrase("تفاصيل فواتير المبيعات", fSection))
            { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 4f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
            doc.Add(hdr1);

            string[] colHdrs = { "رقم", "التاريخ", "السيارة", "الإجمالي", "المدفوع", "المتبقي", "الحالة" };
            float[] colWts = { 0.7f, 1.6f, 1.4f, 1.2f, 1.2f, 1.2f, 1f };

            var tbl = new PdfPTable(colHdrs.Length)
            { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 10f };
            tbl.SetWidths(colWts);

            foreach (var h in colHdrs)
                tbl.AddCell(new PdfPCell(new Phrase(h, fBoldWhite))
                { BackgroundColor = darkBlue, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 7f, RunDirection = PdfWriter.RUN_DIRECTION_RTL, BorderColor = midBlue, BorderWidth = 0.5f });

            int rowIndex = 0;
            foreach (var inv in invoices)
            {
                var bg = rowIndex++ % 2 == 0 ? BaseColor.WHITE : altRowBg;
                DateTime dt = inv.CreatedAt.Kind == DateTimeKind.Utc ? inv.CreatedAt.ToLocalTime()
                    : DateTime.SpecifyKind(inv.CreatedAt, DateTimeKind.Utc).ToLocalTime();
                decimal rem = inv.TotalAmount - inv.PaidAmount;
                bool done = inv.Status == "Completed";

                tbl.AddCell(MakeDataCell(ToAr(inv.Id), fBold, Element.ALIGN_CENTER, bg));
                tbl.AddCell(MakeDataCell(ToArabicNumerals(dt.ToString("yyyy/MM/dd HH:mm", Inv)), fSmall, Element.ALIGN_CENTER, bg));
                tbl.AddCell(MakeDataCell(inv.VehicleName ?? "—", fNormal, Element.ALIGN_CENTER, bg));
                tbl.AddCell(MakeDataCell(ToAr(inv.TotalAmount) + " ج", fBold, Element.ALIGN_CENTER, bg));
                tbl.AddCell(MakeDataCell(ToAr(inv.PaidAmount) + " ج", new Font(bfBold, 11, Font.BOLD, new BaseColor(5, 150, 105)), Element.ALIGN_CENTER, bg));
                tbl.AddCell(MakeDataCell(ToAr(rem) + " ج", new Font(bfBold, 11, Font.BOLD, rem > 0 ? new BaseColor(220, 38, 38) : new BaseColor(5, 150, 105)), Element.ALIGN_CENTER, bg));
                tbl.AddCell(MakeDataCell(done ? "مكتملة" : "معلقة",
                    new Font(bfBold, 10, Font.BOLD, done ? new BaseColor(5, 150, 105) : new BaseColor(217, 119, 6)),
                    Element.ALIGN_CENTER, done ? new BaseColor(236, 253, 245) : new BaseColor(255, 251, 235)));
            }
            doc.Add(tbl);

            var withItems = invoices.Where(i => i.Items != null && i.Items.Count > 0).ToList();
            if (withItems.Count == 0) return;

            var hdr2 = new PdfPTable(1)
            { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 10f, SpacingAfter = 4f };
            hdr2.AddCell(new PdfPCell(new Phrase("تفاصيل منتجات كل فاتورة", fSection))
            { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 4f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
            doc.Add(hdr2);

            foreach (var inv in withItems)
            {
                DateTime dt2 = inv.CreatedAt.Kind == DateTimeKind.Utc ? inv.CreatedAt.ToLocalTime()
                    : DateTime.SpecifyKind(inv.CreatedAt, DateTimeKind.Utc).ToLocalTime();

                var invHdr = new PdfPTable(2)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 8f, SpacingAfter = 2f };
                invHdr.SetWidths(new float[] { 1.5f, 1f });
                invHdr.AddCell(new PdfPCell(new Phrase($"فاتورة #{inv.Id}  —  {inv.CustomerName}  —  {inv.VehicleName}", fBold))
                { BackgroundColor = lightBg, Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 7f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                invHdr.AddCell(new PdfPCell(new Phrase($"التاريخ: {dt2:yyyy/MM/dd}", fSmall))
                { BackgroundColor = lightBg, Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT, Padding = 7f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                doc.Add(invHdr);

                string[] subHdrs = { "المنتج", "كراتين", "علب إضافية", "إجمالي الكمية", "سعر البيع", "الإجمالي" };
                float[] subWts = { 2.5f, 0.8f, 0.9f, 1.1f, 1f, 1.1f };

                var sub = new PdfPTable(subHdrs.Length)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 4f };
                sub.SetWidths(subWts);

                foreach (var h in subHdrs)
                    sub.AddCell(new PdfPCell(new Phrase(h, fBoldWhite))
                    { BackgroundColor = darkBlue, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 6f, RunDirection = PdfWriter.RUN_DIRECTION_RTL, BorderColor = midBlue, BorderWidth = 0.5f });

                decimal invTotal = 0m; int ri = 0;
                foreach (var item in inv.Items)
                {
                    var bg2 = ri++ % 2 == 0 ? BaseColor.WHITE : altRowBg;
                    int bpc = item.BoxesPerCarton > 0 ? item.BoxesPerCarton : 24;
                    int cartons = item.Quantity / bpc, extra = item.Quantity % bpc;
                    decimal rowT = item.Quantity * item.SalePrice;
                    invTotal += rowT;

                    sub.AddCell(MakeDataCell(item.ProductName ?? "—", fBold, Element.ALIGN_CENTER, bg2));
                    sub.AddCell(MakeDataCell(ToAr(cartons), fNormal, Element.ALIGN_CENTER, bg2));
                    sub.AddCell(MakeDataCell(extra > 0 ? ToAr(extra) : "—", fNormal, Element.ALIGN_CENTER, bg2));
                    sub.AddCell(MakeDataCell(
                        extra == 0 ? $"{ToAr(cartons)} كرتون" : $"{ToAr(cartons)} كرتون + {ToAr(extra)} علبة",
                        fNormal, Element.ALIGN_CENTER, bg2));
                    sub.AddCell(MakeDataCell(ToAr(item.SalePrice) + " ج", fNormal, Element.ALIGN_CENTER, bg2));
                    sub.AddCell(MakeDataCell(ToAr(rowT) + " ج", fBold, Element.ALIGN_CENTER, bg2));
                }

                sub.AddCell(new PdfPCell(new Phrase($"إجمالي الفاتورة:   {ToAr(invTotal)} ج", fBold))
                {
                    Colspan = subHdrs.Length,
                    BackgroundColor = new BaseColor(219, 234, 254),
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Padding = 7f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BorderColor = new BaseColor(147, 197, 253),
                    BorderWidth = 0.5f
                });
                doc.Add(sub);
            }

            doc.Add(new Paragraph("\n"));
            var grandTable = new PdfPTable(1)
            { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 60, HorizontalAlignment = Element.ALIGN_RIGHT, SpacingBefore = 6f };
            grandTable.AddCell(new PdfPCell(
                new Phrase($"الإجمالي الكلي:   {ToAr(invoices.Sum(i => i.TotalAmount))} جنيه",
                    new Font(bfBold, 13, Font.BOLD, new BaseColor(21, 101, 192))))
            {
                BackgroundColor = new BaseColor(219, 234, 254),
                HorizontalAlignment = Element.ALIGN_RIGHT,
                Padding = 12f,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                Border = Rectangle.BOX,
                BorderColor = new BaseColor(21, 101, 192),
                BorderWidth = 1.5f
            });
            doc.Add(grandTable);
        }

        // ══════════════════════════════════════════════════════
        //  قسم الواردات
        // ══════════════════════════════════════════════════════
        private void AddInboundSection(
            Document doc, List<InboundOrderDto> inbounds,
            Font fBold, Font fNormal, Font fBoldWhite, Font fSmall, Font fSection, BaseFont bfBold)
        {
            var darkBlue = new BaseColor(26, 47, 94);
            var midBlue = new BaseColor(30, 58, 110);
            var altRowBg = new BaseColor(248, 250, 255);

            var hdr3 = new PdfPTable(1)
            { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 6f, SpacingAfter = 6f };
            hdr3.AddCell(new PdfPCell(new Phrase("تفاصيل أوامر الواردات", fSection))
            { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 4f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
            doc.Add(hdr3);

            string[] colHdrs = { "رقم", "التاريخ", "المنتج", "الكمية", "سعر الشراء", "الإجمالي" };
            float[] colWts = { 0.7f, 1.6f, 2f, 1.2f, 1.2f, 1.2f };

            var tbl = new PdfPTable(colHdrs.Length)
            { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 10f };
            tbl.SetWidths(colWts);

            foreach (var h in colHdrs)
                tbl.AddCell(new PdfPCell(new Phrase(h, fBoldWhite))
                { BackgroundColor = darkBlue, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 7f, RunDirection = PdfWriter.RUN_DIRECTION_RTL, BorderColor = midBlue, BorderWidth = 0.5f });

            int rowIndex = 0;
            foreach (var inb in inbounds)
            {
                var bg = rowIndex++ % 2 == 0 ? BaseColor.WHITE : altRowBg;
                DateTime dt = inb.CreatedAt.Kind == DateTimeKind.Utc ? inb.CreatedAt.ToLocalTime()
                    : DateTime.SpecifyKind(inb.CreatedAt, DateTimeKind.Utc).ToLocalTime();
                int bpc = inb.BoxesPerCarton > 0 ? inb.BoxesPerCarton : 24;
                int cartons = inb.Quantity / bpc, extra = inb.Quantity % bpc;

                tbl.AddCell(MakeDataCell(ToAr(inb.Id), fBold, Element.ALIGN_CENTER, bg));
                tbl.AddCell(MakeDataCell(ToArabicNumerals(dt.ToString("yyyy/MM/dd HH:mm", Inv)), fSmall, Element.ALIGN_CENTER, bg));
                tbl.AddCell(MakeDataCell(inb.ProductName ?? "—", fBold, Element.ALIGN_CENTER, bg));
                tbl.AddCell(MakeDataCell(
                    extra == 0 ? $"{ToAr(cartons)} كرتون" : $"{ToAr(cartons)} كرتون + {ToAr(extra)} علبة",
                    fNormal, Element.ALIGN_CENTER, bg));
                tbl.AddCell(MakeDataCell(ToAr(inb.PurchasePrice) + " ج", fNormal, Element.ALIGN_CENTER, bg));
                tbl.AddCell(MakeDataCell(ToAr(inb.TotalValue) + " ج", fBold, Element.ALIGN_CENTER, bg));
            }

            decimal grandTot = inbounds.Sum(i => i.TotalValue);
            tbl.AddCell(new PdfPCell(
                new Phrase($"الإجمالي الكلي:   {ToAr(grandTot)} جنيه",
                    new Font(bfBold, 13, Font.BOLD, new BaseColor(21, 101, 192))))
            {
                Colspan = colHdrs.Length,
                BackgroundColor = new BaseColor(219, 234, 254),
                HorizontalAlignment = Element.ALIGN_RIGHT,
                Padding = 10f,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                Border = Rectangle.BOX,
                BorderColor = new BaseColor(21, 101, 192),
                BorderWidth = 1.5f
            });
            doc.Add(tbl);
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

        // ══════════════════════════════════════════════════════
        //  بطاقة الملخص — خلفية فاتحة مشتقة من لون الـ accent
        // ══════════════════════════════════════════════════════
        private static PdfPCell MakeSummaryCard(
            BaseFont bfBold, BaseFont bfReg,
            string label, string value, string unit,
            BaseColor accentColor)
        {
            // خلفية فاتحة مشتقة من الـ accent
            var cardBg = new BaseColor(
                Math.Min(255, accentColor.R + 200),
                Math.Min(255, accentColor.G + 200),
                Math.Min(255, accentColor.B + 200));

            // بوردر لوني فاتح
            var cardBorder = new BaseColor(
                Math.Min(255, accentColor.R + 130),
                Math.Min(255, accentColor.G + 130),
                Math.Min(255, accentColor.B + 130));

            var inner = new PdfPTable(1) { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100 };

            // شريط علوي عريض ملون
            inner.AddCell(new PdfPCell
            {
                BackgroundColor = accentColor,
                FixedHeight = 7f,
                Border = Rectangle.NO_BORDER,
                Padding = 0f
            });

            // التسمية
            inner.AddCell(new PdfPCell(
                new Phrase("\u200F" + label, new Font(bfReg, 10, Font.NORMAL, new BaseColor(60, 60, 60))))
            {
                Border = Rectangle.NO_BORDER,
                BackgroundColor = cardBg,
                HorizontalAlignment = Element.ALIGN_RIGHT,
                PaddingTop = 10f,
                PaddingRight = 12f,
                PaddingLeft = 12f,
                PaddingBottom = 2f,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL
            });

            // القيمة الكبيرة بلون الـ accent
            string display = value + (string.IsNullOrEmpty(unit) ? "" : "  " + unit);
            inner.AddCell(new PdfPCell(
                new Phrase("\u200F" + display, new Font(bfBold, 17, Font.BOLD, accentColor)))
            {
                Border = Rectangle.NO_BORDER,
                BackgroundColor = cardBg,
                HorizontalAlignment = Element.ALIGN_RIGHT,
                PaddingRight = 12f,
                PaddingLeft = 12f,
                PaddingBottom = 12f,
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

        private static PdfPCell MakeDataCell(string text, Font font, int align, BaseColor bg) =>
            new PdfPCell(new Phrase(text, font))
            {
                HorizontalAlignment = align,
                Padding = 6f,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                BackgroundColor = bg,
                BorderColor = new BaseColor(226, 232, 240),
                BorderWidth = 0.5f
            };
    }

    // ══ لوجو CellEvent ════════════════════════════════════════
    internal class CdrLogoCellEvent : IPdfPCellEvent
    {
        private readonly BaseFont _bf;
        private readonly BaseColor _dark, _mid;
        public CdrLogoCellEvent(BaseFont bf, BaseColor dark, BaseColor mid)
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