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
    public class PdfReportService
    {
        private static readonly string[] ArabicMonths = new[]
        {
            "", "Ì‰«Ì—", "›»—«Ì—", "„«—”", "√»—Ì·", "„«ÌÊ", "ÌÊ‰ÌÊ",
            "ÌÊ·ÌÊ", "√€”ÿ”", "”» „»—", "√ﬂ Ê»—", "‰Ê›„»—", "œÌ”„»—"
        };

        private static string ToArabicNumerals(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
                sb.Append(c >= '0' && c <= '9' ? (char)(c - '0' + '\u0660') : c);
            return sb.ToString();
        }

        private static string ToAr(decimal value, string format = "N2") =>
            ToArabicNumerals(value.ToString(format, CultureInfo.InvariantCulture));

        private static string ToAr(int value) =>
            ToArabicNumerals(value.ToString());

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

        private readonly VehicleService _vehicleService;

        public PdfReportService()
        {
            _vehicleService = new VehicleService();
        }

        public byte[] GenerateVehicleMonthlyReport(
            VehicleDto vehicle, List<DispatchOrderDto> orders, int month, int year)
        {
            if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
            orders = orders ?? new List<DispatchOrderDto>();

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36, 36, 36, 36);
                var writer = PdfWriter.GetInstance(doc, ms);
                doc.Open();

                var bfReg = BaseFont.CreateFont(GetWindowsFont("Cairo-Regular.ttf", "Cairo Regular.ttf", "tahoma.ttf"), BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                var bfBold = BaseFont.CreateFont(GetWindowsFont("Cairo-Bold.ttf", "Cairo Bold.ttf", "tahomabd.ttf"), BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                var fNormal = new Font(bfReg, 11, Font.NORMAL, BaseColor.BLACK);
                var fBold = new Font(bfBold, 11, Font.BOLD, BaseColor.BLACK);
                var fBoldWhite = new Font(bfBold, 11, Font.BOLD, BaseColor.WHITE);
                var fSmall = new Font(bfReg, 9, Font.NORMAL, new BaseColor(100, 100, 100));
                var fSection = new Font(bfBold, 13, Font.BOLD, new BaseColor(26, 47, 94));
                var fGrandTot = new Font(bfBold, 13, Font.BOLD, new BaseColor(21, 101, 192));

                var darkBlue = new BaseColor(26, 47, 94);
                var midBlue = new BaseColor(21, 101, 192);
                var lightBg = new BaseColor(239, 246, 255);
                var infoBord = new BaseColor(190, 210, 240);
                var borderC = new BaseColor(180, 180, 180);
                var altRowBg = new BaseColor(248, 250, 255);

                string monthName = (month >= 1 && month <= 12) ? ArabicMonths[month] : month.ToString();

                // ?? 1. ·ÊÃÊ ??????????????????????????????????????
                var logoTbl = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 4f };
                var circleCell = new PdfPCell
                { Border = Rectangle.NO_BORDER, FixedHeight = 80f, HorizontalAlignment = Element.ALIGN_CENTER };
                circleCell.CellEvent = new VehicleLogoCellEvent(bfBold, darkBlue, midBlue);
                logoTbl.AddCell(circleCell);
                logoTbl.AddCell(new PdfPCell(
                    new Phrase("‘—ﬂ… »’Ê’ ·· Ê“Ì⁄", new Font(bfBold, 11, Font.BOLD, darkBlue)))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, PaddingBottom = 2f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                doc.Add(logoTbl);
                doc.Add(DrawRule(darkBlue, midBlue));

                // ?? 2. ⁄‰Ê«‰ «· ﬁ—Ì— ??????????????????????????????
                var titleTbl = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 8f, SpacingAfter = 8f };
                titleTbl.AddCell(new PdfPCell(
                    new Phrase($"\u200F«· ﬁ—Ì— «·‘Â—Ì ··”Ì«—…: {vehicle.Name}  .....",
                        new Font(bfBold, 15, Font.BOLD, BaseColor.BLACK)))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                doc.Add(titleTbl);

                // ?? 3. »Ì«‰«  «·”Ì«—… ??????????????????????????????
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

                infoTbl.AddCell(ValCell(vehicle.Name ?? "ó", true)); infoTbl.AddCell(LblCell("«”„ «·”Ì«—…"));
                infoTbl.AddCell(ValCell(vehicle.RepName ?? "ó", true)); infoTbl.AddCell(LblCell("«·„‰œÊ»"));
                infoTbl.AddCell(ValCell($"{monthName}  {ToArabicNumerals(year.ToString())}"));
                infoTbl.AddCell(LblCell("«·‘Â— / «·”‰…"));
                infoTbl.AddCell(ValCell(ToArabicNumerals(DateTime.Now.ToString("yyyy/MM/dd  HH:mm", CultureInfo.InvariantCulture))));
                infoTbl.AddCell(LblCell(" «—ÌŒ «· ﬁ—Ì—"));
                doc.Add(infoTbl);
                doc.Add(DrawRule(darkBlue, midBlue));

                // ?? 4. Ã·» «·ﬂ„Ì«  ????????????????????????????????
                var allOriginalQties = new Dictionary<int, Dictionary<int, int>>();
                var allSoldQties = new Dictionary<int, Dictionary<int, int>>();
                var allReturnedQties = new Dictionary<int, Dictionary<int, int>>();

                foreach (var ord in orders)
                {
                    try { allOriginalQties[ord.Id] = _vehicleService.GetOriginalQuantitiesByDispatch(ord.Id); } catch { allOriginalQties[ord.Id] = new Dictionary<int, int>(); }
                    try { allSoldQties[ord.Id] = _vehicleService.GetSoldQuantitiesByDispatch(ord.Id); } catch { allSoldQties[ord.Id] = new Dictionary<int, int>(); }
                    try { allReturnedQties[ord.Id] = _vehicleService.GetReturnedQuantitiesByDispatch(ord.Id); } catch { allReturnedQties[ord.Id] = new Dictionary<int, int>(); }
                }

                // ?? 5. »ÿ«ﬁ«  «·„·Œ’ ??????????????????????????????
                int totalOrders = orders.Count;
                int totalProducts = 0;
                decimal totalRevenue = 0m;

                foreach (var ord in orders)
                {
                    var origQties = allOriginalQties.ContainsKey(ord.Id) ? allOriginalQties[ord.Id] : new Dictionary<int, int>();
                    foreach (var it in ord.Items ?? new List<DispatchOrderItemDto>())
                    {
                        int origBoxes = origQties.ContainsKey(it.ProductId) ? origQties[it.ProductId] : it.Quantity;
                        totalProducts += origBoxes;
                        totalRevenue += (decimal)origBoxes * it.SalePrice;
                    }
                }

                var st = new PdfPTable(3)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 10f, SpacingAfter = 16f };
                st.SetWidths(new float[] { 1f, 1f, 1f });
                st.AddCell(MakeSummaryCard(bfBold, bfReg, "⁄œœ √Ê«„— «·’—›", ToAr(totalOrders), "", new BaseColor(26, 47, 94)));
                st.AddCell(MakeSummaryCard(bfBold, bfReg, "≈Ã„«·Ì «·ﬂ„Ì…", ToArabicNumerals(totalProducts.ToString("N0")), "⁄·»…", new BaseColor(21, 101, 192)));
                st.AddCell(MakeSummaryCard(bfBold, bfReg, "≈Ã„«·Ì «·≈Ì—«œ« ", ToAr(totalRevenue), "Ã‰ÌÂ", new BaseColor(5, 150, 105)));
                doc.Add(st);

                // ?? 6.  ›«’Ì· √Ê«„— «·’—› ????????????????????????
                var hdrSec = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 4f, SpacingAfter = 6f };
                hdrSec.AddCell(new PdfPCell(new Phrase(" ›«’Ì· √Ê«„— «·’—›", fSection))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 4f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                doc.Add(hdrSec);

                string[] colHdrs = { "«·„‰ Ã", "ﬂ—« Ì‰", "⁄·» ≈÷«›Ì…", "≈Ã„«·Ì «·ﬂ„Ì…", "„”ÕÊ» »›Ê« Ì—", "„— Ã⁄", "”⁄— «·»Ì⁄", "«·≈Ã„«·Ì" };
                float[] colWts = { 2.2f, 0.7f, 0.8f, 1.0f, 1.1f, 0.9f, 0.9f, 1.0f };
                var midBlue2 = new BaseColor(30, 58, 110);

                foreach (var ord in orders.OrderBy(o => o.CreatedAt))
                {
                    var localDate = ord.CreatedAt.ToLocalTime();
                    string ordDate = ToArabicNumerals(localDate.ToString("yyyy/MM/dd   HH:mm", CultureInfo.InvariantCulture));

                    var origQties = allOriginalQties.ContainsKey(ord.Id) ? allOriginalQties[ord.Id] : new Dictionary<int, int>();
                    var soldQties = allSoldQties.ContainsKey(ord.Id) ? allSoldQties[ord.Id] : new Dictionary<int, int>();
                    var returnedQties = allReturnedQties.ContainsKey(ord.Id) ? allReturnedQties[ord.Id] : new Dictionary<int, int>();

                    // ÂÌœ— «·√„—
                    var ordHdr = new PdfPTable(2)
                    { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 10f, SpacingAfter = 2f };
                    ordHdr.SetWidths(new float[] { 1.2f, 1f });
                    ordHdr.AddCell(new PdfPCell(new Phrase($"√„— ’—› —ﬁ„ #{ToAr(ord.Id)}", fBold))
                    { BackgroundColor = lightBg, Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 7f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                    ordHdr.AddCell(new PdfPCell(new Phrase($"«· «—ÌŒ: {ordDate}", fSmall))
                    { BackgroundColor = lightBg, Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT, Padding = 7f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                    doc.Add(ordHdr);

                    // ÃœÊ· «·„‰ Ã« 
                    var tbl = new PdfPTable(colHdrs.Length)
                    { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 4f };
                    tbl.SetWidths(colWts);

                    foreach (var h in colHdrs)
                        tbl.AddCell(new PdfPCell(new Phrase(h, fBoldWhite))
                        { BackgroundColor = darkBlue, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 7f, RunDirection = PdfWriter.RUN_DIRECTION_RTL, BorderColor = midBlue2, BorderWidth = 0.5f });

                    decimal orderTotal = 0m; int rowIndex = 0;
                    foreach (var it in ord.Items ?? new List<DispatchOrderItemDto>())
                    {
                        int totalBoxes = origQties.ContainsKey(it.ProductId) ? origQties[it.ProductId] : it.Quantity;
                        int bpc = it.BoxesPerCarton > 0 ? it.BoxesPerCarton : 1;
                        int cartons = totalBoxes / bpc;
                        int extra = totalBoxes % bpc;
                        int sold = soldQties.ContainsKey(it.ProductId) ? soldQties[it.ProductId] : 0;
                        int returned = returnedQties.ContainsKey(it.ProductId) ? returnedQties[it.ProductId] : 0;
                        decimal rowTot = (decimal)totalBoxes * it.SalePrice;
                        orderTotal += rowTot;

                        var bg = rowIndex++ % 2 == 0 ? BaseColor.WHITE : altRowBg;

                        var fSold = new Font(bfBold, 10, Font.BOLD, new BaseColor(180, 83, 9));
                        var fReturned = new Font(bfBold, 10, Font.BOLD, new BaseColor(109, 40, 217));

                        tbl.AddCell(MakeDataCell(it.ProductName ?? $"#{ToAr(it.ProductId)}", fBold, Element.ALIGN_RIGHT, bg));
                        tbl.AddCell(MakeDataCell(cartons > 0 ? ToAr(cartons) : "ó", fNormal, Element.ALIGN_CENTER, bg));
                        tbl.AddCell(MakeDataCell(extra > 0 ? ToAr(extra) : "ó", fNormal, Element.ALIGN_CENTER, bg));
                        tbl.AddCell(MakeDataCell($"{ToAr(totalBoxes)} ⁄·»…", fNormal, Element.ALIGN_CENTER, bg));
                        tbl.AddCell(MakeDataCell(sold > 0 ? $"{ToAr(sold)} ⁄·»…" : "ó", fSold, Element.ALIGN_CENTER, bg));
                        tbl.AddCell(MakeDataCell(returned > 0 ? $"{ToAr(returned)} ⁄·»…" : "ó", fReturned, Element.ALIGN_CENTER, bg));
                        tbl.AddCell(MakeDataCell(it.SalePrice > 0 ? ToAr(it.SalePrice) + " Ã" : "ó", fNormal, Element.ALIGN_CENTER, bg));
                        tbl.AddCell(MakeDataCell(ToAr(rowTot) + " Ã", fBold, Element.ALIGN_CENTER, bg));
                    }

                    tbl.AddCell(new PdfPCell(new Phrase($"≈Ã„«·Ì «·√„—:   {ToAr(orderTotal)} Ã", fBold))
                    {
                        Colspan = colHdrs.Length,
                        BackgroundColor = new BaseColor(219, 234, 254),
                        HorizontalAlignment = Element.ALIGN_RIGHT,
                        Padding = 7f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BorderColor = new BaseColor(147, 197, 253),
                        BorderWidth = 0.5f
                    });
                    doc.Add(tbl);
                }

                // ?? 7. «·≈Ã„«·Ì «·ﬂ·Ì ?????????????????????????????
                doc.Add(new Paragraph("\n"));
                var grandTable = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 60, HorizontalAlignment = Element.ALIGN_RIGHT, SpacingBefore = 6f };
                grandTable.AddCell(new PdfPCell(
                    new Phrase($"«·≈Ã„«·Ì «·ﬂ·Ì ··‘Â—:   {ToAr(totalRevenue)} Ã‰ÌÂ", fGrandTot))
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

                // ?? 8. ›Ê — ????????????????????????????????????????
                doc.Add(DrawRule(borderC, borderC));
                var footTbl = new PdfPTable(2)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 4f };
                footTbl.SetWidths(new float[] { 1f, 1f });
                footTbl.AddCell(new PdfPCell(new Phrase(
                    " «—ÌŒ «·≈‰‘«¡: " + ToArabicNumerals(DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss", CultureInfo.InvariantCulture)),
                    new Font(bfReg, 8, Font.NORMAL, new BaseColor(150, 150, 150))))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                footTbl.AddCell(new PdfPCell(
                    new Phrase("1  |  Page", new Font(bfReg, 8, Font.NORMAL, new BaseColor(150, 150, 150))))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
                doc.Add(footTbl);

                // ?? 9. ⁄·«„… „«∆Ì… ????????????????????????????????
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
                cbWM.ShowText("»’Ê’");
                cbWM.EndText();
                cbWM.RestoreState();

                doc.Close();
                return ms.ToArray();
            }
        }

        // ??????????????????????????????????????????????????????
        //  HELPERS
        // ??????????????????????????????????????????????????????
        private static PdfPTable DrawRule(BaseColor c1, BaseColor c2)
        {
            var t = new PdfPTable(1) { WidthPercentage = 100, SpacingBefore = 2f, SpacingAfter = 2f };
            t.AddCell(new PdfPCell { Border = Rectangle.BOTTOM_BORDER, BorderColor = c1, BorderWidth = 1.5f, FixedHeight = 3f });
            t.AddCell(new PdfPCell { Border = Rectangle.BOTTOM_BORDER, BorderColor = c2, BorderWidth = 0.5f, FixedHeight = 2f });
            return t;
        }

        // »ÿ«ﬁ… «·„·Œ’ ó Œ·›Ì… ›« Õ… „‘ ﬁ… „‰ «·‹ accent
        private static PdfPCell MakeSummaryCard(
            BaseFont bfBold, BaseFont bfReg,
            string label, string value, string unit,
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

            // ‘—Ìÿ ⁄·ÊÌ „·Ê‰
            inner.AddCell(new PdfPCell
            { BackgroundColor = accentColor, FixedHeight = 7f, Border = Rectangle.NO_BORDER, Padding = 0f });

            // «· ”„Ì…
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

            // «·ﬁÌ„…
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

    // ?? ·ÊÃÊ CellEvent ????????????????????????????????????????
    internal class VehicleLogoCellEvent : IPdfPCellEvent
    {
        private readonly BaseFont _bf;
        private readonly BaseColor _dark, _mid;
        public VehicleLogoCellEvent(BaseFont bf, BaseColor dark, BaseColor mid)
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
            ct.AddText(new Phrase("»’Ê’", new Font(_bf, 16f, Font.BOLD, BaseColor.WHITE)));
            ct.Alignment = Element.ALIGN_CENTER;
            ct.Go();
        }
    }
}