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
    List<Placed> layout = [];
    Placed? sel, drag;
    Point last;
    Canvas sheet = null!;

    public MainWindow()
    {
        InitializeComponent();
        List.ItemsSource = items;
        Redraw();
    }

    static double D(TextBox t, double def) =>
        double.TryParse(t.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : def;

    void Redraw(string? status = null)
    {
        sheet = Nest.Sheet(layout, D(CanvasW, 300), D(CanvasH, 400), RegMarks.IsChecked == true, sel);
        Host.Child = sheet;
        if (status != null) Status.Text = status;
    }

    void Add_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Gambar (PNG, SVG)|*.png;*.svg", Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames)
            try { items.Add(Loader.Load(f, D(WidthBox, 50), D(OffsetBox, 2))); }
            catch (Exception ex) { MessageBox.Show($"{Path.GetFileName(f)}: {ex.Message}", "Gagal memuat"); }
    }

    void Remove_Click(object s, RoutedEventArgs e)
    {
        if (List.SelectedItem is not Sticker st) return;
        items.Remove(st);
        layout.RemoveAll(p => p.S == st);
        Redraw();
    }

    void Nest_Click(object s, RoutedEventArgs e)
    {
        try
        {
            double off = D(OffsetBox, 2);
            for (int i = 0; i < items.Count; i++)
                if (Math.Abs(items[i].ReqOff - off) > 1e-6)
                {
                    var n = Loader.Load(items[i].Source, items[i].ArtW, off);
                    n.Copies = items[i].Copies;
                    items[i] = n;
                }
            layout = Nest.Run(items, D(CanvasW, 300), D(CanvasH, 400), D(GapBox, 2), out int failed);
            sel = null;
            Redraw(Summary(failed));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Gagal menyusun"); }
    }

    string Summary(int failed = 0)
    {
        double used = layout.Sum(p => p.S.AreaMm2), sheetArea = D(CanvasW, 300) * D(CanvasH, 400);
        return $"{layout.Count} stiker di kanvas. Pemakaian bahan {used / sheetArea:P0}."
            + (failed > 0 ? $" {failed} stiker tidak muat — perbesar kanvas." : "");
    }

    void Compact_Click(object s, RoutedEventArgs e)
    {
        if (layout.Count == 0) return;
        CanvasH.Text = Math.Ceiling(layout.Max(p => p.Y + p.BH) + Nest.Margin).ToString(CultureInfo.InvariantCulture);
        Redraw(Summary());
    }

    void Rotate_Click(object s, RoutedEventArgs e)
    {
        if (sel == null) return;
        sel.Rot = (sel.Rot + 90) % 360;
        Redraw();
    }

    void DeletePlaced_Click(object s, RoutedEventArgs e)
    {
        if (sel == null) return;
        layout.Remove(sel);
        sel = null;
        Redraw(Summary());
    }

    void Export_Click(object s, RoutedEventArgs e)
    {
        if (layout.Count == 0) { MessageBox.Show("Susun stiker dulu."); return; }
        var dlg = new SaveFileDialog { Filter = "SVG|*.svg", FileName = "layout.svg" };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, Nest.Export(layout, D(CanvasW, 300), D(CanvasH, 400), RegMarks.IsChecked == true));
        Status.Text = $"Tersimpan: {dlg.FileName}";
    }

    void Refresh_Click(object s, RoutedEventArgs e) => Redraw();

    void Zoom_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Zoom != null) Zoom.ScaleX = Zoom.ScaleY = e.NewValue;
    }

    void Host_Down(object s, MouseButtonEventArgs e)
    {
        DependencyObject? d = e.OriginalSource as DependencyObject;
        while (d != null && !(d is FrameworkElement { Tag: Placed })) d = VisualTreeHelper.GetParent(d);
        sel = (d as FrameworkElement)?.Tag as Placed;
        drag = sel;
        last = e.GetPosition(sheet);
        if (drag != null) Host.CaptureMouse();
        Redraw();
    }

    void Host_Move(object s, MouseEventArgs e)
    {
        if (drag == null) return;
        var pt = e.GetPosition(sheet);
        drag.X += pt.X - last.X;
        drag.Y += pt.Y - last.Y;
        last = pt;
        foreach (var c in sheet.Children.OfType<FrameworkElement>())
            if (c.Tag == drag) c.RenderTransform = Nest.Xf(drag);
    }

    void Host_Up(object s, MouseButtonEventArgs e)
    {
        drag = null;
        Host.ReleaseMouseCapture();
    }
}
