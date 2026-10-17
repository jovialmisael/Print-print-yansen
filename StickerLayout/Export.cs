using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace StickerLayout;

public static class Export
{
    const double Pt = 72 / 25.4;

    // Kiss-cut = 100% magenta, die-cut = 100% cyan (CMYK): Cutting Master 5 memilih garis potong "By Color".
    static readonly XColor KissColor = XColor.FromCmyk(0, 1, 0, 0), DieColor = XColor.FromCmyk(1, 0, 0, 0);

    static Export() => GlobalFontSettings.UseWindowsFontsUnderWindows = true; // teks Print Ticket memakai font Windows

    // Print Ticket: ringkasan 1 halaman A4 untuk lampiran nota kasir/SPK. Daftar lembar dan desain dipotong
    // supaya tetap 1 halaman.
    public static void Ticket(string path, string paper, IList<List<Placed>> sheets, double cw, IList<double> sheetH, double margin, IEnumerable<string> settings)
    {
        using var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Width = XUnit.FromMillimeter(210);
        page.Height = XUnit.FromMillimeter(297);
        using var g = XGraphics.FromPdfPage(page);
        var opt = new XPdfFontOptions(PdfFontEncoding.Unicode);
        XFont title = new("Arial", 16, XFontStyleEx.Bold, opt), bold = new("Arial", 10, XFontStyleEx.Bold, opt), body = new("Arial", 10, XFontStyleEx.Regular, opt);
        double y = 25; // mm, garis dasar baris berikutnya
        void Line(string text, XFont f, double next = 6) { g.DrawString(text, f, XBrushes.Black, 20 * Pt, y * Pt); y += next; }

        var all = sheets.SelectMany(x => x).ToList();
        double fill = sheets.Select((_, i) => Yield.Fillable(cw, sheetH[i], margin)).Sum(), use = fill > 0 ? all.Sum(p => p.S.AreaMm2) / fill : 0;
        bool regular = Yield.Regular(all);
        var t = Yield.Target(regular);
        Line(App.Brand + " — Print Ticket", title, 9);
        Line(Lang.T("TicketDate", DateTime.Now), body, 9);
        Line(Lang.T("TicketPaper", paper, cw, sheetH.Max(), margin), body, 9);
        Line(Lang.T("TicketSheets", sheets.Count), bold);
        Line(Lang.T("TicketPcs", all.Count), bold);
        Line(Lang.T("TicketUse", use, 1 - use), bold);
        Line(Lang.T(regular ? "TargetReg" : "TargetIrr", t.Low, t.High, t.Green), body, 11);

        const int MaxSheets = 10, MaxDesigns = 20;
        Line(Lang.T("TicketPerSheet"), bold);
        for (int i = 0; i < Math.Min(sheets.Count, MaxSheets); i++)
            Line(Lang.T("TicketSheetLine", i + 1, sheets[i].Count, Yield.Use(sheets[i], cw, sheetH[i], margin)), body, 5);
        if (sheets.Count > MaxSheets) Line(Lang.T("TicketMore", sheets.Count - MaxSheets), body, 5);
        y += 5;
        var designs = all.GroupBy(p => p.S).ToList();
        Line(Lang.T("TicketDesigns"), bold);
        foreach (var d in designs.Take(MaxDesigns)) Line($"{d.Key.Name} — {d.Key.ArtW:0.#} × {d.Key.ArtH:0.#} mm — {d.Count()} pcs", body, 5);
        if (designs.Count > MaxDesigns) Line(Lang.T("TicketMore", designs.Count - MaxDesigns), body, 5);
        y += 5;
        Line(Lang.T("TicketSettings"), bold);
        foreach (var s in settings) Line(s, body, 5);
        doc.Save(path);
    }

