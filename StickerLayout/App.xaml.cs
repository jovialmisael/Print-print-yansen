using System.Windows;

namespace StickerLayout;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 1 && e.Args[0] == "--selfcheck") { Shutdown(SelfCheck.Run(e.Args[1])); return; }
        new MainWindow().Show();
    }
}
