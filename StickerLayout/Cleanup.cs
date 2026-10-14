using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StickerLayout;

// Merapikan gambar raster, pengganti langkah Photoshop: hapus latar, penghapus klik, kikis tepi.
// Semua operasi hanya mengubah alpha (transparansi); warna asli tidak disentuh.
public sealed class CleanImage
{
    // ponytail: sisi panjang dibatasi 4000 px (stiker ±33 cm pada 300 dpi); naikkan bila perlu stiker lebih besar.
    public const int MaxSide = 4000;

    public readonly int W, H;
    readonly byte[] px;                    // BGRA; kanal alpha ditulis ulang setiap Compose
    readonly byte[] a0;                    // alpha asli
    readonly byte[] a;                     // alpha kerja (dipakai ulang)
    readonly BitArray erased;              // gabungan semua langkah
    readonly List<BitArray> steps = [];    // piksel yang dihapus per langkah, untuk undo
    int erasedCount;

    public CleanImage(byte[] image)
    {
        BitmapSource src;
        using (var ms = new MemoryStream(image))
            src = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        double k = Math.Min(1, (double)MaxSide / Math.Max(src.PixelWidth, src.PixelHeight));
        if (k < 1) src = new TransformedBitmap(src, new ScaleTransform(k, k));
        var f = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        W = f.PixelWidth;
        H = f.PixelHeight;
        px = new byte[W * H * 4];
        f.CopyPixels(px, W * 4, 0);
        a0 = new byte[W * H];
        for (int i = 0; i < a0.Length; i++) a0[i] = px[i * 4 + 3];
        a = new byte[W * H];
        erased = new BitArray(W * H);
    }

    public bool Changed => steps.Count > 0;
    public double ErasedFraction => (double)erasedCount / (W * H);

    bool Clear(int i) => erased[i] || a0[i] <= 16;

    bool Near(int i, Color c, int tol) =>
        Math.Abs(px[i * 4] - c.B) <= tol && Math.Abs(px[i * 4 + 1] - c.G) <= tol && Math.Abs(px[i * 4 + 2] - c.R) <= tol;

    // Warna latar = warna yang paling sering muncul di tepi gambar (dikelompokkan per 16 tingkat).
    public Color? BorderColor()
    {
        var bins = new Dictionary<int, (int n, long b, long g, long r)>();
        void Add(int x, int y)
        {
            int i = y * W + x;
            if (Clear(i)) return;
            int key = (px[i * 4 + 2] >> 4) << 8 | (px[i * 4 + 1] >> 4) << 4 | px[i * 4] >> 4;
            var (n, b, g, r) = bins.GetValueOrDefault(key);
            bins[key] = (n + 1, b + px[i * 4], g + px[i * 4 + 1], r + px[i * 4 + 2]);
        }
        for (int x = 0; x < W; x++) { Add(x, 0); Add(x, H - 1); }
        for (int y = 1; y < H - 1; y++) { Add(0, y); Add(W - 1, y); }
        if (bins.Count == 0) return null;
        var t = bins.Values.MaxBy(v => v.n);
        return Color.FromRgb((byte)(t.r / t.n), (byte)(t.g / t.n), (byte)(t.b / t.n));
    }

    // Hapus latar: flood-fill dari semua tepi, melewati area yang sudah transparan.
    // Lubang di dalam logo aman karena tidak tersambung ke tepi.
    public int EraseFromBorder(int tol)
    {
        if (BorderColor() is not { } bg) return 0;
        var step = new BitArray(W * H);
        var seen = new BitArray(W * H);
        var st = new Stack<int>();
        int n = 0;
        void Push(int i)
        {
            if (seen[i]) return;
            seen[i] = true;
            if (Clear(i)) st.Push(i);
            else if (Near(i, bg, tol)) { step[i] = true; n++; st.Push(i); }
        }
        for (int x = 0; x < W; x++) { Push(x); Push((H - 1) * W + x); }
        for (int y = 1; y < H - 1; y++) { Push(y * W); Push(y * W + W - 1); }
        Flood(st, Push);
        return Commit(step, n);
    }

    // Penghapus klik: area bersebelahan yang warnanya mirip dengan titik yang diklik.
    public int EraseAt(int x, int y, int tol)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return 0;
        int i0 = y * W + x;
        if (Clear(i0)) return 0;
        var c = Color.FromRgb(px[i0 * 4 + 2], px[i0 * 4 + 1], px[i0 * 4]);
        var step = new BitArray(W * H);
        var st = new Stack<int>();
        int n = 0;
        void Push(int i)
        {
            if (step[i] || Clear(i) || !Near(i, c, tol)) return;
            step[i] = true;
            n++;
            st.Push(i);
        }
        Push(i0);
        Flood(st, Push);
        return Commit(step, n);
    }

    void Flood(Stack<int> st, Action<int> push)
    {
        while (st.Count > 0)
        {
            int i = st.Pop(), x = i % W;
            if (x > 0) push(i - 1);
            if (x < W - 1) push(i + 1);
            if (i >= W) push(i - W);
            if (i < W * (H - 1)) push(i + W);
        }
    }

    int Commit(BitArray step, int n)
    {
        if (n == 0) return 0;
        steps.Add(step);
        erased.Or(step);
        erasedCount += n;
        return n;
    }

    public bool Undo()
    {
        if (steps.Count == 0) return false;
        var last = steps[^1];
        steps.RemoveAt(steps.Count - 1);
        erased.Xor(last); // langkah tidak pernah tumpang tindih, jadi XOR = buang langkah terakhir
        erasedCount -= Count(last);
        return true;
    }

    public void Reset()
    {
        steps.Clear();
        erased.SetAll(false);
        erasedCount = 0;
    }

    static int Count(BitArray b)
    {
        int n = 0;
        foreach (bool v in b) if (v) n++;
        return n;
    }

    // Alpha akhir: asli, dikurangi yang dihapus, lalu tepi dikikis `contract` px (buang halo).
    // Di luar gambar dianggap opak supaya artwork yang menyentuh tepi tidak ikut terkikis.
    public byte[] Alpha(int contract)
    {
        for (int i = 0; i < a.Length; i++) a[i] = erased[i] ? (byte)0 : a0[i];
        var clear = new List<int>();
        for (int pass = 0; pass < contract; pass++)
        {
            clear.Clear();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (a[i] <= 16) continue;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int u = x + dx, v = y + dy;
                            if (u >= 0 && v >= 0 && u < W && v < H && a[v * W + u] <= 16) { clear.Add(i); goto next; }
                        }
                next:;
                }
            foreach (int i in clear) a[i] = 0;
        }
        return a;
    }

    public int OpaqueCount(int contract) => Alpha(contract).Count(v => v > 16);

    public byte[] Compose(int contract)
    {
        var al = Alpha(contract);
        for (int i = 0; i < al.Length; i++) px[i * 4 + 3] = al[i];
        return px;
    }

    // Versi kecil untuk layar (nearest neighbor) supaya gambar besar tetap ringan ditampilkan.
    public void ComposeView(int contract, byte[] dst, int dw, int dh)
    {
        var s32 = MemoryMarshal.Cast<byte, int>(Compose(contract));
        var d32 = MemoryMarshal.Cast<byte, int>(dst.AsSpan());
        for (int y = 0; y < dh; y++)
        {
            int row = (int)((long)y * H / dh) * W;
            for (int x = 0; x < dw; x++) d32[y * dw + x] = s32[row + (int)((long)x * W / dw)];
        }
    }

    public byte[] ToPng(int contract)
    {
        var bmp = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, Compose(contract), W * 4);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }
}