    // Satu halaman per lembar. Artwork PDF/AI/CDR ditempel sebagai vektor (warna asli), PNG/JPG apa adanya,
    // di-clip ke garis potong terluar; garis potong digambar di atasnya. Mengembalikan peringatan per file.
    public static List<string> Pdf(string path, IList<List<Placed>> sheets, double cw, IList<double> sheetH)
    {
        var warn = new List<string>();
        using var doc = new PdfDocument();
        doc.Options.ColorMode = PdfColorMode.Cmyk; // tanpa ini PDFsharp menulis garis potong sebagai RGB
        var forms = new Dictionary<string, XPdfForm?>();
        var images = new Dictionary<Sticker, XImage>();
        for (int i = 0; i < sheets.Count; i++)
        {
            var page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(cw);
            page.Height = XUnit.FromMillimeter(sheetH[i]);
            using var g = XGraphics.FromPdfPage(page);
            g.ScaleTransform(Pt); // satuan gambar = mm
            foreach (var p in sheets[i])
            {
                var s = p.S;
                var st = g.Save();
                Place(g, p);
                g.IntersectClip(ToPath(s.ArtClip));
                var form = s.Pdf == null ? null : Form(forms, s.Pdf, warn);
                if (form != null)
                {
                    form.PageIndex = s.Page;
                    g.DrawImage(form, s.Full.X, s.Full.Y, s.Full.Width, s.Full.Height);
                }
                else if (s.Pdf == null)
                {
                    if (!images.TryGetValue(s, out var img)) images[s] = img = XImage.FromStream(new MemoryStream(s.BleedPng ?? s.Raster));
                    var r = s.BleedPng != null ? s.BleedRect : s.Full;
                    g.DrawImage(img, r.X, r.Y, r.Width, r.Height);
                }
                else
                {
                    // PDF gagal diimpor PDFsharp: tempel render layar (resolusi lebih rendah, warna RGB)
                    if (!images.TryGetValue(s, out var img)) images[s] = img = XImage.FromStream(new MemoryStream(Loader.Png(s.Preview)));
                    g.DrawImage(img, s.Off, s.Off, s.ArtW, s.ArtH);
                }
                g.Restore(st);
            }
            foreach (var p in sheets[i])
            {
                var st = g.Save();
                Place(g, p);
                if (p.S.KissGeo is { } k) g.DrawPath(new XPen(KissColor, 0.1), ToPath(k));
                if (p.S.DieGeo is { } d) g.DrawPath(new XPen(DieColor, 0.1), ToPath(d));
                g.Restore(st);
            }
        }
        doc.Save(path);
        return warn;
    }

    static XPdfForm? Form(Dictionary<string, XPdfForm?> forms, string pdf, List<string> warn)
    {
        if (forms.TryGetValue(pdf, out var f)) return f;
        try { f = XPdfForm.FromFile(pdf); }
        catch (Exception ex) { warn.Add(Lang.T("PdfRaster", Path.GetFileName(pdf), ex.Message)); }
        return forms[pdf] = f;
    }

    static void Place(XGraphics g, Placed p)
    {
        var (tx, ty) = p.T;
        g.TranslateTransform(p.X + tx, p.Y + ty);
        g.RotateTransform(p.Rot);
    }

    // Satu JPG per lembar (nama-1.jpg, nama-2.jpg, ...), sisi panjang +-2000 px: cukup tajam, ringan dikirim lewat WhatsApp.
    public static List<string> Preview(string path, IList<List<Placed>> sheets, double cw, IList<double> sheetH, double margin)
    {
        var files = new List<string>();
        for (int i = 0; i < sheets.Count; i++)
        {
            double h = sheetH[i];
            var file = sheets.Count == 1 ? path
                : Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)}-{i + 1}{Path.GetExtension(path)}");
            var enc = new JpegBitmapEncoder { QualityLevel = 85 };
            enc.Frames.Add(BitmapFrame.Create(Snapshot(Nest.Sheet(sheets[i], cw, h, margin, null, i), cw, h, 2000 / Math.Max(cw, h))));
            using (var fs = File.Create(file)) enc.Save(fs);
            files.Add(file);
        }
        return files;
    }

    // Render elemen WPF (satuan mm) ke bitmap; k = pixel per mm.
    public static BitmapSource Snapshot(FrameworkElement el, double w, double h, double k)
    {
        el.Measure(new Size(w, h));
        el.Arrange(new Rect(0, 0, w, h));
        el.UpdateLayout();
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(k, k));
            dc.DrawRectangle(new VisualBrush(el), null, new Rect(0, 0, w, h));
        }
        var rt = new RenderTargetBitmap((int)Math.Round(w * k), (int)Math.Round(h * k), 96, 96, PixelFormats.Pbgra32);
        rt.Render(v);
        rt.Freeze();
        return rt;
    }

    static XGraphicsPath ToPath(Geometry geo)
    {
        var path = new XGraphicsPath();
        foreach (var f in PathGeometry.CreateFromGeometry(geo).Figures)
        {
            path.StartFigure();
            var a = f.StartPoint;
            foreach (var seg in f.Segments)
                switch (seg)
                {
                    case LineSegment l: path.AddLine(X(a), X(l.Point)); a = l.Point; break;
                    case PolyLineSegment pl: foreach (var q in pl.Points) { path.AddLine(X(a), X(q)); a = q; } break;
                    case BezierSegment b: path.AddBezier(X(a), X(b.Point1), X(b.Point2), X(b.Point3)); a = b.Point3; break;
                    case PolyBezierSegment pb:
                        for (int i = 0; i + 2 < pb.Points.Count; i += 3)
                        {
                            path.AddBezier(X(a), X(pb.Points[i]), X(pb.Points[i + 1]), X(pb.Points[i + 2]));
                            a = pb.Points[i + 2];
                        }
                        break;
                }
            if (f.IsClosed) path.CloseFigure();
        }
        return path;
    }

    static XPoint X(Point p) => new(p.X, p.Y);
}
