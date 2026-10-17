using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BitmapToVector;
using PdfSharp.Pdf.IO;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace StickerLayout;

// Satu garis potong: aktif, jarak dari artwork, dan radius penghalus lekukan (mm).
public record Cut(bool On, double Off, double Smooth);

public class Sticker
{
    public string Name { get; set; } = "";
    public int Copies { get; set; } = 1;
    public double WidthMm { get; set; }          // lebar artwork yang diminta operator (mm)
    public string Source = "";
    public int Page;
    public string? Pdf;                          // PDF yang ditempel vektor saat export (PDF/AI asli atau hasil konversi CDR)
    public byte[] Raster = [];                   // gambar yang ditempel saat export bila bukan vektor
    public byte[]? Edited;                       // PNG hasil "Rapikan gambar"; dipakai menggantikan file asli
    public bool CanClean => Pdf == null;         // vektor (PDF/AI/CDR) tidak dirapikan agar warna CMYK tetap utuh
    public double LoadedW;
    public Cut LoadedKiss = null!, LoadedDie = null!;
    public double W, H;                          // kotak (mm), kelipatan Nest.Cell
    public double Off, ArtW, ArtH;               // posisi & ukuran artwork di dalam kotak (mm)
    public Rect Full;                            // seluruh sumber (halaman PDF / gambar penuh) dalam koordinat kotak (mm)
    public double AreaMm2;                       // luas di dalam garis potong terluar
    public BitmapSource Preview { get; set; } = null!; // artwork yang sudah di-crop, salinan kecil untuk layar
    public int PxW;                                     // lebar artwork dalam piksel asli (untuk dpi)
    public string HText => $"× {ArtH:0.#} mm";        // tinggi artwork di daftar desain
    // Resolusi cetak untuk desain yang ditempel sebagai gambar. Target riset 300 ppi; 150 = batas kasar,
    // sesuaikan setelah melihat hasil cetak Develop.
    public const double MinDpi = 150;
    public double? Dpi => Pdf == null && WidthMm > 0 ? PxW * 25.4 / WidthMm : null;
    public string DpiText => Dpi is { } d ? $"{d:0} dpi" : "";
    public bool LowDpi => Dpi < MinDpi;
    public string Kiss = "", Die = "";           // path SVG garis potong (mm, koordinat kotak)
    public bool[,] Grid = null!;                 // hunian sel nesting dari garis terluar [x,y]
    public int Area;
    Geometry? kg, dg;
    public Geometry? KissGeo => Kiss == "" ? null : kg ??= Freeze(Geometry.Parse(Kiss));
    public Geometry? DieGeo => Die == "" ? null : dg ??= Freeze(Geometry.Parse(Die));
    public Geometry Outer => DieGeo ?? KissGeo!;
    static Geometry Freeze(Geometry g) { g.Freeze(); return g; }
}

public class Placed
{
    public Sticker S = null!;
    public double X, Y;
    public int Rot;
    public bool Bad;                             // tumpang tindih atau keluar margin
    public double BW => Rot % 180 == 0 ? S.W : S.H;
    public double BH => Rot % 180 == 0 ? S.H : S.W;
    public (double x, double y) T => Rot switch { 90 => (S.H, 0), 180 => (S.W, S.H), 270 => (0, S.W), _ => (0, 0) };
}

public static class Loader
{
    public const double PxMm = 6;    // resolusi mask garis potong: 6 px per mm
    // Potrace: tanpa sudut tajam (pisau tidak berbelok mendadak) dan gabung segmen kurva sampai 1 px (0,17 mm).
    // Diukur 2026-10-16 di pipeline ini; kalibrasi ulang setelah uji potong di Graphtec.
    const double AlphaMax = 1.3334, OptTolerance = 1.0;
    const double MaxInward = 0.3;    // penghalusan tidak boleh membawa garis lebih dekat dari (offset - 0,3 mm) ke artwork
    const double ViewPxMm = 10;      // resolusi render PDF untuk layar & deteksi konten
    const double Pt = 72 / 25.4;     // point per mm

