using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace StickerLayout;

// Jalankan: StickerLayout.exe --selfcheck <folder>. Menulis sampel, memuat, menyusun, mengekspor, lalu memeriksa hasilnya.
public static class SelfCheck
{
    public static int Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var log = new List<string>();
        try
        {
            Samples(dir);
            var items = new[] { ("circle.png", 3), ("star.png", 3), ("badge.svg", 2), ("heart.svg", 3) }
                .Select(t => { var s = Loader.Load(Path.Combine(dir, t.Item1), 40, 2); s.Copies = t.Item2; return s; }).ToList();
            foreach (var s in items)
            {
                Check(s.CutPath.StartsWith('M'), $"{s.Name}: path potong kosong", log);
                Check(s.Area > 0, $"{s.Name}: grid kosong", log);
                log.Add($"{s.Name}: kotak {s.W}x{s.H} mm, area {s.AreaMm2:F0} mm2, off {s.Off:F2}");
            }

            const double cw = 200, ch = 200;
            var ps = Nest.Run(items, cw, ch, 2, out int failed);
            Check(failed == 0, $"{failed} stiker tidak muat", log);
            Check(ps.Count == items.Sum(i => i.Copies), "jumlah tempatan salah", log);

            // tidak ada sel yang dipakai dua stiker, semua di dalam area kanvas
            var occ = new Dictionary<(int, int), int>();
            foreach (var p in ps)
            {
                var g = Nest.Rotate(p.S.Grid, p.Rot);
                int ox = (int)Math.Round((p.X - Nest.Margin) / Nest.Cell), oy = (int)Math.Round((p.Y - Nest.Margin) / Nest.Cell);
                for (int x = 0; x < g.GetLength(0); x++)
                    for (int y = 0; y < g.GetLength(1); y++)
                        if (g[x, y])
                        {
                            Check(occ.TryAdd((ox + x, oy + y), 1), "dua stiker menempati sel yang sama", log);
                            Check(p.X >= Nest.Margin && p.Y >= Nest.Margin && p.X + p.BW <= cw - Nest.Margin && p.Y + p.BH <= ch - Nest.Margin, "stiker keluar area kanvas", log);
                        }
            }
            log.Add($"ditempatkan {ps.Count}, rotasi dipakai: {string.Join(",", ps.Select(p => p.Rot).Distinct())}");

            string svg = Nest.Export(ps, cw, ch, true);
            File.WriteAllText(Path.Combine(dir, "export.svg"), svg);
            Check(svg.Contains("id=\"CutContour\"") && svg.Contains("id=\"Print\""), "layer tidak ada", log);
            Check(svg.Split("<path transform=").Length - 1 == ps.Count, "jumlah path potong != jumlah stiker", log);

            Save(Snapshot(Nest.Sheet(ps, cw, ch, true, null), cw, ch, 4), Path.Combine(dir, "layout.png"));
            var dg = new FileSvgReader(new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true }).Read(Path.Combine(dir, "export.svg"));
            log.Add($"export.svg dibaca ulang, bounds {dg.Bounds.Width:F0}x{dg.Bounds.Height:F0}");
            // SharpVectors mengonversi mm -> px (96 dpi); skala balik agar 1 mm = 4 px
            double k = 4 * 25.4 / 96;
            var v = new DrawingVisual();
            using (var dc = v.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, cw * 4, ch * 4));
                dc.PushTransform(new ScaleTransform(k, k));
                dc.DrawDrawing(dg);
            }
            var rt = new RenderTargetBitmap((int)(cw * 4), (int)(ch * 4), 96, 96, PixelFormats.Pbgra32);
            rt.Render(v);
            Save(rt, Path.Combine(dir, "export-render.png"));
            log.Add("OK");
            File.WriteAllLines(Path.Combine(dir, "selfcheck.txt"), log);
            return 0;
        }
        catch (Exception ex)
        {
            log.Add("GAGAL: " + ex);
            File.WriteAllLines(Path.Combine(dir, "selfcheck.txt"), log);
            return 1;
        }
    }

    static void Check(bool ok, string msg, List<string> log)
    {
        if (!ok) throw new Exception(msg);
    }

    static BitmapSource Snapshot(FrameworkElement el, double w, double h, double k)
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
        var rt = new RenderTargetBitmap((int)(w * k), (int)(h * k), 96, 96, PixelFormats.Pbgra32);
        rt.Render(v);
        return rt;
    }

    static void Save(BitmapSource b, string path)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(b));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    static void Samples(string dir)
    {
        // circle.png: transparan
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen())
        {
            dc.DrawEllipse(Brushes.Orange, null, new Point(150, 150), 140, 140);
            dc.DrawEllipse(Brushes.SteelBlue, null, new Point(150, 150), 90, 90);
            dc.DrawEllipse(Brushes.White, null, new Point(150, 150), 40, 40);
        }
        var rt = new RenderTargetBitmap(300, 300, 96, 96, PixelFormats.Pbgra32);
        rt.Render(v);
        Save(rt, Path.Combine(dir, "circle.png"));

        // star.png: latar putih buram + lubang putih di tengah
        v = new DrawingVisual();
        using (var dc = v.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 300, 300));
            var pts = Enumerable.Range(0, 10).Select(i =>
            {
                double a = -Math.PI / 2 + i * Math.PI / 5, r = i % 2 == 0 ? 140 : 60;
                return new Point(150 + r * Math.Cos(a), 155 + r * Math.Sin(a));
            }).ToList();
            var g = new StreamGeometry();
            using (var c = g.Open()) { c.BeginFigure(pts[0], true, true); c.PolyLineTo(pts.Skip(1).ToList(), true, false); }
            dc.DrawGeometry(Brushes.Crimson, null, g);
            dc.DrawEllipse(Brushes.White, null, new Point(150, 155), 25, 25);
        }
        rt = new RenderTargetBitmap(300, 300, 96, 96, PixelFormats.Pbgra32);
        rt.Render(v);
        Save(rt, Path.Combine(dir, "star.png"));

        // SVG dengan viewBox dan width/height berbeda skala
        File.WriteAllText(Path.Combine(dir, "badge.svg"),
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"120\" viewBox=\"0 0 100 60\">" +
            "<rect x=\"2\" y=\"2\" width=\"96\" height=\"56\" rx=\"12\" fill=\"#2a9d8f\"/>" +
            "<circle cx=\"30\" cy=\"30\" r=\"14\" fill=\"#e9c46a\"/><rect x=\"52\" y=\"20\" width=\"36\" height=\"6\" fill=\"#fff\"/><rect x=\"52\" y=\"34\" width=\"28\" height=\"6\" fill=\"#fff\"/></svg>");
        File.WriteAllText(Path.Combine(dir, "heart.svg"),
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">" +
            "<path d=\"M50 88 C10 55 5 30 25 15 C40 5 50 20 50 25 C50 20 60 5 75 15 C95 30 90 55 50 88 Z\" fill=\"#e63946\"/></svg>");
    }
}
