using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StickerLayout;

// Render PDF/AI lewat PDFium (paket bblanchon.PDFium.Win32, versi PDFium terbaru, hanya binary Windows).
// PDFium tidak thread-safe: semua pemanggilan dari satu thread (thread UI).
public sealed class Pdfium : IDisposable
{
    const string Dll = "pdfium";
    [DllImport(Dll)] static extern void FPDF_InitLibrary();
    [DllImport(Dll)] static extern IntPtr FPDF_LoadMemDocument64(IntPtr data, nuint size, [MarshalAs(UnmanagedType.LPStr)] string? password);
    [DllImport(Dll)] static extern void FPDF_CloseDocument(IntPtr doc);
    [DllImport(Dll)] static extern uint FPDF_GetLastError();
    [DllImport(Dll)] static extern int FPDF_GetPageCount(IntPtr doc);
    [DllImport(Dll)] static extern IntPtr FPDF_LoadPage(IntPtr doc, int index);
    [DllImport(Dll)] static extern void FPDF_ClosePage(IntPtr page);
    [DllImport(Dll)] static extern float FPDF_GetPageWidthF(IntPtr page);
    [DllImport(Dll)] static extern float FPDF_GetPageHeightF(IntPtr page);
    [DllImport(Dll)] static extern IntPtr FPDFBitmap_Create(int width, int height, int alpha);
    [DllImport(Dll)] static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);
    [DllImport(Dll)] static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int x, int y, int w, int h, int rotate, int flags);
    [DllImport(Dll)] static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);
    [DllImport(Dll)] static extern int FPDFBitmap_GetStride(IntPtr bitmap);
    [DllImport(Dll)] static extern void FPDFBitmap_Destroy(IntPtr bitmap);

    static Pdfium() => FPDF_InitLibrary();

    IntPtr mem, doc; // PDFium membaca langsung dari memori ini selama dokumen terbuka

    // Dibaca dari memori: path dengan karakter non-ASCII aman, file asli tidak dikunci.
    public Pdfium(string path)
    {
        var bytes = File.ReadAllBytes(path);
        mem = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, mem, bytes.Length);
        doc = FPDF_LoadMemDocument64(mem, (nuint)bytes.Length, null);
        if (doc != IntPtr.Zero) return;
        uint err = FPDF_GetLastError();
        Marshal.FreeHGlobal(mem);
        throw new InvalidDataException($"PDF tidak bisa dibuka (PDFium error {err}).");
    }

    public int Pages => FPDF_GetPageCount(doc);

    public (double W, double H) SizePt(int page)
    {
        var p = Page(page);
        try { return (FPDF_GetPageWidthF(p), FPDF_GetPageHeightF(p)); }
        finally { FPDF_ClosePage(p); }
    }

    // scale = pixel per point. Latar transparan: area tanpa isi punya alpha 0.
    public BitmapSource Render(int page, double scale)
    {
        var p = Page(page);
        try
        {
            int w = Math.Max(1, (int)Math.Round(FPDF_GetPageWidthF(p) * scale)), h = Math.Max(1, (int)Math.Round(FPDF_GetPageHeightF(p) * scale));
            var bmp = FPDFBitmap_Create(w, h, 1);
            if (bmp == IntPtr.Zero) throw new OutOfMemoryException($"Bitmap {w}x{h} terlalu besar.");
            try
            {
                FPDFBitmap_FillRect(bmp, 0, 0, w, h, 0);
                FPDF_RenderPageBitmap(bmp, p, 0, 0, w, h, 0, 0);
                int stride = FPDFBitmap_GetStride(bmp);
                var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, FPDFBitmap_GetBuffer(bmp), stride * h, stride);
                src.Freeze();
                return src;
            }
            finally { FPDFBitmap_Destroy(bmp); }
        }
        finally { FPDF_ClosePage(p); }
    }

    IntPtr Page(int i)
    {
        var p = FPDF_LoadPage(doc, i);
        return p != IntPtr.Zero ? p : throw new InvalidDataException($"Halaman {i + 1} tidak bisa dibaca (PDFium error {FPDF_GetLastError()}).");
    }

    public void Dispose()
    {
        if (doc == IntPtr.Zero) return;
        FPDF_CloseDocument(doc);
        Marshal.FreeHGlobal(mem);
        doc = mem = IntPtr.Zero;
    }
}