    // PDF multi-halaman -> satu desain per halaman. PDF/AI/CDR memakai ukuran asli; PNG/JPG/SVG memakai defWidthMm.
    public static List<Sticker> LoadAll(string path, double defWidthMm, Cut kiss, Cut die)
    {
        var pdf = PdfOf(path);
        int pages = 1;
        if (pdf != null)
            using (var r = new Pdfium(pdf)) pages = r.Pages;
        var res = new List<Sticker>();
        for (int i = 0; i < pages; i++) res.Add(Load(path, i, pdf != null ? 0 : defWidthMm, kiss, die, pages > 1));
        return res;
    }

    static string? PdfOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" or ".ai" => path,
        ".cdr" => Corel.ToPdf(path),
        _ => null,
    };

    // Semua input -> bitmap penuh -> crop ke konten -> mask -> garis kiss/die (dilasi + closing) -> Potrace.
    public static Sticker Load(string path, int page, double widthMm, Cut kiss, Cut die, bool multiPage = false, byte[]? edited = null)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var s = new Sticker { Source = path, Page = page, Name = Path.GetFileName(path) + (multiPage ? $" p{page + 1}" : "") };
        var pdf = edited == null ? PdfOf(path) : null;
        BitmapSource full;
        double nat = double.NaN;                 // mm per px pada ukuran asli (hanya PDF)
        Rect media;                              // area yang digambar saat export, px relatif terhadap bitmap
        if (pdf != null)
        {
            (full, nat, media, bool vector) = RenderPdf(pdf, page);
            if (vector) s.Pdf = pdf;
        }
        else if (edited == null && ext == ".svg") { full = RenderSvg(path, widthMm); s.Raster = Png(full); media = new Rect(0, 0, full.PixelWidth, full.PixelHeight); }
        else if (edited != null || ext is ".png" or ".jpg" or ".jpeg")
        {
            s.Edited = edited;
            s.Raster = edited ?? File.ReadAllBytes(path);
            using var ms = new MemoryStream(s.Raster);
            full = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            media = new Rect(0, 0, full.PixelWidth, full.PixelHeight);
        }
        else throw new NotSupportedException(Lang.T("Unsupported"));

        var (px, fw, fh) = Pixels(full);
        bool alpha = false;
        for (int i = 3; i < px.Length; i += 4) if (px[i] < 250) { alpha = true; break; }
        var bb = ContentBox(px, fw, fh, alpha) ?? throw new InvalidOperationException(Lang.T("Empty"));
        var crop = new CroppedBitmap(full, bb);
        crop.Freeze();
        s.PxW = bb.Width;
        s.Preview = Proxy(crop);
        if (pdf != null && s.Pdf == null) { s.Raster = Png(crop); media = new Rect(bb.X, bb.Y, bb.Width, bb.Height); } // PDF tak terbaca vektor: tempel render-nya

        if (widthMm <= 0) widthMm = Math.Round(bb.Width * nat, 1);
        double k = widthMm / bb.Width;           // mm per px
        s.WidthMm = s.LoadedW = widthMm;
        s.LoadedKiss = kiss;
        s.LoadedDie = die;
        s.ArtW = widthMm;
        s.ArtH = bb.Height * k;

        var small = new TransformedBitmap(crop, new ScaleTransform(s.ArtW * PxMm / bb.Width, s.ArtH * PxMm / bb.Height));
        var mask = Silhouette(small, alpha, out int mw, out int mh);
        static int Reach(Cut c) => c.On ? (int)Math.Ceiling((c.Off + c.Smooth) * PxMm) : 0;
        int pad = Math.Max(Reach(kiss), Reach(die)) + 3, W = mw + 2 * pad, H = mh + 2 * pad;
        var m = new bool[W, H];
        for (int y = 0; y < mh; y++) for (int x = 0; x < mw; x++) m[x + pad, y + pad] = mask[x, y];

        bool[,]? outer = null, kissMask = null;
        if (kiss.On) { outer = kissMask = Solid(Smooth(Close(m, kiss), m, kiss)); s.Kiss = Trace(outer); }
        if (die.On)
        {
            outer = Smooth(Close(m, die), m, die);
            if (kissMask != null) foreach (var (x, y) in Cells(outer)) outer[x, y] |= kissMask[x, y]; // die selalu membungkus kiss
            outer = Solid(outer);
            s.Die = Trace(outer);
        }
        if (outer == null) throw new InvalidOperationException(Lang.T("NeedCut"));

        s.Off = pad / PxMm;
        s.W = Math.Ceiling(W / PxMm / Nest.Cell) * Nest.Cell;
        s.H = Math.Ceiling(H / PxMm / Nest.Cell) * Nest.Cell;
        int cp = (int)Math.Round(Nest.Cell * PxMm), n = 0;
        s.Grid = new bool[(int)Math.Round(s.W / Nest.Cell), (int)Math.Round(s.H / Nest.Cell)];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (outer[x, y]) { s.Grid[x / cp, y / cp] = true; n++; }
        foreach (bool c in s.Grid) if (c) s.Area++;
        s.AreaMm2 = n / (PxMm * PxMm);
        s.Full = new Rect(s.Off + (media.X - bb.X) * k, s.Off + (media.Y - bb.Y) * k, media.Width * k, media.Height * k);
        return s;
    }

    // PDFium merender CropBox; PDFsharp menempel MediaBox. Kembalikan posisi MediaBox relatif terhadap render.
    static (BitmapSource bmp, double nat, Rect media, bool vector) RenderPdf(string pdf, int page)
    {
        BitmapSource bmp;
        double pxMm, scale;
        using (var r = new Pdfium(pdf))
        {
            var (wPt, hPt) = r.SizePt(page);
            pxMm = Math.Min(ViewPxMm, 4000 / (Math.Max(wPt, hPt) / Pt)); // batas 4000 px agar memori tetap kecil
            scale = pxMm / Pt;
            bmp = r.Render(page, scale);
        }
        try
        {
            using var doc = PdfReader.Open(pdf, PdfDocumentOpenMode.Import);
            var pg = doc.Pages[page];
            if (pg.Rotate % 360 == 0)
            {
                var mb = pg.MediaBox;
                var cb = pg.EffectiveCropBoxReadOnly;
                return (bmp, 1 / pxMm, new Rect((mb.X1 - cb.X1) * scale, (cb.Y2 - mb.Y2) * scale, mb.Width * scale, mb.Height * scale), true);
            }
        }
        catch (Exception) { } // terenkripsi / tidak terbaca PDFsharp: tempel sebagai gambar
        return (bmp, 1 / pxMm, default, false);
    }

    // SVG jarang dipakai: dirender ±300 dpi sesuai lebar target, lalu diperlakukan seperti PNG.
    static BitmapSource RenderSvg(string path, double widthMm)
    {
        var d = new FileSvgReader(new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true }).Read(path);
        var b = d.Bounds;
        double k = Math.Min(widthMm * 12 / b.Width, 8000 / Math.Max(b.Width, b.Height));
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(k, k));
            dc.PushTransform(new TranslateTransform(-b.X, -b.Y));
            dc.DrawDrawing(d);
        }
        var rt = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(b.Width * k)), Math.Max(1, (int)Math.Ceiling(b.Height * k)), 96, 96, PixelFormats.Pbgra32);
        rt.Render(v);
        rt.Freeze();
        return rt;
    }

    // Layar memakai salinan kecil (sisi panjang maks 1600 px): RAM dan render tetap ringan untuk gambar besar.
    // Export dan garis potong tetap memakai resolusi asli.
    const int ViewMax = 1600;

    static BitmapSource Proxy(BitmapSource b)
    {
        double k = (double)ViewMax / Math.Max(b.PixelWidth, b.PixelHeight);
        if (k >= 1) return b;
        var w = new WriteableBitmap(new TransformedBitmap(b, new ScaleTransform(k, k))); // salinan: gambar asli bisa dibuang
        w.Freeze();
        return w;
    }

    public static byte[] Png(BitmapSource b)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(b));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    static (byte[] px, int w, int h) Pixels(BitmapSource b)
    {
        var f = new FormatConvertedBitmap(b, PixelFormats.Bgra32, null, 0);
        var px = new byte[f.PixelWidth * f.PixelHeight * 4];
        f.CopyPixels(px, f.PixelWidth * 4, 0);
        return (px, f.PixelWidth, f.PixelHeight);
    }

    // Foreground = alpha (jika ada transparansi) atau "bukan putih".
    static bool Fg(byte[] px, int i, bool alpha) =>
        alpha ? px[i * 4 + 3] > 16 : px[i * 4] < 240 || px[i * 4 + 1] < 240 || px[i * 4 + 2] < 240;

    static Int32Rect? ContentBox(byte[] px, int w, int h, bool alpha)
    {
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (Fg(px, y * w + x, alpha)) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        return x1 < 0 ? null : new Int32Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    static bool[,] Silhouette(BitmapSource b, bool alpha, out int w, out int h)
    {
        var (px, W, H) = Pixels(b);
        var m = new bool[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) m[x, y] = Fg(px, y * W + x, alpha);
        w = W; h = H;
        return Solid(m);
    }

    // Lubang (latar yang tidak terhubung ke tepi) diisi: hanya kontur luar yang dipotong.
    // Berlaku juga untuk lubang baru yang terbentuk saat offset menutup celah artwork (huruf C besar, teks melingkar).
    static bool[,] Solid(bool[,] m)
    {
        int W = m.GetLength(0), H = m.GetLength(1);
        var bg = new bool[W, H];
        var st = new Stack<(int, int)>();
        void Push(int x, int y) { if (x >= 0 && y >= 0 && x < W && y < H && !bg[x, y] && !m[x, y]) { bg[x, y] = true; st.Push((x, y)); } }
        for (int x = 0; x < W; x++) { Push(x, 0); Push(x, H - 1); }
        for (int y = 0; y < H; y++) { Push(0, y); Push(W - 1, y); }
        while (st.Count > 0) { var (x, y) = st.Pop(); Push(x - 1, y); Push(x + 1, y); Push(x, y - 1); Push(x, y + 1); }
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) bg[x, y] = !bg[x, y];
        return bg;
    }

    // Offset + penghalus: dilasi (offset + halus), lalu erosi (halus) = closing yang menutup lekukan sempit.
    static bool[,] Close(bool[,] m, Cut c)
    {
        int r = (int)Math.Round(c.Off * PxMm), sm = (int)Math.Round(c.Smooth * PxMm);
        var d = Dilate(m, r + sm);
        return sm > 0 ? Not(Dilate(Not(d), sm)) : d;
    }

    // Haluskan kontur: blur Gaussian (sigma = Halus) lalu threshold 0,5. Lekukan kecil hilang, node berkurang,
    // pisau bergerak mulus. Hasil di-OR dengan artwork + (offset - 0,3 mm) supaya garis tidak mendekati artwork.
    static bool[,] Smooth(bool[,] m, bool[,] art, Cut c)
    {
        double s = c.Smooth * PxMm;
        if (s <= 0) return m;
        int W = m.GetLength(0), H = m.GetLength(1);
        var a = new double[W * H]; // urutan [x * H + y], sama seperti bool[,]
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (m[x, y]) a[x * H + y] = 1;
        // Gaussian didekati 3 lintasan box blur (O(1) per piksel): varians 3 * (w^2 - 1) / 12 = s^2.
        int r = Math.Max(1, (int)Math.Round((Math.Sqrt(4 * s * s + 1) - 1) / 2));
        var b = new double[a.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            Box(a, b, W, H, r, H, 1);   // sepanjang y
            Box(b, a, H, W, r, 1, H);   // sepanjang x
        }
        var res = Dilate(art, (int)Math.Floor((c.Off - MaxInward) * PxMm));
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (a[x * H + y] >= 0.5) res[x, y] = true;
        return res;
    }

    // Rata-rata berjalan lebar 2r+1 untuk `lines` baris berisi `len` sel; indeks = baris * rowStep + i * step.
    // Di luar gambar dihitung 0, sama seperti mask kosong.
    static void Box(double[] src, double[] dst, int lines, int len, int r, int rowStep, int step)
    {
        double k = 1.0 / (2 * r + 1);
        for (int l = 0; l < lines; l++)
        {
            int o = l * rowStep;
            double sum = 0;
            for (int i = 0; i <= Math.Min(r, len - 1); i++) sum += src[o + i * step];
            for (int i = 0; i < len; i++)
            {
                dst[o + i * step] = sum * k;
                if (i + r + 1 < len) sum += src[o + (i + r + 1) * step];
                if (i - r >= 0) sum -= src[o + (i - r) * step];
            }
        }
    }

    static IEnumerable<(int, int)> Cells(bool[,] m)
    {
        for (int x = 0; x < m.GetLength(0); x++) for (int y = 0; y < m.GetLength(1); y++) yield return (x, y);
    }

    static bool[,] Not(bool[,] m)
    {
        var n = new bool[m.GetLength(0), m.GetLength(1)];
        for (int x = 0; x < m.GetLength(0); x++) for (int y = 0; y < m.GetLength(1); y++) n[x, y] = !m[x, y];
        return n;
    }

    static bool[,] Dilate(bool[,] m, int r)
    {
        int W = m.GetLength(0), H = m.GetLength(1);
        var d = (bool[,])m.Clone();
        if (r <= 0) return d;
        var disc = new List<(int, int)>();
        for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++) if (dx * dx + dy * dy <= r * r) disc.Add((dx, dy));
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (!m[x, y]) continue;
                bool edge = x == 0 || y == 0 || x == W - 1 || y == H - 1 || !m[x - 1, y] || !m[x + 1, y] || !m[x, y - 1] || !m[x, y + 1];
                if (!edge) continue;
                foreach (var (dx, dy) in disc)
                {
                    int u = x + dx, v = y + dy;
                    if (u >= 0 && v >= 0 && u < W && v < H) d[u, v] = true;
                }
            }
        return d;
    }

    static string Trace(bool[,] m)
    {
        int W = m.GetLength(0), H = m.GetLength(1);
        using var pb = PotraceBitmap.Create(W, H);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (m[x, y]) pb.SetBlack(x, y);
        var st = Potrace.Trace(new PotraceParam { AlphaMax = AlphaMax, OptTolerance = OptTolerance }, pb);
        var sb = new StringBuilder();
        string P(PotraceDPoint p) => $"{F(p.X / PxMm)} {F(p.Y / PxMm)}";
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

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}

