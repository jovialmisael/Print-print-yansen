using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using BitmapToVector;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace StickerLayout;

public class Sticker
{
    public string Name { get; set; } = "";
    public int Copies { get; set; } = 1;
    public string Source = "";
    public double W, H;              // ukuran kotak (mm), kelipatan Cell, sudah termasuk offset potong
    public double Off, ArtW, ArtH;   // posisi dan ukuran artwork di dalam kotak (mm)
    public double ReqOff, AreaMm2;
    public BitmapSource Preview = null!;
    public string ArtSvg = "", ViewBox = "";
    public byte[] ArtPng = [];
    public string CutPath = "";      // path SVG (mm), koordinat kotak
    public bool[,] Grid = null!;     // hunian sel nesting [x,y]
    public int Area;
    Geometry? geo;
    public Geometry CutGeo => geo ??= Freeze(Geometry.Parse(CutPath));
    static Geometry Freeze(Geometry g) { g.Freeze(); return g; }
}

public class Placed
{
    public Sticker S = null!;
    public double X, Y;
    public int Rot;
    public double BW => Rot % 180 == 0 ? S.W : S.H;
    public double BH => Rot % 180 == 0 ? S.H : S.W;
    public (double x, double y) T => Rot switch { 90 => (S.H, 0), 180 => (S.W, S.H), 270 => (0, S.W), _ => (0, 0) };
}

public static class Loader
{
    public const double PxMm = 6; // resolusi mask trace: 6 px per mm

