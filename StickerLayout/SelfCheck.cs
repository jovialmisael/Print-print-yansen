using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace StickerLayout;

// Jalankan: StickerLayout.exe --selfcheck <folder>. Membuat sampel, memuat, menyusun, mengekspor PDF, lalu memeriksa hasilnya.
public static class SelfCheck
{
    const double A4W = 210, A4H = 297, Margin = 10, Gap = 2;

    public static int Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var log = new List<string>();
        try
        {
            Samples(dir);
            Cut kiss = new(true, 2, 1), die = new(true, 4, 2);
            var files = new[] { ("circle.png", 6), ("star.jpg", 6), ("badge.svg", 4), ("logo.pdf", 6), ("logo.ai", 4) };
            List<Sticker> Load() => files.SelectMany(f => Loader.LoadAll(Path.Combine(dir, f.Item1), 40, kiss, die)
                .Select(s => { s.Copies = f.Item2; return s; })).ToList();

            // Ukur sel grid 2 mm vs 1 mm pada job ±130 stiker (waktu vs pemakaian bahan).
            double cell0 = Nest.Cell;
            foreach (double cell in new[] { 2.0, 1.0 })
            {
                Nest.Cell = cell;
                var big = Load();
                foreach (var s in big) s.Copies *= 5;
                var sw = Stopwatch.StartNew();
                var r = Nest.Run(big, A4W, A4H, Margin, Gap, out _);
                log.Add($"cell {cell} mm: {big.Sum(s => s.Copies)} stiker -> {r.Count} lembar A4, pemakaian {Use(r):P1}, {sw.ElapsedMilliseconds} ms");
            }
            Nest.Cell = cell0;

            long mem0 = GC.GetTotalMemory(true);
            var items = Load();
            log.Add($"memori: managed +{(GC.GetTotalMemory(true) - mem0) / 1048576.0:F1} MB untuk {items.Count} desain, total managed {GC.GetTotalMemory(true) / 1048576.0:F0} MB, working set {Environment.WorkingSet / 1048576} MB");
            foreach (var s in items)
            {
                Check(s.Kiss.StartsWith('M') && s.Die.StartsWith('M'), $"{s.Name}: garis potong kosong");
                Check(s.DieGeo!.GetArea() > s.KissGeo!.GetArea(), $"{s.Name}: die-cut tidak membungkus kiss-cut");
                log.Add($"{s.Name}: artwork {s.ArtW:F1}x{s.ArtH:F1} mm, kotak {s.W}x{s.H} mm, vektor={s.Pdf != null}");
            }
            foreach (var f in new[] { "circle.png", "star.jpg", "badge.svg", "logo.pdf", "rough.jpg" })
            {
                var nsw = Stopwatch.StartNew();
                var ns = Loader.Load(Path.Combine(dir, f), 0, f.EndsWith(".pdf") ? 0 : 40, kiss, die);
                log.Add($"node {f}: kiss {Nodes(ns.Kiss)} ({Corners(ns.Kiss)} sudut), die {Nodes(ns.Die)} ({Corners(ns.Die)} sudut), {nsw.ElapsedMilliseconds} ms");
            }

            var pdfItem = items.First(s => s.Name == "logo.pdf");
            Check(pdfItem.Pdf != null, "logo.pdf tidak dipakai sebagai vektor");
            // isi PDF: lebar 140 pt (49,39 mm), tinggi 120 pt (42,33 mm), kotak merah di luar CropBox tidak boleh ikut
            Check(Math.Abs(pdfItem.ArtW - 49.39) < 0.6 && Math.Abs(pdfItem.ArtH - 42.33) < 0.6, $"ukuran asli PDF salah: {pdfItem.ArtW:F2}x{pdfItem.ArtH:F2}");
            Check(items.First(s => s.Name == "logo.ai").Pdf != null, "logo.ai tidak dibaca sebagai PDF");

            // Rapikan gambar: logo oranye + teks hijau di latar biru (JPG, tanpa alpha).
            var blueJpg = Path.Combine(dir, "logo-on-blue.jpg");
            var rawBlue = Loader.Load(blueJpg, 0, 40, kiss, new Cut(false, 0, 0));
            var ci = new CleanImage(File.ReadAllBytes(blueJpg));
            Check(ci.BorderColor() is { } bc && bc.B > 150 && bc.R < 80, $"warna latar salah: {ci.BorderColor()}");
            int full0 = ci.OpaqueCount(0);
            Check(ci.EraseFromBorder(32) > 0, "hapus latar tidak menghapus apa pun");
            int afterBg = ci.OpaqueCount(0);
            Check(ci.EraseAt(150, 286, 32) > 0, "penghapus klik tidak menghapus teks");
            int afterText = ci.OpaqueCount(0);
            Check(ci.Undo() && ci.OpaqueCount(0) == afterBg, "undo tidak mengembalikan teks");
            Check(ci.EraseAt(150, 286, 32) > 0 && ci.OpaqueCount(0) == afterText, "hapus teks ulang gagal");
            Check(ci.OpaqueCount(2) < afterText, "kikis tepi tidak mengurangi piksel");
            var cleaned = Loader.Load(blueJpg, 0, 40, kiss, new Cut(false, 0, 0), edited: ci.ToPng(1));
            Check(cleaned.Edited != null && cleaned.Raster == cleaned.Edited, "hasil rapikan tidak dipakai untuk export");
            Check(Math.Abs(cleaned.ArtW - cleaned.ArtH) < 1, $"teks masih ikut: {cleaned.ArtW:F1}x{cleaned.ArtH:F1} mm");
            Check(cleaned.KissGeo!.GetArea() < rawBlue.KissGeo!.GetArea() * 0.85, "garis potong tidak mengikuti lingkaran");
            log.Add($"rapikan: piksel opak {full0} -> latar {afterBg} -> teks {afterText}; luas kiss {rawBlue.KissGeo!.GetArea():F0} -> {cleaned.KissGeo!.GetArea():F0} mm2");
            var cs = new CleanImage(File.ReadAllBytes(Path.Combine(dir, "star.jpg")));
            cs.EraseFromBorder(32);
            Check(cs.Alpha(0)[155 * cs.W + 150] > 16, "hapus latar ikut menghapus lubang di tengah bintang");

            // CDR lewat proses worker: tanpa CorelDRAW muncul pesan NoCorel dan aplikasi tidak crash;
            // worker yang melewati batas waktu dihentikan paksa.
            var fakeCdr = Path.Combine(dir, "tidak-ada.cdr");
            var cdrSw = Stopwatch.StartNew();
            string cdrMsg = "dilewati (CorelDRAW terpasang)";
            if (Type.GetTypeFromProgID("CorelDRAW.Application") == null)
            {
                try { Corel.ToPdf(fakeCdr); cdrMsg = "tanpa error"; } catch (InvalidOperationException ex) { cdrMsg = ex.Message; }
                Check(cdrMsg == Lang.T("NoCorel"), "worker CDR tanpa CorelDRAW: " + cdrMsg);
            }
            long noCorelMs = cdrSw.ElapsedMilliseconds;
            bool killed = false;
            cdrSw.Restart();
            try { Corel.ToPdf(fakeCdr, timeoutMs: 1); } catch (TimeoutException) { killed = true; }
            Check(killed, "worker CDR tidak dihentikan setelah batas waktu");
            log.Add($"cdr worker: {cdrMsg} ({noCorelMs} ms); batas 1 ms -> dihentikan ({cdrSw.ElapsedMilliseconds} ms)");

            // A3+ = kertas stiker Develop (AP 55).
            Check(Paper.Sizes.TryGetValue("A3+", out var a3p) && a3p == (320, 480), "preset A3+ 320x480 tidak ada");
            // DPI hanya untuk desain yang ditempel sebagai gambar; vektor tidak punya DPI.
            var circle = items.First(s => s.Name == "circle.png");
            Check(circle.Dpi is { } cd && Math.Abs(cd - circle.PxW * 25.4 / 40) < 0.01 && !circle.LowDpi, $"DPI circle salah: {circle.Dpi}");
            Check(pdfItem.Dpi == null && !pdfItem.LowDpi, "PDF vektor tidak boleh punya DPI");
            var wide = Loader.Load(Path.Combine(dir, "circle.png"), 0, 80, kiss, die);
            Check(wide.LowDpi, $"circle 80 mm ({wide.Dpi:0} dpi) harus ditandai resolusi rendah");
            log.Add($"dpi: circle 40 mm = {circle.Dpi:0} dpi, 80 mm = {wide.Dpi:0} dpi (rendah)");

            // Skenario uji garis potong S1-S9 dan S11 (docs/UJI-GARIS-POTONG.md). S10 dan bagian CorelDRAW di S8 diuji operator.
            var fails = new List<string>();
            void S(int n, string title, bool ok, string detail)
            {
                log.Add($"S{n} {(ok ? "LULUS" : "GAGAL")}: {title} | {detail}");
                if (!ok) fails.Add($"S{n}");
            }
            var none = new Cut(false, 0, 0);
            Sticker FromFile(string f, double w, Cut k, Cut d) => Loader.Load(Path.Combine(dir, f), 0, w, k, d);
            Sticker Png(BitmapSource b, double w, Cut k, Cut d) => Loader.Load("uji.png", 0, w, k, d, edited: Loader.Png(b));

            // S1: logo JPG bertepi kasar.
            var rough0 = FromFile("rough.jpg", 40, new Cut(true, 2, 0), new Cut(true, 4, 0));
            var rough1 = FromFile("rough.jpg", 40, kiss, die);
            double cutK = 1 - (double)Nodes(rough1.Kiss) / Nodes(rough0.Kiss), cutD = 1 - (double)Nodes(rough1.Die) / Nodes(rough0.Die);
            S(1, "logo JPG kasar: node turun >= 60%, tanpa sudut tajam", cutK >= 0.6 && cutD >= 0.6 && Corners(rough1.Kiss) + Corners(rough1.Die) == 0,
                $"kiss {Nodes(rough0.Kiss)} -> {Nodes(rough1.Kiss)} ({cutK:P0}), die {Nodes(rough0.Die)} -> {Nodes(rough1.Die)} ({cutD:P0})");

            // S2: lingkaran 40 mm + kiss 2 mm tetap bulat, ukuran benar, node minimal.
            var circle40 = items.First(s => s.Name == "circle.png");
            var cb = circle40.KissGeo!.Bounds;
            double cExp = circle40.ArtW + 2 * kiss.Off;
            S(2, "lingkaran tetap bulat dan node minimal", Nodes(circle40.Kiss) <= 4 && Math.Abs(cb.Width - cb.Height) < 0.3 && Math.Abs(cb.Width - cExp) < 0.5,
                $"{Nodes(circle40.Kiss)} node, {cb.Width:F2} x {cb.Height:F2} mm (harapan {cExp:F1})");

            // S3: teks tebal. Node turun, tanpa sudut, huruf tidak terpotong.
            var text = Draw(dc => dc.DrawText(new FormattedText("PRINTECH", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 100, Brushes.Black, 1.0), new Point(10, 10)), 620, 140);
            var tArt = Png(text, 60, new Cut(true, 0, 0), none);
            var t0 = Png(text, 60, new Cut(true, 2, 0), none);
            var t1 = Png(text, 60, kiss, none);
            double tOut = Outside(tArt, t1);
            S(3, "teks: node turun, tanpa sudut tajam, huruf tidak terpotong", Nodes(t1.Kiss) < Nodes(t0.Kiss) && Corners(t1.Kiss) == 0 && tOut < 0.5,
                $"halus 0 = {Nodes(t0.Kiss)} node, halus 1 = {Nodes(t1.Kiss)} node, artwork di luar garis {tOut:F2} mm2");

            // S4: offset kecil (0,5 mm) dan nol pada bintang (ujung lancip): garis tidak masuk artwork.
            var sArt = FromFile("star.jpg", 40, new Cut(true, 0, 0), none);
            double o05 = Outside(sArt, FromFile("star.jpg", 40, new Cut(true, 0.5, 1), none)), o0 = Outside(sArt, FromFile("star.jpg", 40, new Cut(true, 0, 1), none));
            S(4, "offset 0,5 mm dan 0 mm: garis tidak masuk artwork", o05 < 0.5 && o0 < 3,
                $"artwork di luar garis: offset 0,5 = {o05:F2} mm2, offset 0 = {o0:F2} mm2");

            // S5: berbagai nilai Halus. Semua jalan, semua lebih sedikit node daripada Halus 0.
            var sweep = new[] { 0, 0.5, 1, 2, 4 }.Select(h => Nodes(FromFile("rough.jpg", 40, new Cut(true, 2, h), none).Kiss)).ToArray();
            S(5, "halus 0 / 0,5 / 1 / 2 / 4 mm: node selalu di bawah halus 0", sweep.Skip(1).All(n => n < sweep[0]), string.Join(" / ", sweep) + " node");

            // S6: kiss selalu di dalam die, termasuk pengaturan tidak biasa (kiss halus 3, die halus 0).
            var odd = new[] { FromFile("rough.jpg", 40, new Cut(true, 2, 3), new Cut(true, 4, 0)), FromFile("star.jpg", 40, new Cut(true, 2, 3), new Cut(true, 4, 0)),
                Png(text, 60, new Cut(true, 2, 3), new Cut(true, 4, 0)) };
            double worst = items.Append(rough1).Concat(odd).Max(s => Geometry.Combine(s.KissGeo, s.DieGeo, GeometryCombineMode.Exclude, null).GetArea());
            S(6, "kiss selalu di dalam die", worst < 1, $"luas kiss di luar die maks {worst:F2} mm2 dari {items.Count + 4} desain");

            // S7: dua bagian artwork berdekatan (celah 3-3,9 mm) tidak pecah jadi dua stiker oleh penghalusan.
            var split = new List<string>();
            foreach (double gap in new[] { 3.0, 3.3, 3.6, 3.9 })
            {
                var duo = Draw(dc => { dc.DrawEllipse(Brushes.Black, null, new Point(100, 100), 100, 100); dc.DrawEllipse(Brushes.Black, null, new Point(300 + gap * 10, 100), 100, 100); }, (int)(400 + gap * 10), 200);
                int c0 = Png(duo, 40 + gap, new Cut(true, 2, 0), none).Kiss.Count(ch => ch == 'M'), c1 = Png(duo, 40 + gap, kiss, none).Kiss.Count(ch => ch == 'M');
                if (c1 > c0) split.Add($"celah {gap} mm: {c0} -> {c1} kontur");
            }
            S(7, "dua bagian berdekatan tidak pecah", split.Count == 0, split.Count == 0 ? "celah 3,0 / 3,3 / 3,6 / 3,9 mm: kontur tidak bertambah" : string.Join("; ", split));

            // S11: hanya kontur luar. Offset menutup celah cincin "C" dan titik melingkar; lubang yang terbentuk tidak boleh ikut dipotong.
            var cRing = Draw(dc =>
            {
                double a = 5.4 * Math.PI / 180;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(150 + 120 * Math.Cos(a), 150 + 120 * Math.Sin(a)), false, false);
                    ctx.ArcTo(new Point(150 + 120 * Math.Cos(a), 150 - 120 * Math.Sin(a)), new Size(120, 120), 0, true, SweepDirection.Clockwise, true, false);
                }
                dc.DrawGeometry(null, new Pen(Brushes.Black, 40), g);
            });
            var dots = Draw(dc =>
            {
                for (int i = 0; i < 12; i++) dc.DrawEllipse(Brushes.Black, null, new Point(150 + 110 * Math.Cos(i * Math.PI / 6), 150 + 110 * Math.Sin(i * Math.PI / 6)), 22, 22);
                dc.DrawEllipse(Brushes.Black, null, new Point(150, 150), 40, 40);
            });
            var holes = new List<string>();
            bool solid = true;
            foreach (var (name, img) in new[] { ("cincin C", cRing), ("titik melingkar + tengah", dots) })
            {
                var hs = Png(img, 40, kiss, die);
                int kissN = hs.Kiss.Count(ch => ch == 'M'), dieN = hs.Die.Count(ch => ch == 'M');
                solid &= kissN == 1 && dieN == 1;
                holes.Add($"{name}: kiss {kissN}, die {dieN} kontur");
            }
            S(11, "hanya kontur luar: lubang yang tertutup offset tidak dipotong", solid, string.Join("; ", holes));

            // S9: stiker besar 300 mm (gambar 3000 px), kiss + die.
            var bigImg = Draw(dc => dc.DrawEllipse(Brushes.Black, null, new Point(1500, 1500), 1400, 1400), 3000, 3000);
            var bsw = Stopwatch.StartNew();
            var big300 = Png(bigImg, 300, kiss, die);
            S(9, "stiker besar 300 mm dimuat < 5 detik", bsw.ElapsedMilliseconds < 5000, $"{bsw.ElapsedMilliseconds} ms, kiss {Nodes(big300.Kiss)} node, die {Nodes(big300.Die)} node");

            var sheets = Nest.Run(items, A4W, A4H, Margin, Gap, out int tooBig);
            Check(tooBig == 0, $"{tooBig} stiker tidak muat");
            Check(sheets.Count >= 2, "tidak meluap ke lembar kedua");
            Check(sheets.Sum(x => x.Count) == items.Sum(s => s.Copies), "jumlah tempatan salah");
            foreach (var sh in sheets) Check(Nest.Check(sh, A4W, A4H, Margin) == 0, "ada tumpang tindih / keluar margin");
            log.Add($"susun: {sheets.Sum(x => x.Count)} stiker -> {sheets.Count} lembar A4, pemakaian {Use(sheets):P1}");

            // Uji deteksi tumpang tindih: geser satu stiker ke atas tetangganya.
            var probe = sheets[0];
            var (ox, oy) = (probe[1].X, probe[1].Y);
            (probe[1].X, probe[1].Y) = (probe[0].X, probe[0].Y);
            Check(Nest.Check(probe, A4W, A4H, Margin) >= 2, "tumpang tindih tidak terdeteksi");
            (probe[1].X, probe[1].Y) = (ox, oy);
            Nest.Check(probe, A4W, A4H, Margin);

            var heights = sheets.Select(_ => A4H).ToList();
            var outPdf = Path.Combine(dir, "export.pdf");
            var warn = Export.Pdf(outPdf, sheets, A4W, heights);
            Check(warn.Count == 0, "peringatan export: " + string.Join("; ", warn));

            using (var r = new Pdfium(outPdf))
                Check(r.Pages == sheets.Count, "jumlah halaman != jumlah lembar");

            // Artwork PDF harus tepat di posisinya: titik biru (persegi) dan oranye (lingkaran) pada render export.
            int si = sheets.FindIndex(x => x.Any(p => p.S == pdfItem));
            var pp = sheets[si].First(p => p.S == pdfItem);
            var render = RenderPage(outPdf, si, 4);
            Save(render, Path.Combine(dir, $"export-render-{si + 1}.png"));
            var blue = At(render, pp, 70, 60, 4);
            var orange = At(render, pp, 20, 60, 4);
            Check(blue.B > 150 && blue.R < 100, $"posisi artwork PDF salah (biru = {blue})");
            Check(orange.R > 200 && orange.B < 100, $"posisi artwork PDF salah (oranye = {orange})");

            using (var doc = PdfReader.Open(outPdf, PdfDocumentOpenMode.Import))
            {
                var page = doc.Pages[si];
                var xo = page.Resources.Elements.GetDictionary("/XObject");
                Check(xo != null && xo.Elements.Values.Any(v => (v as PdfReference)?.Value is PdfDictionary d && d.Elements.GetName("/Subtype") == "/Form"),
                    "artwork PDF tidak tertempel sebagai vektor (Form XObject)");
                var content = Content(page);
                Check(content.Contains("0 1 0 0 K") && content.Contains("1 0 0 0 K"), "garis potong bukan CMYK magenta/cyan");
                int lines = Regex.Matches(content, @"(?<=\s)l(?=\s)").Count, curves = Regex.Matches(content, @"(?<=\s)c(?=\s)").Count;
                S(8, "PDF export: garis potong hanya kurva Bezier, warna CMYK", lines == 0 && curves > 0, $"halaman {si + 1}: {curves} kurva, {lines} garis lurus");
            }
            Check(fails.Count == 0, "skenario gagal: " + string.Join(", ", fails));

            Save(Export.Snapshot(Nest.Sheet(sheets[0], A4W, A4H, Margin, null, 0), A4W, A4H, 4), Path.Combine(dir, "layout-1.png"));

            // Preview JPG untuk customer: satu file per lembar, sisi panjang +-2000 px, bisa dibaca ulang.
            var jpgs = Export.Preview(Path.Combine(dir, "preview.jpg"), sheets, A4W, heights, Margin);
            Check(jpgs.Count == sheets.Count && jpgs.All(File.Exists), "jumlah preview JPG != jumlah lembar");
            var jf = BitmapDecoder.Create(new Uri(jpgs[0]), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Check(Math.Abs(Math.Max(jf.PixelWidth, jf.PixelHeight) - 2000) <= 2, $"ukuran preview salah: {jf.PixelWidth}x{jf.PixelHeight}");
            log.Add($"preview: {jpgs.Count} JPG, {jf.PixelWidth}x{jf.PixelHeight} px, {new FileInfo(jpgs[0]).Length / 1024} KB");
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

    // Node seperti di CorelDRAW: satu per segmen (C = kurva, L = garis lurus); sudut tajam Potrace = dua L.
    static int Nodes(string path) => path.Count(ch => ch is 'C' or 'L');
    static int Corners(string path) => path.Count(ch => ch == 'L') / 2;

    static void Check(bool ok, string msg)
    {
        if (!ok) throw new Exception(msg);
    }

    static double Use(List<List<Placed>> sheets) => sheets.Sum(x => x.Sum(p => p.S.AreaMm2)) / (A4W * A4H * sheets.Count);

    static string Content(PdfPage page)
    {
        var sb = new StringBuilder();
        foreach (var item in page.Contents.Elements)
            if ((item as PdfReference)?.Value is PdfDictionary d && d.Stream != null) sb.Append(Encoding.ASCII.GetString(d.Stream.UnfilteredValue));
        return sb.ToString();
    }

    static BitmapSource RenderPage(string pdf, int page, double pxMm)
    {
        using var r = new Pdfium(pdf);
        var src = r.Render(page, pxMm * 25.4 / 72);
        int w = src.PixelWidth, h = src.PixelHeight;
        var px = new byte[w * h * 4];
        src.CopyPixels(px, w * 4, 0);
        for (int i = 0; i < px.Length; i += 4) // latar transparan -> putih
        {
            double a = px[i + 3] / 255.0;
            for (int c = 0; c < 3; c++) px[i + c] = (byte)(px[i + c] * a + 255 * (1 - a));
            px[i + 3] = 255;
        }
        return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
    }

    // Warna render pada titik (pt dari pojok kiri-atas konten PDF) milik stiker yang ditempatkan.
    static Color At(BitmapSource bmp, Placed p, double xPt, double yPt, double pxMm)
    {
        var page = Nest.Xf(p).Transform(new Point(p.S.Off + xPt * 25.4 / 72, p.S.Off + yPt * 25.4 / 72));
        var c = new byte[4];
        bmp.CopyPixels(new Int32Rect((int)(page.X * pxMm), (int)(page.Y * pxMm), 1, 1), c, 4, 0);
        return Color.FromRgb(c[2], c[1], c[0]);
    }

    static void Save(BitmapSource b, string path, BitmapEncoder? enc = null)
    {
        enc ??= new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(b));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    // Luas (mm2) bagian dalam garis kiss a yang keluar dari garis kiss b. Bingkai disamakan lewat Off (posisi artwork).
    static double Outside(Sticker a, Sticker b)
    {
        static Geometry G(Sticker s) { var g = s.KissGeo!.Clone(); g.Transform = new TranslateTransform(-s.Off, -s.Off); return g; }
        return Geometry.Combine(G(a), G(b), GeometryCombineMode.Exclude, null).GetArea();
    }

    static BitmapSource Draw(Action<DrawingContext> paint, int w = 300, int h = 300)
    {
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen()) paint(dc);
        var rt = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rt.Render(v);
        return rt;
    }

    static void Samples(string dir)
    {
        // circle.png: transparan
        Save(Draw(dc =>
        {
            dc.DrawEllipse(Brushes.Orange, null, new Point(150, 150), 140, 140);
            dc.DrawEllipse(Brushes.SteelBlue, null, new Point(150, 150), 90, 90);
            dc.DrawEllipse(Brushes.White, null, new Point(150, 150), 40, 40);
        }), Path.Combine(dir, "circle.png"));

        // star.jpg: latar putih (tanpa alpha) + lubang putih di tengah
        Save(Draw(dc =>
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
        }), Path.Combine(dir, "star.jpg"), new JpegBitmapEncoder { QualityLevel = 90 });

        // rough.jpg: logo bergelombang bertepi kasar (seperti foto JPG dari WhatsApp), untuk uji reduksi node
        var rnd = new Random(7);
        var blob = Enumerable.Range(0, 180).Select(i =>
        {
            double a = i * Math.PI / 90, r = 110 + 18 * Math.Sin(5 * a) + rnd.Next(-3, 4);
            return new Point(150 + r * Math.Cos(a), 150 + r * Math.Sin(a));
        }).ToList();
        Save(Draw(dc =>
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 300, 300));
            var g = new StreamGeometry();
            using (var c = g.Open()) { c.BeginFigure(blob[0], true, true); c.PolyLineTo(blob.Skip(1).ToList(), true, false); }
            dc.DrawGeometry(Brushes.SeaGreen, null, g);
        }), Path.Combine(dir, "rough.jpg"), new JpegBitmapEncoder { QualityLevel = 70 });

        // logo-on-blue.jpg: lingkaran oranye + teks hijau di latar biru, untuk uji Rapikan gambar
        Save(Draw(dc =>
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 90, 168)), null, new Rect(0, 0, 300, 300));
            dc.DrawEllipse(Brushes.DarkOrange, null, new Point(150, 140), 110, 110);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(20, 120, 60)), null, new Rect(20, 280, 260, 12));
        }), Path.Combine(dir, "logo-on-blue.jpg"), new JpegBitmapEncoder { QualityLevel = 90 });

        File.WriteAllText(Path.Combine(dir, "badge.svg"),
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"120\" viewBox=\"0 0 100 60\">" +
            "<rect x=\"2\" y=\"2\" width=\"96\" height=\"56\" rx=\"12\" fill=\"#2a9d8f\"/>" +
            "<circle cx=\"30\" cy=\"30\" r=\"14\" fill=\"#e9c46a\"/><rect x=\"52\" y=\"20\" width=\"36\" height=\"6\" fill=\"#fff\"/><rect x=\"52\" y=\"34\" width=\"28\" height=\"6\" fill=\"#fff\"/></svg>");

        // logo.pdf: vektor CMYK, CropBox (40,60)-(260,250) lebih kecil dari MediaBox 300x300 pt.
        // Dibuat sekali saja: PDFsharp menulis tanggal/ID baru tiap kali, file sampel di git jadi berubah terus.
        if (!File.Exists(Path.Combine(dir, "logo.pdf")))
        using (var doc = new PdfDocument())
        {
            var page = doc.AddPage();
            page.Width = XUnit.FromPoint(300);
            page.Height = XUnit.FromPoint(300);
            using (var g = XGraphics.FromPdfPage(page))
            {
                g.DrawEllipse(new XSolidBrush(XColor.FromCmyk(0, 0.6, 1, 0)), 90, 90, 120, 120);
                g.DrawRectangle(new XSolidBrush(XColor.FromCmyk(1, 0.4, 0, 0)), 150, 135, 80, 30);
                g.DrawRectangle(new XSolidBrush(XColor.FromCmyk(0, 1, 1, 0)), 5, 5, 20, 20); // di luar CropBox
            }
            page.CropBox = new PdfRectangle(new XPoint(40, 60), new XPoint(260, 250));
            doc.Save(Path.Combine(dir, "logo.pdf"));
        }
        File.Copy(Path.Combine(dir, "logo.pdf"), Path.Combine(dir, "logo.ai"), true); // AI "PDF compatible"
    }
}