// CDR dibuka lewat CorelDRAW yang terpasang di PC operator, lalu di-publish ke PDF.
// COM berjalan di proses worker (exe ini dengan --cdr2pdf). Bila CorelDRAW menggantung (misalnya ada dialog),
// worker dihentikan setelah batas waktu dan Printech tetap jalan. CorelDRAW sendiri tidak dimatikan,
// jadi pekerjaan operator yang belum disimpan tetap aman.
// ponytail: belum diuji dengan CorelDRAW asli (tidak ada di PC pengembangan); batas 30 detik dikalibrasi di PC operator.
public static class Corel
{
    public const int TimeoutMs = 30_000;
    static readonly ConcurrentDictionary<string, string> cache = new();

    public static string ToPdf(string cdr, int timeoutMs = TimeoutMs)
    {
        if (cache.TryGetValue(cdr, out var done) && File.Exists(done)) return done;
        var pdf = Path.Combine(App.Temp, Guid.NewGuid() + ".pdf");
        Directory.CreateDirectory(App.Temp);
        var psi = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var a in new[] { "--cdr2pdf", cdr, pdf }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutMs))
        {
            p.Kill();
            p.WaitForExit();
            throw new TimeoutException(Lang.T("CorelTimeout", timeoutMs / 1000));
        }
        return p.ExitCode switch
        {
            0 when File.Exists(pdf) => cache[cdr] = pdf,
            2 => throw new InvalidOperationException(Lang.T("NoCorel")),
            _ => throw new InvalidOperationException(Lang.T("CorelFail", err.Result.Trim())),
        };
    }

    // Dijalankan di proses worker (main thread STA). Exit code: 0 = berhasil, 2 = CorelDRAW tidak terpasang,
    // 1 = error lain (pesan di stderr).
    public static int Worker(string cdr, string pdf)
    {
        var t = Type.GetTypeFromProgID("CorelDRAW.Application");
        if (t == null) return 2;
        try
        {
            dynamic app = Activator.CreateInstance(t)!;
            dynamic doc = app.OpenDocument(cdr);
            try { doc.PublishToPDF(pdf); }
            finally { doc.Close(); }
            return 0;
        }
        catch (Exception ex) { Console.Error.Write(ex.Message); return 1; }
    }
}