    // Semua input (PNG / SVG / hasil scan) -> siluet -> dilasi offset -> Potrace -> path potong vektor.
    public static Sticker Load(string path, double widthMm, double offMm)
    {
        var s = new Sticker { Name = System.IO.Path.GetFileName(path), Source = path, ReqOff = offMm };
        BitmapSource src;
        if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            var (xml, vx, vy) = Normalize(File.ReadAllText(path));
            s.ArtSvg = xml;
            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".svg");
            File.WriteAllText(tmp, xml);
            DrawingGroup dg;
            try { dg = new FileSvgReader(new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true }).Read(tmp); }
            finally { File.Delete(tmp); }
            var b = dg.Bounds;
            s.ViewBox = $"{F(b.X + vx)} {F(b.Y + vy)} {F(b.Width)} {F(b.Height)}";
            src = Render(dg, 1200);
        }
        else
        {
            s.ArtPng = File.ReadAllBytes(path);
            using var ms = new MemoryStream(s.ArtPng);
            src = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        }
        s.Preview = src;
        s.ArtW = widthMm;
        s.ArtH = widthMm * src.PixelHeight / src.PixelWidth;

        var small = new TransformedBitmap(src, new ScaleTransform(s.ArtW * PxMm / src.PixelWidth, s.ArtH * PxMm / src.PixelHeight));
        var mask = Silhouette(small, out int mw, out int mh);

        int r = (int)Math.Ceiling(offMm * PxMm), pad = r + 2, W = mw + 2 * pad, H = mh + 2 * pad;
        var disc = new List<(int, int)>();
        for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++) if (dx * dx + dy * dy <= r * r) disc.Add((dx, dy));
        var dil = new bool[W, H];
        for (int y = 0; y < mh; y++)
            for (int x = 0; x < mw; x++)
            {
                if (!mask[x, y]) continue;
                dil[x + pad, y + pad] = true;
                bool edge = x == 0 || y == 0 || x == mw - 1 || y == mh - 1 || !mask[x - 1, y] || !mask[x + 1, y] || !mask[x, y - 1] || !mask[x, y + 1];
                if (edge) foreach (var (dx, dy) in disc) dil[x + pad + dx, y + pad + dy] = true;
            }

        s.Off = pad / PxMm;
        s.W = Math.Ceiling(W / PxMm / Nest.Cell) * Nest.Cell;
        s.H = Math.Ceiling(H / PxMm / Nest.Cell) * Nest.Cell;

        using var pb = PotraceBitmap.Create(W, H);
        int px = 0;
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (dil[x, y]) { pb.SetBlack(x, y); px++; }
        s.CutPath = ToSvg(Potrace.Trace(new PotraceParam(), pb), 1 / PxMm);
        s.AreaMm2 = px / (PxMm * PxMm);

        int cp = (int)(Nest.Cell * PxMm);
        s.Grid = new bool[(int)(s.W / Nest.Cell), (int)(s.H / Nest.Cell)];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (dil[x, y]) s.Grid[x / cp, y / cp] = true;
        foreach (bool c in s.Grid) if (c) s.Area++;
        return s;
    }

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    // SharpVectors memakai satuan viewport; samakan width/height dengan viewBox agar 1 unit = 1 unit user.
    static (string xml, double vx, double vy) Normalize(string xml)
    {
        var doc = XDocument.Parse(xml);
        var r = doc.Root!;
        var vb = r.Attribute("viewBox")?.Value.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        if (vb is not { Length: 4 }) return (xml, 0, 0);
        r.SetAttributeValue("width", F(vb[2]));
        r.SetAttributeValue("height", F(vb[3]));
        return (doc.ToString(SaveOptions.DisableFormatting), vb[0], vb[1]);
    }

    static BitmapSource Render(Drawing d, int maxPx)
    {
        var b = d.Bounds;
        double k = maxPx / Math.Max(b.Width, b.Height);
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(k, k));
            dc.PushTransform(new TranslateTransform(-b.X, -b.Y));
            dc.DrawDrawing(d);
        }
        var rt = new RenderTargetBitmap((int)Math.Ceiling(b.Width * k), (int)Math.Ceiling(b.Height * k), 96, 96, PixelFormats.Pbgra32);
        rt.Render(v);
        rt.Freeze();
        return rt;
    }

    // Foreground = alpha (jika ada transparansi) atau "bukan putih". Lubang di dalam diisi: hanya kontur luar yang dipotong.
    static bool[,] Silhouette(BitmapSource b, out int w, out int h)
    {
        var f = new FormatConvertedBitmap(b, PixelFormats.Bgra32, null, 0);
        int W = f.PixelWidth, H = f.PixelHeight;
        var px = new byte[W * H * 4];
        f.CopyPixels(px, W * 4, 0);
        bool alpha = false;
        for (int i = 3; i < px.Length; i += 4) if (px[i] < 250) { alpha = true; break; }
        bool Fg(int i) => alpha ? px[i * 4 + 3] > 16 : px[i * 4] < 240 || px[i * 4 + 1] < 240 || px[i * 4 + 2] < 240;
        var bg = new bool[W * H];
        var st = new Stack<int>();
        void Push(int x, int y) { int i = y * W + x; if (!bg[i] && !Fg(i)) { bg[i] = true; st.Push(i); } }
        for (int x = 0; x < W; x++) { Push(x, 0); Push(x, H - 1); }
        for (int y = 0; y < H; y++) { Push(0, y); Push(W - 1, y); }
        while (st.Count > 0)
        {
            int i = st.Pop(), x = i % W, y = i / W;
            if (x > 0) Push(x - 1, y);
            if (x < W - 1) Push(x + 1, y);
            if (y > 0) Push(x, y - 1);
            if (y < H - 1) Push(x, y + 1);
        }
        var m = new bool[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) m[x, y] = !bg[y * W + x];
        w = W; h = H;
        return m;
    }

    static string ToSvg(PotraceState st, double k)
    {
        var sb = new StringBuilder();
        string P(PotraceDPoint p) => $"{F(p.X * k)} {F(p.Y * k)}";
        for (var p = st.Plist; p != null; p = p.Next)
        {
            var c = p.Curve;
            sb.Append('M').Append(P(c.C[c.N - 1][2]));
            for (int i = 0; i < c.N; i++)
            {
                var a = c.C[i];
                if (c.Tag[i] == PotraceCurve.PotraceCurveTo) sb.Append('C').Append(P(a[0])).Append(' ').Append(P(a[1])).Append(' ').Append(P(a[2]));
                else sb.Append('L').Append(P(a[1])).Append('L').Append(P(a[2]));
            }
            sb.Append('Z');
        }
        return sb.ToString();
    }
}

public static class Nest
{
    public const double Cell = 2;    // mm per sel grid nesting
    public const double Margin = 10; // area tepi kanvas untuk tanda registrasi

