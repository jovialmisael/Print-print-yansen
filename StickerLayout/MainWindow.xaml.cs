using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace StickerLayout;

public partial class MainWindow : Window
{
    enum Tool { Move, Hand, Zoom }

    static readonly string[] Exts = [".pdf", ".ai", ".cdr", ".png", ".jpg", ".jpeg", ".svg"];
    readonly ObservableCollection<Sticker> items = [];
    readonly ScaleTransform zoom = new();
    readonly string[] startFiles;
    List<List<Placed>> sheets = [];
    List<double> sheetH = [];
    List<Canvas> canvases = [];
    double sheetW, panH, panV;
    Placed? sel, drag;
    int selSheet, tooBig;
    Point last, panFrom;
    bool panning, space;
    Tool tool = Tool.Move;
    string status = "";

    public MainWindow(string[] files)
    {
        startFiles = files;
        InitializeComponent();
        List.ItemsSource = items;
        PaperBox.ItemsSource = Paper.Sizes.Keys.Append("Custom").ToList();
        PaperBox.SelectedItem = "A3+"; // kertas stiker Develop (AP 55)
    }

    static double D(TextBox t, double def, double min = 0.01) =>
        double.TryParse(t.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= min ? v : def;

    static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    double CW => D(CanvasW, 297, 10);
    double CH => D(CanvasH, 420, 10);
    double MarginMm => D(MarginBox, 10, 0);
    Cut Kiss => new(KissOn.IsChecked == true, D(KissOff, 2, 0), D(KissSm, 0, 0));
    Cut Die => new(DieOn.IsChecked == true, D(DieOff, 4, 0), D(DieSm, 0, 0));
    Brush Dim => (Brush)FindResource("Dim");

    // Judul jendela gelap agar menyatu dengan tema (Windows 10 20H1+; warna judul hanya Windows 11).
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    void Window_SourceInitialized(object s, EventArgs e)
    {
        var h = new WindowInteropHelper(this).Handle;
        int dark = 1, caption = 0x2B2726; // #26272B sebagai COLORREF 0x00BBGGRR
        DwmSetWindowAttribute(h, 20, ref dark, 4);
        DwmSetWindowAttribute(h, 35, ref caption, 4);
    }

    void Redraw(string? msg = null)
    {
        if (msg != null) status = msg;
        Status.Text = status;
        Status.ToolTip = status == "" ? null : status;
        canvases = [];
        var views = new List<UIElement>();
        double w = sheets.Count > 0 ? sheetW : CW;
        for (int i = 0; i < (items.Count == 0 ? 0 : Math.Max(1, sheets.Count)); i++)
        {
            double h = i < sheetH.Count ? sheetH[i] : CH;
            var ps = i < sheets.Count ? sheets[i] : [];
            var c = Nest.Sheet(ps, w, h, MarginMm, sel, i);
            c.LayoutTransform = zoom;
            canvases.Add(c);
            var head = new DockPanel { Margin = new Thickness(1, 0, 1, 7) };
            var pct = new TextBlock { Text = (ps.Sum(p => p.S.AreaMm2) / (w * h)).ToString("P0"), Foreground = Dim };
            DockPanel.SetDock(pct, Dock.Right);
            head.Children.Add(pct);
            head.Children.Add(new TextBlock { Text = Lang.T("SheetN", i + 1), Foreground = Dim });
            var panel = new StackPanel { Margin = new Thickness(18), HorizontalAlignment = HorizontalAlignment.Left };
            panel.Children.Add(head);
            panel.Children.Add(c);
            views.Add(panel);
        }
        Sheets.ItemsSource = views;
        Empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FileCount.Text = items.Count > 0 ? items.Count.ToString(CultureInfo.InvariantCulture) : "";
        var paper = PaperBox.SelectedItem as string ?? "Custom";
        TabText.Text = $"{paper}   {F(w)} × {F(CH)} mm";

        double area = sheetW * sheetH.Sum(), use = area > 0 ? sheets.Sum(x => x.Sum(p => p.S.AreaMm2)) / area : 0;
        UseBar.Width = 84 * Math.Clamp(use, 0, 1);
        UseText.Text = sheets.Count > 0 ? use.ToString("P0") : "–";
        var warn = new List<string>();
        int bad = sheets.Sum(x => x.Count(p => p.Bad));
        if (bad > 0) warn.Add(Lang.T("Overlap", bad));
        if (tooBig > 0) warn.Add(Lang.T("TooBig", tooBig));
        int low = items.Count(i => i.LowDpi);
        if (low > 0) warn.Add(Lang.T("LowDpi", low));
        WarnText.Text = string.Join("   ", warn);
        WarnPill.Visibility = warn.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        SelNone.Visibility = sel == null ? Visibility.Visible : Visibility.Collapsed;
        SelInfo.Visibility = sel == null ? Visibility.Collapsed : Visibility.Visible;
        if (sel != null)
        {
            var (aw, ah) = sel.Rot % 180 == 0 ? (sel.S.ArtW, sel.S.ArtH) : (sel.S.ArtH, sel.S.ArtW);
            SelThumb.Source = sel.S.Preview;
            SelName.Text = sel.S.Name;
            SelSize.Text = $"{F(aw)} × {F(ah)} mm";
            SelRot.Text = $"{sel.Rot}°";
            SelPos.Text = $"X {F(sel.X)}   Y {F(sel.Y)} mm";
        }
    }

    string Summary() => Lang.T("Summary", sheets.Sum(x => x.Count), sheets.Count);

    bool CutsValid()
    {
        if (!Kiss.On && !Die.On) { MessageBox.Show(Lang.T("NeedCut"), App.Brand); return false; }
        if (Kiss.On && Die.On && Die.Off <= Kiss.Off) { MessageBox.Show(Lang.T("DieInside"), App.Brand); return false; }
        return true;
    }

    void ClearLayout()
    {
        sheets = [];
        sheetH = [];
        sel = drag = null;
        tooBig = 0;
        Redraw("");
    }

    void DropEmpty()
    {
        for (int i = sheets.Count - 1; i >= 0; i--)
            if (sheets[i].Count == 0) { sheets.RemoveAt(i); sheetH.RemoveAt(i); }
    }

    void Add_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "PDF, AI, CDR, PNG, JPG, SVG|*.pdf;*.ai;*.cdr;*.png;*.jpg;*.jpeg;*.svg", Multiselect = true };
        if (dlg.ShowDialog() == true) AddFiles(dlg.FileNames);
    }

    // File baru langsung disusun: satu klik lebih sedikit untuk operator.
    void AddFiles(IEnumerable<string> files)
    {
        if (!CutsValid()) return;
        int before = items.Count;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            foreach (var f in files)
                try { foreach (var st in Loader.LoadAll(f, D(WidthBox, 50), Kiss, Die)) items.Add(st); }
                catch (Exception ex) { MessageBox.Show($"{Path.GetFileName(f)}: {ex.Message}", Lang.T("LoadFail")); }
        }
        finally { Mouse.OverrideCursor = null; }
        if (items.Count == before) return;
        bool first = sheets.Count == 0;
        Arrange();
        if (first) Fit();
    }

    void RemoveItem_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is not Sticker st) return;
        items.Remove(st);
        foreach (var sh in sheets) sh.RemoveAll(p => p.S == st);
        DropEmpty();
        sel = null;
        if (items.Count == 0) tooBig = 0;
        Redraw(sheets.Count > 0 ? Summary() : "");
    }

    void Nest_Click(object s, RoutedEventArgs e) => Arrange();

    void Arrange()
    {
        if (items.Count == 0) { Redraw(Lang.T("NestFirst")); return; }
        if (!CutsValid()) return;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            Cut kiss = Kiss, die = Die;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.WidthMm > 0 && (Math.Abs(it.LoadedW - it.WidthMm) > 1e-6 || it.LoadedKiss != kiss || it.LoadedDie != die))
                {
                    var n = Loader.Load(it.Source, it.Page, it.WidthMm, kiss, die);
                    n.Copies = it.Copies;
                    n.Name = it.Name;
                    items[i] = n;
                }
            }
            sheetW = CW;
            sheets = Nest.Run(items, sheetW, CH, MarginMm, D(GapBox, 2, 0), out tooBig);
            sheetH = sheets.Select(_ => CH).ToList();
            for (int i = 0; i < sheets.Count; i++) Nest.Check(sheets[i], sheetW, sheetH[i], MarginMm);
            sel = null;
            Redraw(Summary());
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, App.Brand); }
        finally { Mouse.OverrideCursor = null; }
    }

    void Compact_Click(object s, RoutedEventArgs e)
    {
        if (sheets.Count == 0) return;
        int i = sheets.Count - 1;
        sheetH[i] = Math.Ceiling(sheets[i].Max(p => p.Y + p.BH) + MarginMm);
        Nest.Check(sheets[i], sheetW, sheetH[i], MarginMm);
        Redraw(Summary());
    }

    void EditSel(Action<Placed> act)
    {
        if (sel == null) return;
        act(sel);
        Nest.Check(sheets[selSheet], sheetW, sheetH[selSheet], MarginMm);
        Redraw(Summary());
    }

    void DeleteSel()
    {
        if (sel == null) return;
        sheets[selSheet].Remove(sel);
        sel = null;
        DropEmpty();
        for (int i = 0; i < sheets.Count; i++) Nest.Check(sheets[i], sheetW, sheetH[i], MarginMm);
        Redraw(sheets.Count > 0 ? Summary() : "");
    }

    void Rotate_Click(object s, RoutedEventArgs e) => EditSel(p => p.Rot = (p.Rot + 90) % 360);
    void DeletePlaced_Click(object s, RoutedEventArgs e) => DeleteSel();
    void Exit_Click(object s, RoutedEventArgs e) => Close();

    void Export_Click(object s, RoutedEventArgs e)
    {
        if (sheets.Count == 0) { MessageBox.Show(Lang.T("NestFirst"), App.Brand); return; }
        if (sheets.Any(x => x.Any(p => p.Bad)) &&
            MessageBox.Show(Lang.T("ExportBad"), App.Brand, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var dlg = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = "layout.pdf" };
        if (dlg.ShowDialog() != true) return;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var warn = Export.Pdf(dlg.FileName, sheets, sheetW, sheetH);
            Redraw(string.Join("  ", warn.Prepend(Lang.T("Saved", dlg.FileName))));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, App.Brand); }
        finally { Mouse.OverrideCursor = null; }
    }

    // JPG ringan untuk konfirmasi ke customer (WhatsApp) sebelum cetak.
    void Preview_Click(object s, RoutedEventArgs e)
    {
        if (sheets.Count == 0) { MessageBox.Show(Lang.T("NestFirst"), App.Brand); return; }
        var dlg = new SaveFileDialog { Filter = "JPG|*.jpg", FileName = "preview.jpg" };
        if (dlg.ShowDialog() != true) return;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var files = Export.Preview(dlg.FileName, sheets, sheetW, sheetH, MarginMm);
            Redraw(Lang.T("PreviewSaved", files.Count, Path.GetDirectoryName(dlg.FileName)!));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, App.Brand); }
        finally { Mouse.OverrideCursor = null; }
    }

    void Paper_Changed(object s, SelectionChangedEventArgs e)
    {
        if (PaperBox.SelectedItem is string k && Paper.Sizes.TryGetValue(k, out var sz)) { CanvasW.Text = F(sz.W); CanvasH.Text = F(sz.H); }
        ClearLayout();
        if (IsLoaded && items.Count > 0) Arrange();
    }

    void PaperRot_Click(object s, RoutedEventArgs e)
    {
        (CanvasW.Text, CanvasH.Text) = (CanvasH.Text, CanvasW.Text);
        ClearLayout();
        if (items.Count > 0) Arrange();
    }

    void Lang_Click(object s, RoutedEventArgs e)
    {
        var code = (string)((MenuItem)s).Tag;
        LangId.IsChecked = code == "id";
        LangEn.IsChecked = code == "en";
        Lang.Apply(code);
        Redraw(sheets.Count > 0 ? Summary() : "");
    }

    void Window_Loaded(object s, RoutedEventArgs e)
    {
        Redraw();
        Fit();
        if (startFiles.Length > 0) AddFiles(startFiles);
    }

    // ---- File: drag & drop ----
    static string[] Dropped(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] f ? f.Where(x => Exts.Contains(Path.GetExtension(x).ToLowerInvariant())).ToArray() : [];

    void Window_DragOver(object s, DragEventArgs e)
    {
        bool ok = Dropped(e).Length > 0;
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        DropHint.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    void Window_DragLeave(object s, DragEventArgs e) => DropHint.Visibility = Visibility.Collapsed;

    void Window_Drop(object s, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
        var files = Dropped(e);
        if (files.Length > 0) AddFiles(files);
    }

    // ---- Tampilan: zoom & geser ----
    void Fit_Click(object s, RoutedEventArgs e) => Fit();
    void ZoomIn_Click(object s, RoutedEventArgs e) => ZoomAt(ZoomSlider.Value * 1.25);
    void ZoomOut_Click(object s, RoutedEventArgs e) => ZoomAt(ZoomSlider.Value / 1.25);

    void Fit()
    {
        double h = sheetH.Count > 0 ? sheetH.Max() : CH, w = sheets.Count > 0 ? sheetW : CW;
        double k = Math.Min((View.ViewportHeight - 110) / h, (View.ViewportWidth - 80) / w); // DIP per mm
        if (k > 0) ZoomSlider.Value = Math.Clamp(k / (96 / 25.4) * 100, ZoomSlider.Minimum, ZoomSlider.Maximum);
    }

    // Zoom dengan titik di bawah kursor tetap di tempat.
    void ZoomAt(double pct, Point? at = null)
    {
        pct = Math.Clamp(pct, ZoomSlider.Minimum, ZoomSlider.Maximum);
        var p = at ?? new Point(View.ViewportWidth / 2, View.ViewportHeight / 2);
        double k = pct / ZoomSlider.Value, h = View.HorizontalOffset, v = View.VerticalOffset;
        ZoomSlider.Value = pct;
        View.UpdateLayout();
        View.ScrollToHorizontalOffset((h + p.X) * k - p.X);
        View.ScrollToVerticalOffset((v + p.Y) * k - p.Y);
    }

    void Zoom_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        zoom.ScaleX = zoom.ScaleY = e.NewValue / 100 * 96 / 25.4; // 100% = ukuran asli
        if (ZoomText != null) ZoomText.Text = $"{e.NewValue:0}%";
    }

    void View_Wheel(object s, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        ZoomAt(ZoomSlider.Value * (e.Delta > 0 ? 1.1 : 1 / 1.1), e.GetPosition(View));
        e.Handled = true;
    }

    void Tool_Checked(object s, RoutedEventArgs e)
    {
        tool = s == ToolHand ? Tool.Hand : s == ToolZoom ? Tool.Zoom : Tool.Move;
        if (View != null) UpdateCursor();
    }

    void UpdateCursor() =>
        View.Cursor = panning || space || tool == Tool.Hand ? Cursors.SizeAll : tool == Tool.Zoom ? Cursors.Cross : null;

    static bool OnScrollBar(object src)
    {
        for (var d = src as DependencyObject; d is Visual; d = VisualTreeHelper.GetParent(d))
            if (d is ScrollBar) return true;
        return false;
    }

    // Geser: alat Hand, tahan Spasi, atau tombol tengah mouse. Zoom: alat Zoom (Alt = perkecil).
    void View_PreviewDown(object s, MouseButtonEventArgs e)
    {
        if (OnScrollBar(e.OriginalSource)) return;
        var pos = e.GetPosition(View);
        if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && (space || tool == Tool.Hand)))
        {
            panning = true;
            panFrom = pos;
            (panH, panV) = (View.HorizontalOffset, View.VerticalOffset);
            View.CaptureMouse();
            UpdateCursor();
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Left && tool == Tool.Zoom)
        {
            ZoomAt(ZoomSlider.Value * (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ? 1 / 1.25 : 1.25), pos);
            e.Handled = true;
        }
    }

    void View_PreviewMove(object s, MouseEventArgs e)
    {
        if (!panning) return;
        var d = e.GetPosition(View) - panFrom;
        View.ScrollToHorizontalOffset(panH - d.X);
        View.ScrollToVerticalOffset(panV - d.Y);
    }

    void View_PreviewUp(object s, MouseButtonEventArgs e)
    {
        if (!panning) return;
        panning = false;
        View.ReleaseMouseCapture();
        UpdateCursor();
        e.Handled = true;
    }

    // ---- Keyboard, gaya Photoshop ----
    void Window_KeyDown(object s, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.E)
        {
            Preview_Click(s, e);
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.O: case Key.I: Add_Click(s, e); break; // Ctrl+I = Import di CorelDRAW
                case Key.E: Export_Click(s, e); break;
                case Key.L: Arrange(); break;
                case Key.D0: case Key.NumPad0: Fit(); break;
                case Key.OemPlus: case Key.Add: ZoomAt(ZoomSlider.Value * 1.25); break;
                case Key.OemMinus: case Key.Subtract: ZoomAt(ZoomSlider.Value / 1.25); break;
                default: return;
            }
            e.Handled = true;
            return;
        }
        if (Keyboard.FocusedElement is TextBox) return;
        double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Space: if (!space) { space = true; UpdateCursor(); } break;
            case Key.V: ToolMove.IsChecked = true; break;
            case Key.H: ToolHand.IsChecked = true; break;
            case Key.Z: ToolZoom.IsChecked = true; break;
            case Key.F4 when Keyboard.FocusedElement is not ComboBox: Fit(); break; // F4 / Shift+F4 seperti CorelDRAW
            case Key.Delete when sel != null: DeleteSel(); break;
            case Key.R when sel != null: EditSel(p => p.Rot = (p.Rot + 90) % 360); break;
            case Key.Left when sel != null: EditSel(p => p.X -= step); break;
            case Key.Right when sel != null: EditSel(p => p.X += step); break;
            case Key.Up when sel != null: EditSel(p => p.Y -= step); break;
            case Key.Down when sel != null: EditSel(p => p.Y += step); break;
            default: return;
        }
        e.Handled = true;
    }

    void Window_KeyUp(object s, KeyEventArgs e)
    {
        if (e.Key != Key.Space || !space) return;
        space = false;
        UpdateCursor();
        e.Handled = true;
    }

    // ---- Pilih & seret stiker (alat Move) ----
    void Sheets_Down(object s, MouseButtonEventArgs e)
    {
        Placed? hit = null;
        Canvas? sheet = null;
        for (var d = e.OriginalSource as DependencyObject; d is Visual; d = VisualTreeHelper.GetParent(d))
        {
            if (hit == null && d is FrameworkElement { Tag: Placed p }) hit = p;
            if (d is Canvas { Tag: int }) { sheet = (Canvas)d; break; }
        }
        View.Focus();
        sel = sheet == null ? null : hit;
        if (sel != null)
        {
            selSheet = (int)sheet!.Tag;
            last = e.GetPosition(sheet);
            drag = sel;
            Sheets.CaptureMouse();
        }
        Redraw();
    }

    void Sheets_Move(object s, MouseEventArgs e)
    {
        if (drag == null) return;
        var c = canvases[selSheet];
        var pt = e.GetPosition(c);
        drag.X += pt.X - last.X;
        drag.Y += pt.Y - last.Y;
        last = pt;
        foreach (var el in c.Children.OfType<FrameworkElement>()) if (el.Tag == drag) el.RenderTransform = Nest.Xf(drag);
    }

    void Sheets_Up(object s, MouseButtonEventArgs e)
    {
        if (drag == null) return;
        drag = null;
        Sheets.ReleaseMouseCapture();
        Nest.Check(sheets[selSheet], sheetW, sheetH[selSheet], MarginMm);
        Redraw(Summary());
    }
}