public static class Paper
{
    public static readonly Dictionary<string, (double W, double H)> Sizes = new()
    {
        ["A0"] = (841, 1189), ["A1"] = (594, 841), ["A2"] = (420, 594), ["A3"] = (297, 420), ["A3+"] = (320, 480), ["A4"] = (210, 297),
        ["A5"] = (148, 210), ["A6"] = (105, 148), ["SRA3"] = (320, 450), ["SRA4"] = (225, 320),
    };
}

public static class Nest
{
    // mm per sel grid nesting. Diukur di SelfCheck (130 stiker, A4): 1 mm ±5× lebih lambat, jumlah lembar sama.
    public static double Cell = 2;
    static readonly int[] Rots = [0, 90, 180, 270];

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

    // ponytail: greedy first-fit di grid sel, 4 rotasi, lintas lembar, 3 urutan dicoba. Bukan optimal;
    // ganti dengan NFP/SVGnest bila pemakaian bahan kurang. Masih di UI thread (±50 ms untuk 100 stiker);
    // pindah ke background bila operator merasakan jeda.
    public static List<List<Placed>> Run(IEnumerable<Sticker> stickers, double cw, double ch, double margin, double gap, out int tooBig)
    {
        var items = stickers.SelectMany(s => Enumerable.Repeat(s, Math.Max(0, s.Copies))).ToList();
        Func<Sticker, double>[] keys = [s => s.Area, s => Math.Max(s.W, s.H), s => s.W + s.H];
        // 3 percobaan dihitung paralel (kerja CPU). Once hanya membaca Sticker dan memakai cache sendiri.
        var trials = new (List<List<Placed>> r, int big)[keys.Length];
        Parallel.For(0, keys.Length, i => trials[i] = (Once(items.OrderByDescending(keys[i]).ToList(), cw, ch, margin, gap, out int big), big));
        // Dipilih menurut urutan percobaan, bukan urutan selesai: hasil sama setiap kali.
        List<List<Placed>> best = [];
        double bestScore = double.MaxValue;
        tooBig = 0;
        foreach (var (r, big) in trials)
        {
            double score = r.Count * 1e6 + (r.Count == 0 ? 0 : r[^1].Max(p => p.Y + p.BH));
            if (score < bestScore) { best = r; bestScore = score; tooBig = big; }
        }
        return best;
    }

