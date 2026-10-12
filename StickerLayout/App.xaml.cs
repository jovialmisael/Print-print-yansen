using System.Windows;

namespace StickerLayout;

public partial class App : Application
{
    public const string Brand = "Printech"; // nama sementara

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 1 && e.Args[0] == "--selfcheck") { Shutdown(SelfCheck.Run(e.Args[1])); return; }
        Lang.Apply("id");
        new MainWindow(e.Args).Show(); // file sebagai argumen: "Open with" / drag file ke ikon aplikasi
    }
}
