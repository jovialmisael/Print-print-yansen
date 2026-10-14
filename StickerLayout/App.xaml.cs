using System.IO;
using System.Windows;

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

    protected override void OnExit(ExitEventArgs e)
    {
        try { if (Directory.Exists(Temp)) Directory.Delete(Temp, true); }
        catch (IOException) { } // file masih dipakai: biarkan, Windows membersihkan folder Temp
        catch (UnauthorizedAccessException) { }
        base.OnExit(e);
    }
}