    static List<List<Placed>> Once(List<Sticker> items, double cw, double ch, double margin, double gap, out int tooBig)
    {
        int cols = Math.Max(0, (int)((cw - 2 * margin) / Cell)), rows = Math.Max(0, (int)((ch - 2 * margin) / Cell));
        int gc = (int)Math.Ceiling(gap / Cell - 1e-9);
        var sheets = new List<(bool[,] occ, List<Placed> ps, HashSet<Sticker> full)>();
        var cache = new Dictionary<(Sticker, int), (int gw, int gh, List<(int, int)> cells)>();
        tooBig = 0;
        foreach (var s in items)
        {
            bool done = false;
            foreach (var sh in sheets)
            {
                if (sh.full.Contains(s)) continue; // salinan sebelumnya sudah tidak muat di lembar ini
                if (TryPlace(sh.occ, s, cache, gc, margin, sh.ps)) { done = true; break; }
                sh.full.Add(s);
            }
            if (done) continue;
            var occ = new bool[cols, rows];
            var ps = new List<Placed>();
            if (TryPlace(occ, s, cache, gc, margin, ps)) sheets.Add((occ, ps, []));
            else tooBig++;
        }
        return sheets.Select(x => x.ps).ToList();
    }

    static bool TryPlace(bool[,] occ, Sticker s, Dictionary<(Sticker, int), (int gw, int gh, List<(int, int)> cells)> cache, int gc, double margin, List<Placed> ps)
    {
        int cols = occ.GetLength(0), rows = occ.GetLength(1);
        (int x, int y, int rot, int score)? best = null;
        foreach (int rot in Rots)
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
                    if (Fits(occ, e.cells, x, y)) { best = (x, y, rot, y + e.gh); goto next; }
            }
        next:;
        }
        if (best is not { } p) return false;
        foreach (var (cx, cy) in cache[(s, p.rot)].cells)
            for (int dy = -gc; dy <= gc; dy++)
                for (int dx = -gc; dx <= gc; dx++)
                {
                    int x = p.x + cx + dx, y = p.y + cy + dy;
                    if (x >= 0 && y >= 0 && x < cols && y < rows) occ[x, y] = true;
                }
        ps.Add(new Placed { S = s, Rot = p.rot, X = margin + p.x * Cell, Y = margin + p.y * Cell });
        return true;
    }

    static bool Fits(bool[,] occ, List<(int, int)> cells, int x, int y)
    {
        foreach (var (cx, cy) in cells) if (occ[x + cx, y + cy]) return false;
        return true;
    }

    // Tandai stiker yang tumpang tindih (jejak garis terluar) atau keluar margin. Kembalikan jumlahnya.
    public static int Check(List<Placed> ps, double cw, double ch, double margin)
    {
        var owner = new Dictionary<(int, int), Placed>();
        foreach (var p in ps)
            p.Bad = p.X < margin - 0.01 || p.Y < margin - 0.01 || p.X + p.BW > cw - margin + 0.01 || p.Y + p.BH > ch - margin + 0.01;
        foreach (var p in ps)
        {
            var g = Rotate(p.S.Grid, p.Rot);
            int ox = (int)Math.Round(p.X / Cell), oy = (int)Math.Round(p.Y / Cell);
            for (int x = 0; x < g.GetLength(0); x++)
                for (int y = 0; y < g.GetLength(1); y++)
                    if (g[x, y])
                    {
                        if (owner.TryGetValue((ox + x, oy + y), out var o) && o != p) o.Bad = p.Bad = true;
                        else owner[(ox + x, oy + y)] = p;
                    }
        }
        return ps.Count(p => p.Bad);
    }

    public static Transform Xf(Placed p)
    {
        var (tx, ty) = p.T;
        return new TransformGroup { Children = { new RotateTransform(p.Rot), new TranslateTransform(p.X + tx, p.Y + ty) } };
    }

    static readonly Brush KissBrush = Frozen("#EC008C"), DieBrush = Frozen("#00AEEF"), SelBrush = Frozen("#4A90FF");
    static Brush Frozen(string c) { var b = (Brush)new BrushConverter().ConvertFromString(c)!; b.Freeze(); return b; }

    // Satuan kanvas = mm. Tag kanvas = indeks lembar, Tag stiker = Placed.
    public static Canvas Sheet(IEnumerable<Placed> ps, double cw, double ch, double margin, Placed? sel, int index)
    {
        var root = new Canvas { Width = cw, Height = ch, Background = Brushes.White, ClipToBounds = true, Tag = index };
        root.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Width = Math.Max(0, cw - 2 * margin), Height = Math.Max(0, ch - 2 * margin), Stroke = Brushes.LightGray, StrokeThickness = 0.3,
            StrokeDashArray = [4, 4], Margin = new Thickness(margin, margin, 0, 0), IsHitTestVisible = false
        });
        foreach (var p in ps)
        {
            var s = p.S;
            var c = new Canvas { Width = s.W, Height = s.H, Background = Brushes.Transparent, Tag = p, RenderTransform = Xf(p) };
            // artwork di-clip ke garis potongnya sendiri agar tidak menimpa stiker tetangga
            var art = new Canvas { Width = s.W, Height = s.H, Clip = s.Outer, IsHitTestVisible = false };
            var img = new Image { Source = s.Preview, Width = s.ArtW, Height = s.ArtH, Stretch = Stretch.Fill };
            Canvas.SetLeft(img, s.Off);
            Canvas.SetTop(img, s.Off);
            art.Children.Add(img);
            c.Children.Add(art);
            if (s.KissGeo is { } k) c.Children.Add(new System.Windows.Shapes.Path { Data = k, Stroke = KissBrush, StrokeThickness = 0.4, IsHitTestVisible = false });
            if (s.DieGeo is { } d) c.Children.Add(new System.Windows.Shapes.Path { Data = d, Stroke = DieBrush, StrokeThickness = 0.4, IsHitTestVisible = false });
            if (p.Bad || p == sel)
                c.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Width = s.W, Height = s.H, Stroke = p.Bad ? Brushes.Red : SelBrush, StrokeThickness = p.Bad ? 0.8 : 0.6,
                    StrokeDashArray = p.Bad ? null : [3, 2], IsHitTestVisible = false
                });
            root.Children.Add(c);
        }
        return root;
    }
}