    public static bool[,] Rotate(bool[,] g, int rot)
    {
        if (rot == 0) return g;
        int w = g.GetLength(0), h = g.GetLength(1);
        var r = rot == 180 ? new bool[w, h] : new bool[h, w];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (g[x, y])
                    switch (rot)
                    {
                        case 90: r[h - 1 - y, x] = true; break;
                        case 180: r[w - 1 - x, h - 1 - y] = true; break;
                        default: r[y, w - 1 - x] = true; break;
                    }
        return r;
    }

    // ponytail: greedy first-fit pada grid sel 2 mm, 4 rotasi, stiker terbesar dulu. Bukan optimal;
    // ganti dengan NFP/SVGnest bila pemakaian bahan kurang.
    public static List<Placed> Run(IEnumerable<Sticker> stickers, double cw, double ch, double gap, out int failed)
    {
        int cols = (int)((cw - 2 * Margin) / Cell), rows = (int)((ch - 2 * Margin) / Cell), gc = (int)Math.Ceiling(gap / Cell);
        var occ = new bool[Math.Max(cols, 0), Math.Max(rows, 0)];
        var res = new List<Placed>();
        var cache = new Dictionary<(Sticker, int), (int gw, int gh, List<(int, int)> cells)>();
        failed = 0;
        foreach (var s in stickers.SelectMany(s => Enumerable.Repeat(s, s.Copies)).OrderByDescending(s => s.Area))
        {
            (int x, int y, int rot, int score)? best = null;
            foreach (int rot in new[] { 0, 90, 180, 270 })
            {
                if (!cache.TryGetValue((s, rot), out var e))
                {
                    var g = Rotate(s.Grid, rot);
                    var cells = new List<(int, int)>();
                    for (int y = 0; y < g.GetLength(1); y++) for (int x = 0; x < g.GetLength(0); x++) if (g[x, y]) cells.Add((x, y));
                    cache[(s, rot)] = e = (g.GetLength(0), g.GetLength(1), cells);
                }
                for (int y = 0; y + e.gh <= rows; y++)
                {
                    if (best is { } b && y + e.gh >= b.score) break;
                    for (int x = 0; x + e.gw <= cols; x++)
                        if (Fits(occ, e.cells, x, y)) { best = (x, y, rot, y + e.gh); goto found; }
                }
            found:;
            }
            if (best is not { } p) { failed++; continue; }
            foreach (var (cx, cy) in cache[(s, p.rot)].cells)
                for (int dy = -gc; dy <= gc; dy++)
                    for (int dx = -gc; dx <= gc; dx++)
                    {
                        int x = p.x + cx + dx, y = p.y + cy + dy;
                        if (x >= 0 && y >= 0 && x < cols && y < rows) occ[x, y] = true;
                    }
            res.Add(new Placed { S = s, Rot = p.rot, X = Margin + p.x * Cell, Y = Margin + p.y * Cell });
        }
        return res;
    }

    static bool Fits(bool[,] occ, List<(int, int)> cells, int x, int y)
    {
        foreach (var (cx, cy) in cells) if (occ[x + cx, y + cy]) return false;
        return true;
    }

    public static Transform Xf(Placed p)
    {
        var (tx, ty) = p.T;
        return new TransformGroup { Children = { new RotateTransform(p.Rot), new TranslateTransform(p.X + tx, p.Y + ty) } };
    }

