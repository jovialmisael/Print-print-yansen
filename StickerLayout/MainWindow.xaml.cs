using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace StickerLayout;

public partial class MainWindow : Window
{
    readonly ObservableCollection<Sticker> items = [];
    readonly List<Machine> machines = Machines.Load();
    readonly ScaleTransform zoom = new();
    List<List<Placed>> sheets = [];
    List<double> sheetH = [];
    List<Canvas> canvases = [];
    double sheetW;
    Placed? sel, drag;
    int selSheet;
    Point last;
    string status = "";

    readonly string[] startFiles;

    public MainWindow(string[] files)
    {
        startFiles = files;
        InitializeComponent();
        List.ItemsSource = items;
        MachineBox.ItemsSource = machines;
        MachineBox.SelectedIndex = 0;
        PaperBox.ItemsSource = Paper.Sizes.Keys.Append("Custom").ToList();
        PaperBox.SelectedItem = "A3";
    }

    static double D(TextBox t, double def, double min = 0.01) =>
        double.TryParse(t.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= min ? v : def;

    static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    double CW => D(CanvasW, 297, 10);
    double CH => D(CanvasH, 420, 10);
    double MarginMm => D(MarginBox, 10, 0);
    Cut Kiss => new(KissOn.IsChecked == true, D(KissOff, 2, 0), D(KissSm, 0, 0));
    Cut Die => new(DieOn.IsChecked == true, D(DieOff, 4, 0), D(DieSm, 0, 0));

    void Redraw(string? msg = null)
    {
        if (msg != null) status = msg;
        Status.Text = status;
        canvases = [];
        var views = new List<UIElement>();
        double w = sheets.Count > 0 ? sheetW : CW;
        for (int i = 0; i < Math.Max(1, sheets.Count); i++)
        {
            double h = i < sheetH.Count ? sheetH[i] : CH;
            var ps = i < sheets.Count ? sheets[i] : [];
            var c = Nest.Sheet(ps, w, h, MarginMm, sel, i);
            c.LayoutTransform = zoom;
            canvases.Add(c);
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = Lang.T("SheetHdr", i + 1, ps.Sum(p => p.S.AreaMm2) / (w * h)), Margin = new Thickness(0, 0, 0, 4) });
            panel.Children.Add(new Border { Child = c, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left });
            views.Add(panel);
        }
        Sheets.ItemsSource = views;
    }

    string Summary(int tooBig = 0)
    {
        double area = sheetW * sheetH.Sum();
        var msg = Lang.T("Summary", sheets.Sum(s => s.Count), sheets.Count, area > 0 ? sheets.Sum(s => s.Sum(p => p.S.AreaMm2)) / area : 0);
        if (tooBig > 0) msg += " " + Lang.T("TooBig", tooBig);
        int bad = sheets.Sum(s => s.Count(p => p.Bad));
        if (bad > 0) msg += " " + Lang.T("Overlap", bad);
        double h = sheetH.Count > 0 ? sheetH.Max() : CH, w = sheets.Count > 0 ? sheetW : CW;
        if (MachineBox.SelectedItem is Machine m && !((w <= m.MaxW && h <= m.MaxH) || (h <= m.MaxW && w <= m.MaxH)))
            msg += " " + Lang.T("OverMachine", m.Name, F(m.MaxW), F(m.MaxH));
        return msg;
    }

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

    void AddFiles(IEnumerable<string> files)
    {
        if (!CutsValid()) return;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            foreach (var f in files)
                try { foreach (var st in Loader.LoadAll(f, D(WidthBox, 50), Kiss, Die)) items.Add(st); }
                catch (Exception ex) { MessageBox.Show($"{Path.GetFileName(f)}: {ex.Message}", Lang.T("LoadFail")); }
        }
        finally { Mouse.OverrideCursor = null; }
    }

    void Remove_Click(object s, RoutedEventArgs e)
    {
        if (List.SelectedItem is not Sticker st) return;
        items.Remove(st);
        foreach (var sh in sheets) sh.RemoveAll(p => p.S == st);
        DropEmpty();
        sel = null;
        Redraw(sheets.Count > 0 ? Summary() : "");
    }

    void Nest_Click(object s, RoutedEventArgs e)
    {
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
            sheets = Nest.Run(items, sheetW, CH, MarginMm, D(GapBox, 2, 0), out int big);
            sheetH = sheets.Select(_ => CH).ToList();
            for (int i = 0; i < sheets.Count; i++) Nest.Check(sheets[i], sheetW, sheetH[i], MarginMm);
            sel = null;
            Redraw(Summary(big));
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
            Redraw(string.Join("\n", warn.Prepend(Lang.T("Saved", dlg.FileName))));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, App.Brand); }
        finally { Mouse.OverrideCursor = null; }
    }

    void Machine_Changed(object s, SelectionChangedEventArgs e)
    {
        if (MachineBox.SelectedItem is Machine m) MarginBox.Text = F(m.Margin);
        if (IsLoaded) Redraw(sheets.Count > 0 ? Summary() : null);
    }

    void SaveMachine_Click(object s, RoutedEventArgs e)
    {
        var name = NewName.Text.Trim();
        double w = D(NewW, 0), h = D(NewH, 0);
        if (name == "" || w <= 0 || h <= 0) return;
        var m = new Machine(name, w, h, D(NewMargin, 10, 0));
        machines.RemoveAll(x => x.Name == name);
        machines.Add(m);
        try { Machines.Save(machines); }
        catch (Exception ex) { MessageBox.Show(ex.Message, App.Brand); return; }
        MachineBox.ItemsSource = null;
        MachineBox.ItemsSource = machines;
        MachineBox.SelectedItem = m;
        Redraw(Lang.T("MachineSaved", name));
    }

    void Paper_Changed(object s, SelectionChangedEventArgs e)
    {
        if (PaperBox.SelectedItem is string k && Paper.Sizes.TryGetValue(k, out var sz)) { CanvasW.Text = F(sz.W); CanvasH.Text = F(sz.H); }
        ClearLayout();
    }

    void PaperRot_Click(object s, RoutedEventArgs e)
    {
        (CanvasW.Text, CanvasH.Text) = (CanvasH.Text, CanvasW.Text);
        ClearLayout();
    }

    void Lang_Changed(object s, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || LangBox.SelectedItem is not ComboBoxItem { Tag: string code }) return;
        Lang.Apply(code);
        Redraw(sheets.Count > 0 ? Summary() : "");
    }

    void Window_Loaded(object s, RoutedEventArgs e)
    {
        Fit();
        if (startFiles.Length > 0) AddFiles(startFiles);
    }
    void Fit_Click(object s, RoutedEventArgs e) => Fit();

    void Fit()
    {
        double h = sheetH.Count > 0 ? sheetH.Max() : CH, w = sheets.Count > 0 ? sheetW : CW;
        double k = Math.Min((View.ViewportHeight - 70) / h, (View.ViewportWidth - 40) / w); // DIP per mm
        if (k > 0) ZoomSlider.Value = Math.Clamp(k / (96 / 25.4) * 100, ZoomSlider.Minimum, ZoomSlider.Maximum);
    }

    void Zoom_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        zoom.ScaleX = zoom.ScaleY = e.NewValue / 100 * 96 / 25.4; // 100% = ukuran asli
        if (ZoomText != null) ZoomText.Text = $"{e.NewValue:0}%";
    }

    void View_Wheel(object s, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        ZoomSlider.Value = Math.Clamp(ZoomSlider.Value * (e.Delta > 0 ? 1.1 : 1 / 1.1), ZoomSlider.Minimum, ZoomSlider.Maximum);
        e.Handled = true;
    }

    void Window_KeyDown(object s, KeyEventArgs e)
    {
        if (sel == null || Keyboard.FocusedElement is TextBox) return;
        double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Delete: DeleteSel(); break;
            case Key.R: EditSel(p => p.Rot = (p.Rot + 90) % 360); break;
            case Key.Left: EditSel(p => p.X -= step); break;
            case Key.Right: EditSel(p => p.X += step); break;
            case Key.Up: EditSel(p => p.Y -= step); break;
            case Key.Down: EditSel(p => p.Y += step); break;
            default: return;
        }
        e.Handled = true;
    }

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
