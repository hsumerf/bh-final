using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace BrailleUrdu
{
    public static class DocumentSerializer
    {
        // ── Save ─────────────────────────────────────────────────────────────

        public static void Save(string path, CanvasPanel canvas)
        {
            var xd   = new XmlDocument();
            var root = xd.CreateElement("BhDoc");
            root.SetAttribute("version",  "1");
            root.SetAttribute("widthMm",  Fmt(DocumentPage.WIDTH_MM));
            root.SetAttribute("heightMm", Fmt(DocumentPage.HEIGHT_MM));
            xd.AppendChild(root);

            for (int i = 0; i < Document.Pages.Count; i++)
                root.AppendChild(BuildPageElement(xd, Document.Pages[i], "regular", canvas));

            root.AppendChild(BuildPageElement(xd, Document.MasterOdd,  "masterOdd",  canvas));
            root.AppendChild(BuildPageElement(xd, Document.MasterEven, "masterEven", canvas));

            xd.Save(path);
        }

        private static XmlElement BuildPageElement(XmlDocument xd, DocumentPage page,
                                                    string id, CanvasPanel canvas)
        {
            var pe = xd.CreateElement("Page");
            pe.SetAttribute("id",           id);
            pe.SetAttribute("marginLeft",   Fmt(page.MarginLeft));
            pe.SetAttribute("marginRight",  Fmt(page.MarginRight));
            pe.SetAttribute("marginTop",    Fmt(page.MarginTop));
            pe.SetAttribute("marginBottom", Fmt(page.MarginBottom));

            foreach (var ctrl in canvas.GetControlsForPage(page))
            {
                var ce = BuildControlElement(xd, ctrl, canvas);
                if (ce != null) pe.AppendChild(ce);
            }
            return pe;
        }

        // ── Load ─────────────────────────────────────────────────────────────

        public static void Load(string path, CanvasPanel canvas, PagesPanel pages)
        {
            var xd = new XmlDocument();
            xd.Load(path);
            var root = xd.DocumentElement;
            if (root == null) throw new InvalidDataException("Invalid .epd file.");

            DocumentPage.WIDTH_MM  = ParseF(root, "widthMm",  210f);
            DocumentPage.HEIGHT_MM = ParseF(root, "heightMm", 297f);

            canvas.ClearAll();
            canvas.AutoScrollPosition = Point.Empty; // reset scroll so loaded positions are physical=logical
            Document.Pages.Clear();
            Document.CurrentPageIndex = 0;

            foreach (XmlElement pe in root.SelectNodes("Page"))
            {
                string id = pe.GetAttribute("id");
                DocumentPage page;
                if      (id == "masterOdd")  page = Document.MasterOdd;
                else if (id == "masterEven") page = Document.MasterEven;
                else { page = new DocumentPage(); Document.Pages.Add(page); }

                page.MarginLeft   = ParseF(pe, "marginLeft",   15f);
                page.MarginRight  = ParseF(pe, "marginRight",  15f);
                page.MarginTop    = ParseF(pe, "marginTop",    15f);
                page.MarginBottom = ParseF(pe, "marginBottom", 15f);

                foreach (XmlElement ce in pe.ChildNodes)
                {
                    var ctrl = BuildControl(ce, canvas);
                    if (ctrl != null) canvas.LoadControlForPage(page, ctrl);
                }
            }

            if (Document.Pages.Count == 0) Document.Pages.Add(new DocumentPage());
            Document.CurrentPageIndex = 0;
            canvas.PageChanged();
            pages.RebuildThumbnails();
        }

        // ── Export PNG ────────────────────────────────────────────────────────

        public static void ExportPng(string basePath, CanvasPanel canvas)
        {
            string dir  = Path.GetDirectoryName(basePath) ?? ".";
            string stem = Path.GetFileNameWithoutExtension(basePath);

            for (int i = 0; i < Document.Pages.Count; i++)
            {
                using (var bmp = canvas.RenderPageToBitmap(Document.Pages[i]))
                {
                    string file = Document.Pages.Count == 1
                        ? Path.Combine(dir, stem + ".png")
                        : Path.Combine(dir, stem + "_page" + (i + 1) + ".png");
                    bmp.Save(file, ImageFormat.Png);
                }
            }
        }

        // ── Export PDF ────────────────────────────────────────────────────────

        public static void ExportPdf(string path, CanvasPanel canvas)
        {
            const string PRINTER = "Microsoft Print to PDF";

            bool found = false;
            foreach (string p in PrinterSettings.InstalledPrinters)
                if (string.Equals(p, PRINTER, StringComparison.OrdinalIgnoreCase))
                { found = true; break; }

            if (!found)
                throw new InvalidOperationException(
                    "'Microsoft Print to PDF' is not available on this system.\n" +
                    "Please install it via Windows Settings > Printers & Scanners.");

            int pageIdx = 0;
            var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName  = PRINTER;
            doc.PrinterSettings.PrintToFile  = true;
            doc.PrinterSettings.PrintFileName = path;

            // Convert document page size from mm to hundredths-of-inch
            int wHundredths = (int)(DocumentPage.WIDTH_MM  / 25.4f * 100);
            int hHundredths = (int)(DocumentPage.HEIGHT_MM / 25.4f * 100);
            doc.DefaultPageSettings.PaperSize = new PaperSize("Custom", wHundredths, hHundredths);
            doc.DefaultPageSettings.Margins   = new Margins(0, 0, 0, 0);

            doc.PrintPage += (s, pe) =>
            {
                canvas.RenderPageToPrinter(pe.Graphics, Document.Pages[pageIdx]);
                pageIdx++;
                pe.HasMorePages = pageIdx < Document.Pages.Count;
            };

            doc.Print();
        }

        // ── Snapshot / restore (used by undo) ────────────────────────────────

        internal static string SnapshotPage(DocumentPage page, CanvasPanel canvas)
        {
            var xd   = new XmlDocument();
            var snap = xd.CreateElement("Snap");
            foreach (var ctrl in canvas.GetControlsForPage(page))
            {
                var ce = BuildControlElement(xd, ctrl, canvas);
                if (ce != null) snap.AppendChild(ce);
            }
            return snap.OuterXml;
        }

        internal static void RestorePageSnapshot(string snapshot, DocumentPage page,
                                                  CanvasPanel canvas)
        {
            // Dispose existing controls for this page
            var existing = new List<Control>(
                (IEnumerable<Control>)canvas.GetControlsForPage(page));
            foreach (var c in existing)
            {
                canvas.Controls.Remove(c);
                if (!c.IsDisposed) c.Dispose();
            }

            // Restore controls from snapshot XML
            var xd = new XmlDocument();
            xd.LoadXml(snapshot);
            foreach (XmlElement ce in xd.DocumentElement.ChildNodes)
            {
                var ctrl = BuildControl(ce, canvas);
                if (ctrl != null) canvas.LoadControlForPage(page, ctrl);
            }
            canvas.PageChanged();
        }

        // ── Clipboard helpers ─────────────────────────────────────────────────

        internal static string SerializeControl(Control ctrl, CanvasPanel canvas)
        {
            var xd = new XmlDocument();
            var ce = BuildControlElement(xd, ctrl, canvas);
            return ce?.OuterXml ?? string.Empty;
        }

        internal static Control DeserializeControl(string xml, CanvasPanel canvas)
        {
            if (string.IsNullOrEmpty(xml)) return null;
            var xd = new XmlDocument();
            xd.LoadXml(xml);
            return BuildControl(xd.DocumentElement, canvas);
        }

        // Serialises multiple controls into a single <MultiClipboard> XML string.
        internal static string SerializeControls(
            System.Collections.Generic.IEnumerable<Control> controls, CanvasPanel canvas)
        {
            var xd   = new XmlDocument();
            var root = xd.CreateElement("MultiClipboard");
            xd.AppendChild(root);
            foreach (var ctrl in controls)
            {
                var ce = BuildControlElement(xd, ctrl, canvas);
                if (ce != null) root.AppendChild(ce);
            }
            return root.OuterXml;
        }

        // Deserialises a <MultiClipboard> string (or a legacy single-element string)
        // and returns all controls, positioned correctly for the current canvas.
        internal static System.Collections.Generic.List<Control> DeserializeControls(
            string xml, CanvasPanel canvas)
        {
            var result = new System.Collections.Generic.List<Control>();
            if (string.IsNullOrEmpty(xml)) return result;
            try
            {
                var xd = new XmlDocument();
                xd.LoadXml(xml);
                var root = xd.DocumentElement;
                if (root == null) return result;

                if (root.Name == "MultiClipboard")
                {
                    foreach (XmlElement ce in root.ChildNodes)
                    {
                        var ctrl = BuildControl(ce, canvas);
                        if (ctrl != null) result.Add(ctrl);
                    }
                }
                else
                {
                    // Legacy single-control format
                    var ctrl = BuildControl(root, canvas);
                    if (ctrl != null) result.Add(ctrl);
                }
            }
            catch { }
            return result;
        }

        // ── Core element builders (shared by save, snapshot, clipboard) ───────

        private static XmlElement BuildControlElement(XmlDocument xd, Control ctrl,
                                                       CanvasPanel canvas)
        {
            float  pxMm   = canvas.PxPerMm;
            PointF origin = canvas.PageOriginPx;
            // AutoScroll only shifts controls that have a Win32 HWND (visible or previously shown).
            // Invisible controls without handles are already at their logical positions.
            int    scrollX = ctrl.IsHandleCreated ? canvas.AutoScrollPosition.X : 0;
            int    scrollY = ctrl.IsHandleCreated ? canvas.AutoScrollPosition.Y : 0;
            float  xMm    = ((ctrl.Location.X - scrollX) - origin.X) / pxMm;
            float  yMm    = ((ctrl.Location.Y - scrollY) - origin.Y) / pxMm;
            float  wMm    = ctrl.Width  / pxMm;
            float  hMm    = ctrl.Height / pxMm;

            XmlElement ce = null;

            if (ctrl is PrintTextBox ptb)
            {
                ce = xd.CreateElement("PrintBox");
                ce.SetAttribute("font",            ptb.FontFamily);
                ce.SetAttribute("sizePt",          Fmt(ptb.FontSizePt));
                ce.SetAttribute("style",           ((int)ptb.TextFontStyle).ToString());
                ce.SetAttribute("textColor",       ToHex(ptb.TextColor));
                ce.SetAttribute("hAlign",          ((int)ptb.HTextAlign).ToString());
                ce.SetAttribute("vAlign",          ((int)ptb.VTextAlign).ToString());
                ce.SetAttribute("borderColor",     ToHex(ptb.BorderColor));
                ce.SetAttribute("borderWidth",     ptb.BorderWidth.ToString());
                ce.SetAttribute("borderTop",       ptb.BorderTop    ? "1" : "0");
                ce.SetAttribute("borderBottom",    ptb.BorderBottom ? "1" : "0");
                ce.SetAttribute("borderLeft",      ptb.BorderLeft   ? "1" : "0");
                ce.SetAttribute("borderRight",     ptb.BorderRight  ? "1" : "0");
                ce.SetAttribute("fillColor",       ToHex(ptb.FillColor));
                ce.SetAttribute("fillTransparent", ptb.FillTransparent ? "1" : "0");
                ce.SetAttribute("rtl",             ptb.IsRightToLeft ? "1" : "0");
                ce.SetAttribute("text",            ptb.DisplayText);
                string csData = ptb.CharStyleData;
                if (!string.IsNullOrEmpty(csData))
                    ce.SetAttribute("charStyles",  csData);
            }
            else if (ctrl is BrailleTextBox btb)
            {
                ce = xd.CreateElement("BrailleBox");
                ce.SetAttribute("text", btb.BrailleText);
            }
            else if (ctrl is TactileBox tb)
            {
                var grid = tb.DotGrid;
                int cols = grid.GetLength(0);
                int rows = grid.GetLength(1);
                var sb   = new System.Text.StringBuilder(cols * rows);
                for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    sb.Append(grid[c, r] ? '1' : '0');
                ce = xd.CreateElement("TactileBox");
                ce.SetAttribute("cols", cols.ToString());
                ce.SetAttribute("rows", rows.ToString());
                ce.SetAttribute("dots", sb.ToString());
            }
            else if (ctrl is ImageBox ib && ib.SourceImage != null)
            {
                ce = xd.CreateElement("ImageBox");
                using (var ms = new MemoryStream())
                {
                    ib.SourceImage.Save(ms, ImageFormat.Png);
                    ce.InnerText = Convert.ToBase64String(ms.ToArray());
                }
            }
            else if (ctrl is LineBox lb)
            {
                ce = xd.CreateElement("LineBox");
                ce.SetAttribute("direction", lb.Direction == LineBox.LineDirection.Vertical ? "v" : "h");
                ce.SetAttribute("lineColor", ToHex(lb.LineColor));
                ce.SetAttribute("thickness", lb.LineThickness.ToString());
            }
            else if (ctrl is TableBox tab)
            {
                ce = xd.CreateElement("TableBox");
                ce.SetAttribute("lineColor", ToHex(tab.LineColor));
                ce.SetAttribute("thickness", tab.LineThickness.ToString());
                ce.SetAttribute("rowSpec",   tab.RowSpec);
                ce.SetAttribute("colSpec",   tab.ColSpec);
            }
            else if (ctrl is PageNumberBox pnbSave)
            {
                ce = xd.CreateElement("PageNumberBox");
                ce.SetAttribute("braille", pnbSave.IsBraille ? "1" : "0");
            }

            if (ce != null)
            {
                ce.SetAttribute("xMm", Fmt(xMm));
                ce.SetAttribute("yMm", Fmt(yMm));
                ce.SetAttribute("wMm", Fmt(wMm));
                ce.SetAttribute("hMm", Fmt(hMm));
            }
            return ce;
        }

        private static Control BuildControl(XmlElement ce, CanvasPanel canvas)
        {
            float  pxMm   = canvas.PxPerMm;
            PointF origin = canvas.PageOriginPx;
            float  xMm    = ParseF(ce, "xMm", 0f);
            float  yMm    = ParseF(ce, "yMm", 0f);
            float  wMm    = ParseF(ce, "wMm", 40f);
            float  hMm    = ParseF(ce, "hMm", 10f);
            int    px     = (int)(origin.X + xMm * pxMm);
            int    py     = (int)(origin.Y + yMm * pxMm);
            int    pw     = Math.Max(10, (int)(wMm * pxMm));
            int    ph     = Math.Max(10, (int)(hMm * pxMm));

            Control ctrl = null;

            switch (ce.Name)
            {
                case "PrintBox":
                {
                    var ptbLoad = new PrintTextBox
                    {
                        FontFamily      = ce.GetAttribute("font"),
                        FontSizePt      = ParseF(ce, "sizePt", 12f),
                        TextFontStyle   = (FontStyle)ParseI(ce, "style", 0),
                        TextColor       = FromHex(ce.GetAttribute("textColor")),
                        HTextAlign      = (StringAlignment)ParseI(ce, "hAlign", 0),
                        VTextAlign      = (StringAlignment)ParseI(ce, "vAlign", 0),
                        BorderColor     = FromHex(ce.GetAttribute("borderColor")),
                        BorderWidth     = ParseI(ce, "borderWidth", 1),
                        BorderTop       = ce.GetAttribute("borderTop")    == "1",
                        BorderBottom    = ce.GetAttribute("borderBottom") == "1",
                        BorderLeft      = ce.GetAttribute("borderLeft")   == "1",
                        BorderRight     = ce.GetAttribute("borderRight")  == "1",
                        FillColor       = FromHex(ce.GetAttribute("fillColor")),
                        FillTransparent = ce.GetAttribute("fillTransparent") != "0",
                        IsRightToLeft   = ce.GetAttribute("rtl") == "1",
                        DisplayText     = ce.GetAttribute("text"), // resets _charStyle — must be before CharStyleData
                        Width           = pw
                    };
                    string csData = ce.GetAttribute("charStyles");
                    if (!string.IsNullOrEmpty(csData))
                        ptbLoad.CharStyleData = csData;
                    ctrl = ptbLoad;
                    break;
                }

                case "BrailleBox":
                    ctrl = new BrailleTextBox
                    {
                        BrailleText = ce.GetAttribute("text"),
                        Size        = new Size(pw, ph)
                    };
                    break;

                case "TactileBox":
                {
                    int    cols    = ParseI(ce, "cols", 1);
                    int    rows    = ParseI(ce, "rows", 1);
                    string dots    = ce.GetAttribute("dots");
                    var    grid    = new bool[Math.Max(1, cols), Math.Max(1, rows)];
                    int    k       = 0;
                    for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++, k++)
                        if (k < dots.Length) grid[c, r] = dots[k] == '1';
                    ctrl = new TactileBox { DotGrid = grid };
                    break;
                }

                case "ImageBox":
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(ce.InnerText.Trim());
                        using (var ms  = new MemoryStream(bytes))
                        using (var tmp = Image.FromStream(ms))
                            ctrl = new ImageBox(new Bitmap(tmp)) { Size = new Size(pw, ph) };
                    }
                    catch { }
                    break;

                case "LineBox":
                    ctrl = new LineBox
                    {
                        Direction     = ce.GetAttribute("direction") == "v"
                                        ? LineBox.LineDirection.Vertical
                                        : LineBox.LineDirection.Horizontal,
                        LineColor     = FromHex(ce.GetAttribute("lineColor")),
                        LineThickness = ParseI(ce, "thickness", 1),
                        Size          = new Size(pw, ph)
                    };
                    break;

                case "TableBox":
                    ctrl = new TableBox
                    {
                        LineColor     = FromHex(ce.GetAttribute("lineColor")),
                        LineThickness = ParseI(ce, "thickness", 1),
                        RowSpec       = ce.GetAttribute("rowSpec"),
                        ColSpec       = ce.GetAttribute("colSpec"),
                        Size          = new Size(pw, ph)
                    };
                    break;

                case "PageNumberBox":
                    ctrl = new PageNumberBox
                    {
                        IsBraille = ce.GetAttribute("braille") == "1"
                    };
                    break;
            }

            if (ctrl != null) ctrl.Location = new Point(px, py);
            return ctrl;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string Fmt(float v) =>
            v.ToString("F2", CultureInfo.InvariantCulture);

        private static string ToHex(Color c) =>
            ((uint)c.ToArgb()).ToString("X8");

        private static Color FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.Black;
            try { return Color.FromArgb((int)Convert.ToUInt32(hex, 16)); }
            catch { return Color.Black; }
        }

        private static float ParseF(XmlElement el, string attr, float def)
        {
            string v = el.GetAttribute(attr);
            return float.TryParse(v, NumberStyles.Float,
                CultureInfo.InvariantCulture, out float r) ? r : def;
        }

        private static int ParseI(XmlElement el, string attr, int def)
        {
            string v = el.GetAttribute(attr);
            return int.TryParse(v, out int r) ? r : def;
        }
    }
}