    // Satuan kanvas = mm.
    public static Canvas Sheet(IEnumerable<Placed> ps, double cw, double ch, bool marks, Placed? sel)
    {
        var root = new Canvas { Width = cw, Height = ch, Background = Brushes.White, ClipToBounds = true };
        root.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Width = cw - 2 * Margin, Height = ch - 2 * Margin, Stroke = Brushes.LightGray, StrokeThickness = 0.3,
            StrokeDashArray = [4, 4], Margin = new Thickness(Margin, Margin, 0, 0), IsHitTestVisible = false
        });
        if (marks)
            foreach (var (x, y) in Corners(cw, ch))
                root.Children.Add(new System.Windows.Shapes.Rectangle { Width = 5, Height = 5, Fill = Brushes.Black, Margin = new Thickness(x - 2.5, y - 2.5, 0, 0), IsHitTestVisible = false });
        foreach (var p in ps)
        {
            var s = p.S;
            var c = new Canvas { Width = s.W, Height = s.H, Background = Brushes.Transparent, Tag = p, RenderTransform = Xf(p) };
            // artwork di-clip ke garis potongnya sendiri agar tidak menimpa stiker tetangga
            var art = new Canvas { Width = s.W, Height = s.H, Clip = s.CutGeo, IsHitTestVisible = false };
            var img = new System.Windows.Controls.Image { Source = s.Preview, Width = s.ArtW, Height = s.ArtH, Stretch = Stretch.Fill };
            Canvas.SetLeft(img, s.Off);
            Canvas.SetTop(img, s.Off);
            art.Children.Add(img);
            c.Children.Add(art);
            c.Children.Add(new System.Windows.Shapes.Path { Data = s.CutGeo, Stroke = Brushes.Magenta, StrokeThickness = 0.4, IsHitTestVisible = false });
            if (p == sel)
                c.Children.Add(new System.Windows.Shapes.Rectangle { Width = s.W, Height = s.H, Stroke = Brushes.DodgerBlue, StrokeThickness = 0.6, StrokeDashArray = [3, 2], IsHitTestVisible = false });
            root.Children.Add(c);
        }
        return root;
    }

    static IEnumerable<(double, double)> Corners(double cw, double ch)
    {
        double m = Margin / 2;
        return [(m, m), (cw - m, m), (m, ch - m), (cw - m, ch - m)];
    }

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string Tf(Placed p) { var (tx, ty) = p.T; return $"translate({F(p.X + tx)} {F(p.Y + ty)}) rotate({p.Rot})"; }

    // Satu file SVG: layer "Print" (artwork + tanda registrasi) dan layer "CutContour" (garis potong magenta).
    public static string Export(IList<Placed> ps, double cw, double ch, bool marks)
    {
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"{F(cw)}mm\" height=\"{F(ch)}mm\" viewBox=\"0 0 {F(cw)} {F(ch)}\">\n<g id=\"Print\">\n");
        if (marks)
            foreach (var (x, y) in Corners(cw, ch))
                sb.Append($"<rect x=\"{F(x - 2.5)}\" y=\"{F(y - 2.5)}\" width=\"5\" height=\"5\" fill=\"#000000\"/>\n");
        var ids = ps.Select(p => p.S).Distinct().Select((s, i) => (s, id: "c" + i)).ToDictionary(t => t.s, t => t.id);
        sb.Append("<defs>");
        foreach (var (s, id) in ids) sb.Append($"<clipPath id=\"{id}\"><path d=\"{s.CutPath}\"/></clipPath>");
        sb.Append("</defs>\n");
        foreach (var p in ps) sb.Append($"<g transform=\"{Tf(p)}\"><g clip-path=\"url(#{ids[p.S]})\">{Art(p.S)}</g></g>\n");
        sb.Append("</g>\n<g id=\"CutContour\" fill=\"none\" stroke=\"#FF00FF\" stroke-width=\"0.1\">\n");
        foreach (var p in ps) sb.Append($"<path transform=\"{Tf(p)}\" d=\"{p.S.CutPath}\"/>\n");
        return sb.Append("</g>\n</svg>\n").ToString();
    }

    static string Art(Sticker s)
    {
        if (s.ArtSvg == "")
            return $"<image x=\"{F(s.Off)}\" y=\"{F(s.Off)}\" width=\"{F(s.ArtW)}\" height=\"{F(s.ArtH)}\" preserveAspectRatio=\"none\" xlink:href=\"data:image/png;base64,{Convert.ToBase64String(s.ArtPng)}\"/>";
        var e = new XElement(XDocument.Parse(s.ArtSvg).Root!);
        e.SetAttributeValue("x", F(s.Off));
        e.SetAttributeValue("y", F(s.Off));
        e.SetAttributeValue("width", F(s.ArtW));
        e.SetAttributeValue("height", F(s.ArtH));
        e.SetAttributeValue("viewBox", s.ViewBox);
        e.SetAttributeValue("preserveAspectRatio", "none");
        return e.ToString(SaveOptions.DisableFormatting);
    }
}
