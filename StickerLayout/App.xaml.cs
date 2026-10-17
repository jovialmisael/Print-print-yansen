using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StickerLayout;

public partial class App : Application
{
    public const string Brand = "Printech"; // nama sementara

    // File sementara milik proses ini (PDF hasil konversi CDR). Dihapus saat aplikasi ditutup.
    public static readonly string Temp = Path.Combine(Path.GetTempPath(), Brand, Environment.ProcessId.ToString());

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 1 && e.Args[0] == "--selfcheck") { Shutdown(SelfCheck.Run(e.Args[1])); return; }
        if (e.Args.Length > 2 && e.Args[0] == "--cdr2pdf") { Shutdown(Corel.Worker(e.Args[1], e.Args[2])); return; } // proses worker CorelDRAW
        // Error tak terduga: tampilkan pesan, aplikasi tetap jalan (layout operator tidak hilang).
        DispatcherUnhandledException += (_, ev) =>
        {
            MessageBox.Show(ev.Exception.Message, Brand, MessageBoxButton.OK, MessageBoxImage.Error);
            ev.Handled = true;
        };
        // Render CPU: RAM 122 MB vs 467 MB lewat driver GPU (diukur, Intel iGPU). Gambar 2D sederhana, CPU cukup.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        Lang.Apply("id");
        new MainWindow(e.Args).Show(); // file sebagai argumen: "Open with" / drag file ke ikon aplikasi
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // Judul jendela gelap agar menyatu dengan tema (Windows 10 20H1+; warna judul hanya Windows 11).
    public static void DarkTitle(Window w)
    {
        var h = new WindowInteropHelper(w).Handle;
        int dark = 1, caption = 0x2B2726; // #26272B sebagai COLORREF 0x00BBGGRR
        DwmSetWindowAttribute(h, 20, ref dark, 4);
        DwmSetWindowAttribute(h, 35, ref caption, 4);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { if (Directory.Exists(Temp)) Directory.Delete(Temp, true); }
        catch (IOException) { } // file masih dipakai: biarkan, Windows membersihkan folder Temp
        catch (UnauthorizedAccessException) { }
        base.OnExit(e);
    }
}
