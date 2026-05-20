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
    public class SalesInvoicePdfService
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

        public byte[] GenerateInvoicePdf(SalesInvoiceDto inv)
        {
            if (inv == null) throw new ArgumentNullException(nameof(inv));

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36, 36, 36, 36);
                var writer = PdfWriter.GetInstance(doc, ms);
                doc.Open();

                var bfR = BaseFont.CreateFont(
                    GetWindowsFont("Cairo-Regular.ttf", "Cairo-VariableFont_slnt,wght.ttf", "arial.ttf"),
                    BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                var bfB = BaseFont.CreateFont(
                    GetWindowsFont("Cairo-Bold.ttf", "Cairo-VariableFont_slnt,wght.ttf", "arialbd.ttf"),
                    BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                var fSm = new Font(bfR, 9, Font.NORMAL, new BaseColor(80, 80, 80));
                var fSmB = new Font(bfB, 9, Font.BOLD, new BaseColor(26, 47, 94));
                var fWh = new Font(bfB, 10, Font.BOLD, BaseColor.WHITE);

                var darkBlue = new BaseColor(26, 47, 94);
                var midBlue = new BaseColor(21, 101, 192);
                var lightBg = new BaseColor(239, 246, 255);
                var altRow = new BaseColor(248, 250, 255);
                var borderC = new BaseColor(180, 180, 180);
                var infoBord = new BaseColor(190, 210, 240);

                DateTime dt = inv.CreatedAt.Kind == DateTimeKind.Utc
                    ? inv.CreatedAt.ToLocalTime() : inv.CreatedAt;
                string dateStr = Ar(dt.ToString("yyyy/MM/dd", Inv));
                string timeStr = Ar(dt.ToString("HH:mm", Inv));
                string payTypeAr = inv.PaymentType == "Cash" ? "كاش" : "آجل / تقسيط";
                bool done = inv.Status == "Completed";
                string statusAr = done ? "مكتملة" : "معلقة";
                decimal total = inv.Items?.Sum(i => i.Quantity * i.SalePrice) ?? inv.TotalAmount;
                decimal remain = total - inv.PaidAmount;
                var items = inv.Items ?? new List<SalesInvoiceItemDto>();

                // ══ 1. لوجو ══════════════════════════════════
                var logoTbl = new PdfPTable(1)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 4f };
                var circleCell = new PdfPCell
                { Border = Rectangle.NO_BORDER, FixedHeight = 80f, HorizontalAlignment = Element.ALIGN_CENTER };
                circleCell.CellEvent = new LogoCellEvent(bfB, darkBlue, midBlue);
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

                // ══ 2. العنوان ═══════════════════════════════
                var titleTbl = new PdfPTable(1)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 100,
                    SpacingBefore = 8f,
                    SpacingAfter = 8f
                };
                titleTbl.AddCell(new PdfPCell(
                    new Phrase($"\u200Fفاتورة بيع رقم  {Ar(inv.Id)}  .....",
                        new Font(bfB, 15, Font.BOLD, BaseColor.BLACK)))
                {
                    Border = Rectangle.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL
                });
                doc.Add(titleTbl);

                // ══ 3. بيانات الرأس ══════════════════════════
                var infoTbl = new PdfPTable(2)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_LTR,
                    WidthPercentage = 100,
                    SpacingBefore = 6f,
                    SpacingAfter = 10f
                };
                // LTR: col0=يسار (value واسع) | col1=يمين (label ضيق داكن)
                infoTbl.SetWidths(new float[] { 3.5f, 1.4f });

                // ══ Label cell — فونت أكبر (11) ══════════════
                PdfPCell LblCell(string text) => new PdfPCell(
                    new Phrase(text, new Font(bfB, 11, Font.BOLD, BaseColor.WHITE)))   // ← 9 → 11
                {
                    BackgroundColor = darkBlue,
                    HorizontalAlignment = Element.ALIGN_CENTER,   // وسط
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    PaddingTop = 7f,
                    PaddingBottom = 7f,
                    PaddingLeft = 4f,
                    PaddingRight = 4f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BorderColor = infoBord,
                    BorderWidth = 0.5f
                };

                // ══ Value cell — نص في المنتصف ════════════════
                PdfPCell ValCell(string text, bool bold = false) => new PdfPCell(
                    new Phrase("\u200F" + text,
                        bold ? new Font(bfB, 11, Font.BOLD, darkBlue)
                             : new Font(bfR, 11, Font.NORMAL, new BaseColor(30, 30, 30))))
                {
                    BackgroundColor = bold ? lightBg : new BaseColor(252, 253, 255),
                    HorizontalAlignment = Element.ALIGN_CENTER,   // ← RIGHT → CENTER
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    PaddingTop = 7f,
                    PaddingBottom = 7f,
                    PaddingRight = 12f,
                    PaddingLeft = 4f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BorderColor = infoBord,
                    BorderWidth = 0.5f
                };

                // LTR: value أولاً (يسار) ثم label (يمين)
                infoTbl.AddCell(ValCell(inv.CustomerName ?? "—", true)); infoTbl.AddCell(LblCell("اسم العميل"));
                infoTbl.AddCell(ValCell(inv.VehicleName ?? "—", true)); infoTbl.AddCell(LblCell("السيارة"));
                infoTbl.AddCell(ValCell(Ar(inv.Id), true)); infoTbl.AddCell(LblCell("رقم الفاتورة"));
                infoTbl.AddCell(ValCell(payTypeAr)); infoTbl.AddCell(LblCell("طريقة السداد"));
                infoTbl.AddCell(ValCell($"{dateStr}   {timeStr}")); infoTbl.AddCell(LblCell("تحريراً في"));

                // خلية الحالة — نفس التعديل (CENTER)
                infoTbl.AddCell(new PdfPCell(
                    new Phrase("\u200F" + statusAr,
                        new Font(bfB, 11, Font.BOLD,
                            done ? new BaseColor(5, 150, 105) : new BaseColor(180, 90, 0))))
                {
                    BackgroundColor = done ? new BaseColor(236, 253, 245) : new BaseColor(255, 248, 230),
                    HorizontalAlignment = Element.ALIGN_CENTER,   // ← CENTER
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    PaddingTop = 7f,
                    PaddingBottom = 7f,
                    PaddingRight = 12f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BorderColor = done ? new BaseColor(100, 200, 150) : new BaseColor(220, 160, 60),
                    BorderWidth = 0.5f
                });
                infoTbl.AddCell(LblCell("الحالة"));

                doc.Add(infoTbl);
                doc.Add(DrawRule(darkBlue, midBlue));

                // ══ 4. جدول المنتجات — 7 أعمدة ══════════════
                float[] colW = { 0.35f, 2.6f, 1.0f, 1.0f, 0.6f, 1.3f, 0.6f };
                var hBg = darkBlue;
                var subBg2 = new BaseColor(205, 220, 250);

                var fRowNo = new Font(bfR, 9, Font.NORMAL, new BaseColor(80, 80, 80));
                var fProd = new Font(bfB, 13, Font.BOLD, BaseColor.BLACK);  // ← كبّرنا الفونت
                var fRowN = new Font(bfR, 10, Font.NORMAL, BaseColor.BLACK);
                var fRowTot = new Font(bfB, 11, Font.BOLD, new BaseColor(5, 100, 60));
                var fSumVal = new Font(bfB, 11, Font.BOLD, midBlue);

                // ── هيدر الجدول ──────────────────────────────
                var hdrTbl = new PdfPTable(7)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 100,
                    SpacingBefore = 4f,
                    SpacingAfter = 0f
                };
                hdrTbl.SetWidths(colW);

                hdrTbl.AddCell(H2("مسلسل", hBg, bfB));
                hdrTbl.AddCell(H2("البيـــان", hBg, bfB));
                hdrTbl.AddCell(H2("الكمية", hBg, bfB));
                hdrTbl.AddCell(new PdfPCell(new Phrase("سعر الوحدة", new Font(bfB, 10, Font.BOLD, BaseColor.WHITE)))
                {
                    Colspan = 2,
                    BackgroundColor = hBg,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 7f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BorderColor = borderC,
                    BorderWidth = 0.5f
                });
                hdrTbl.AddCell(new PdfPCell(new Phrase("الإجمالي", new Font(bfB, 10, Font.BOLD, BaseColor.WHITE)))
                {
                    Colspan = 2,
                    BackgroundColor = hBg,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 7f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BorderColor = borderC,
                    BorderWidth = 0.5f
                });

                hdrTbl.AddCell(SH("", subBg2, bfR, borderC));
                hdrTbl.AddCell(SH("", subBg2, bfR, borderC));
                hdrTbl.AddCell(SH("علبة", subBg2, bfR, borderC));
                hdrTbl.AddCell(SH("جنيه", subBg2, bfR, borderC));
                hdrTbl.AddCell(SH("قرش", subBg2, bfR, borderC));
                hdrTbl.AddCell(SH("جنيه", subBg2, bfR, borderC));
                hdrTbl.AddCell(SH("قرش", subBg2, bfR, borderC));
                doc.Add(hdrTbl);

                // ── جدول البيانات ─────────────────────────────
                var tbl = new PdfPTable(7)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 100,
                    SpacingAfter = 0f,
                    SpacingBefore = 0f
                };
                tbl.SetWidths(colW);

                int minR = 12;
                for (int r = 0; r < Math.Max(items.Count, minR); r++)
                {
                    var bg = r % 2 == 0 ? BaseColor.WHITE : altRow;
                    if (r < items.Count)
                    {
                        var it = items[r];
                        decimal rt = it.Quantity * it.SalePrice;
                        int bpc = it.BoxesPerCarton > 0 ? it.BoxesPerCarton : 1;
                        int cartons = it.Quantity / bpc;
                        int boxes = it.Quantity % bpc;
                        string qty = cartons > 0 && boxes > 0
                            ? $"{Ar(cartons)} ك + {Ar(boxes)} ع"
                            : cartons > 0 ? $"{Ar(cartons)} كرتونة"
                                          : $"{Ar(boxes)} علبة";

                        long spJn = (long)Math.Floor(it.SalePrice);
                        int spQr = (int)Math.Round((it.SalePrice - spJn) * 100);
                        long rtJn = (long)Math.Floor(rt);
                        int rtQr = (int)Math.Round((rt - rtJn) * 100);

                        tbl.AddCell(DC(Ar(r + 1), fRowNo, Element.ALIGN_CENTER, bg, borderC, 6f));
                        tbl.AddCell(DC(it.ProductName ?? "—", fProd, Element.ALIGN_CENTER, bg, borderC, 6f));  // ← RIGHT → CENTER
                        tbl.AddCell(DC(qty, fRowN, Element.ALIGN_CENTER, bg, borderC, 6f));
                        tbl.AddCell(DC(Ar((decimal)spJn, "N0"), fRowN, Element.ALIGN_CENTER, bg, borderC, 6f));
                        tbl.AddCell(DC(spQr > 0 ? Ar(spQr) : "—", fRowN, Element.ALIGN_CENTER, bg, borderC, 6f));
                        tbl.AddCell(DC(Ar((decimal)rtJn, "N0"), fRowTot, Element.ALIGN_CENTER, bg, borderC, 6f));
                        tbl.AddCell(DC(rtQr > 0 ? Ar(rtQr) : "—", fRowN, Element.ALIGN_CENTER, bg, borderC, 6f));
                    }
                    else
                    {
                        for (int c = 0; c < 7; c++)
                            tbl.AddCell(DC("", fRowN, Element.ALIGN_CENTER, bg, borderC, 13f));
                    }
                }

                // ── إجمالي المبيعات ───────────────────────────
                long totJn = (long)Math.Floor(total);
                int totQr = (int)Math.Round((total - totJn) * 100);

                tbl.AddCell(new PdfPCell(
                    new Phrase("\u200Fإجمالي المبيعات", new Font(bfB, 10, Font.BOLD, darkBlue)))
                {
                    Colspan = 5,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Padding = 7f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BackgroundColor = lightBg,
                    BorderColor = borderC,
                    BorderWidth = 0.5f
                });
                tbl.AddCell(DC(Ar((decimal)totJn, "N0"), fSumVal, Element.ALIGN_CENTER, lightBg, borderC, 7f));
                tbl.AddCell(DC(totQr > 0 ? Ar(totQr) : "—",
                    new Font(bfR, 9, Font.NORMAL, midBlue), Element.ALIGN_CENTER, lightBg, borderC, 7f));

                // ── ضريبة ─────────────────────────────────────
                tbl.AddCell(new PdfPCell(
                    new Phrase("\u200F+ %١٠  ضريبة المبيعات",
                        new Font(bfR, 9, Font.NORMAL, new BaseColor(100, 100, 100))))
                {
                    Colspan = 5,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Padding = 6f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BackgroundColor = altRow,
                    BorderColor = borderC,
                    BorderWidth = 0.5f
                });
                tbl.AddCell(DC("—", fRowN, Element.ALIGN_CENTER, altRow, borderC, 6f));
                tbl.AddCell(DC("—", fRowN, Element.ALIGN_CENTER, altRow, borderC, 6f));

                // ── الإجمالي فقط وقدره ────────────────────────
                string words = GetTotalInWords(total);
                var wBg = new BaseColor(219, 234, 254);
                tbl.AddCell(new PdfPCell(
                    new Phrase($"\u200Fالإجمالي فقط وقدره  ( {words} )",
                        new Font(bfB, 9, Font.BOLD, darkBlue)))
                {
                    Colspan = 4,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    Padding = 9f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BackgroundColor = wBg,
                    BorderColor = midBlue,
                    BorderWidth = 1f
                });
                tbl.AddCell(new PdfPCell(
                    new Phrase("( لا غير. )", new Font(bfR, 9, Font.NORMAL, new BaseColor(80, 80, 80))))
                {
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 9f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BackgroundColor = wBg,
                    BorderColor = midBlue,
                    BorderWidth = 1f
                });
                tbl.AddCell(DC(Ar((decimal)totJn, "N0"),
                    new Font(bfB, 12, Font.BOLD, midBlue), Element.ALIGN_CENTER, wBg, midBlue, 9f));
                tbl.AddCell(DC(totQr > 0 ? Ar(totQr) : "—",
                    new Font(bfR, 9, Font.NORMAL, midBlue), Element.ALIGN_CENTER, wBg, midBlue, 9f));

                doc.Add(tbl);

                // ══ 5. ملخص الدفع ════════════════════════════
                var payTbl = new PdfPTable(3)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 55,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    SpacingBefore = 8f,
                    SpacingAfter = 6f
                };
                payTbl.SetWidths(new float[] { 1.4f, 1f, 1f });
                payTbl.AddCell(new PdfPCell(new Phrase("ملخص الدفع", fWh))
                {
                    Colspan = 3,
                    BackgroundColor = darkBlue,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 7f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    BorderColor = borderC,
                    BorderWidth = 0.5f
                });

                string[] pLbls = { "نوع الدفع", "المدفوع", "المتبقي" };
                string[] pVals = { payTypeAr, Ar(inv.PaidAmount) + " ج", Ar(remain) + " ج" };
                var pClrs = new[] {
                    midBlue,
                    new BaseColor(5,  150, 105),
                    remain > 0 ? new BaseColor(220, 38, 38) : new BaseColor(5, 150, 105)
                };

                for (int i = 0; i < 3; i++)
                    payTbl.AddCell(new PdfPCell(new Phrase(pLbls[i], fSmB))
                    {
                        BackgroundColor = lightBg,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BorderColor = borderC,
                        BorderWidth = 0.5f
                    });
                for (int i = 0; i < 3; i++)
                    payTbl.AddCell(new PdfPCell(
                        new Phrase(pVals[i], new Font(bfB, 11, Font.BOLD, pClrs[i])))
                    {
                        BackgroundColor = BaseColor.WHITE,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 7f,
                        RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                        BorderColor = borderC,
                        BorderWidth = 0.5f
                    });
                doc.Add(payTbl);

                // الحالة
                var stTbl = new PdfPTable(1)
                {
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    WidthPercentage = 42,
                    HorizontalAlignment = Element.ALIGN_RIGHT,
                    SpacingAfter = 12f
                };
                stTbl.AddCell(new PdfPCell(
                    new Phrase(done ? "الفاتورة مكتملة السداد" : "الفاتورة معلقة — يوجد متبقي",
                        new Font(bfB, 10, Font.BOLD,
                            done ? new BaseColor(5, 150, 105) : new BaseColor(217, 119, 6))))
                {
                    BackgroundColor = done ? new BaseColor(236, 253, 245) : new BaseColor(255, 251, 235),
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 8f,
                    RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                    Border = Rectangle.BOX,
                    BorderColor = done ? new BaseColor(167, 243, 208) : new BaseColor(253, 230, 138),
                    BorderWidth = 1f
                });
                doc.Add(stTbl);

                // ══ 6. التوقيع ═══════════════════════════════
                var sigTbl = new PdfPTable(2)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingAfter = 12f };
                sigTbl.SetWidths(new float[] { 1f, 1f });
                sigTbl.AddCell(new PdfPCell(new Phrase("توقيع العميل", fSmB))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 4f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                sigTbl.AddCell(new PdfPCell(new Phrase("توقيع المندوب", fSmB))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 4f, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                sigTbl.AddCell(new PdfPCell
                { Border = Rectangle.BOTTOM_BORDER, BorderColor = darkBlue, BorderWidth = 1f, FixedHeight = 30f });
                sigTbl.AddCell(new PdfPCell
                { Border = Rectangle.BOTTOM_BORDER, BorderColor = darkBlue, BorderWidth = 1f, FixedHeight = 30f });
                doc.Add(sigTbl);

                // ══ 7. فوتر ══════════════════════════════════
                doc.Add(DrawRule(borderC, borderC));
                var footTbl = new PdfPTable(2)
                { RunDirection = PdfWriter.RUN_DIRECTION_RTL, WidthPercentage = 100, SpacingBefore = 4f };
                footTbl.SetWidths(new float[] { 1f, 1f });
                footTbl.AddCell(new PdfPCell(new Phrase(
                    "تاريخ الطباعة: " + Ar(DateTime.Now.ToString("yyyy/MM/dd  HH:mm", Inv)),
                    new Font(bfR, 8, Font.NORMAL, new BaseColor(150, 150, 150))))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT, RunDirection = PdfWriter.RUN_DIRECTION_RTL });
                footTbl.AddCell(new PdfPCell(
                    new Phrase("1  |  Page", new Font(bfR, 8, Font.NORMAL, new BaseColor(150, 150, 150))))
                { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
                doc.Add(footTbl);

                // ══ 8. علامة مائية ═══════════════════════════
                var cbWM = writer.DirectContentUnder;
                cbWM.SaveState();
                var gs = new PdfGState { FillOpacity = 0.05f };
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

        // ══ خط فاصل ════════════════════════════════════════
        private static PdfPTable DrawRule(BaseColor c1, BaseColor c2)
        {
            var t = new PdfPTable(1) { WidthPercentage = 100, SpacingBefore = 2f, SpacingAfter = 2f };
            t.AddCell(new PdfPCell { Border = Rectangle.BOTTOM_BORDER, BorderColor = c1, BorderWidth = 1.5f, FixedHeight = 3f });
            t.AddCell(new PdfPCell { Border = Rectangle.BOTTOM_BORDER, BorderColor = c2, BorderWidth = 0.5f, FixedHeight = 2f });
            return t;
        }

        private static PdfPCell H2(string text, BaseColor bg, BaseFont bf) =>
            new PdfPCell(new Phrase(text, new Font(bf, 10, Font.BOLD, BaseColor.WHITE)))
            {
                BackgroundColor = bg,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 7f,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                BorderColor = new BaseColor(180, 180, 180),
                BorderWidth = 0.5f
            };

        private static PdfPCell SH(string text, BaseColor bg, BaseFont bf, BaseColor border) =>
            new PdfPCell(new Phrase(text, new Font(bf, 9, Font.NORMAL, new BaseColor(40, 40, 40))))
            {
                BackgroundColor = bg,
                HorizontalAlignment = Element.ALIGN_CENTER,
                Padding = 4f,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                BorderColor = border,
                BorderWidth = 0.5f
            };

        private static PdfPCell DC(string text, Font font, int align,
            BaseColor bg, BaseColor border, float pad = 6f) =>
            new PdfPCell(new Phrase(text, font))
            {
                HorizontalAlignment = align,
                Padding = pad,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                BackgroundColor = bg,
                BorderColor = border,
                BorderWidth = 0.5f
            };

        private static string GetTotalInWords(decimal amount)
        {
            long p = (long)Math.Floor(amount);
            int q = (int)Math.Round((amount - p) * 100);
            string[] ones = {"","واحد","اثنان","ثلاثة","أربعة","خمسة","ستة","سبعة","ثمانية","تسعة",
                "عشرة","أحد عشر","اثنا عشر","ثلاثة عشر","أربعة عشر","خمسة عشر","ستة عشر",
                "سبعة عشر","ثمانية عشر","تسعة عشر"};
            string[] tens = { "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون" };
            string[] hund = { "", "مائة", "مئتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة" };
            if (p == 0) return "صفر جنيه";
            if (p > 999999) return Ar(p.ToString()) + " جنيه";
            string res = ""; int th = (int)(p / 1000), rm = (int)(p % 1000);
            if (th > 0) { res = th == 1 ? "ألف" : th == 2 ? "ألفان" : th < 11 ? ones[th] + " آلاف" : Ar(th.ToString()) + " ألف"; if (rm > 0) res += " و"; }
            if (rm > 0) { int h = rm / 100, t2 = rm % 100; if (h > 0) res += hund[h]; if (t2 > 0) { if (h > 0) res += " و"; res += t2 < 20 ? ones[t2] : (t2 % 10 > 0 ? ones[t2 % 10] + " و" : "") + tens[t2 / 10]; } }
            res += " جنيه";
            if (q > 0) res += " و" + (q < 20 ? ones[q] : ones[q % 10] + " و" + tens[q / 10]) + " قرش";
            return res;
        }
    }

    internal class LogoCellEvent : IPdfPCellEvent
    {
        private readonly BaseFont _bf;
        private readonly BaseColor _dark, _mid;
        public LogoCellEvent(BaseFont bf, BaseColor dark, BaseColor mid)
        { _bf = bf; _dark = dark; _mid = mid; }

        public void CellLayout(PdfPCell cell, Rectangle pos, PdfContentByte[] canvases)
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