using System.Windows;

namespace StickerLayout;

// Teks UI dua bahasa. XAML memakai {DynamicResource Kunci}; kode memakai Lang.T("Kunci").
public static class Lang
{
    public static string Code { get; private set; } = "id";

    static readonly Dictionary<string, (string Id, string En)> S = new()
    {
        ["Title"] = ($"{App.Brand} — Prototipe", $"{App.Brand} — Prototype"),
        ["Lang"] = ("Bahasa", "Language"),
        ["SecFiles"] = ("File desain", "Design files"),
        ["Add"] = ("Tambah file…", "Add files…"),
        ["DefWidth"] = ("Lebar awal PNG/JPG/SVG (mm)", "Initial width PNG/JPG/SVG (mm)"),
        ["ListHdr"] = ("Nama · Lebar (mm) · Jumlah", "Name · Width (mm) · Qty"),
        ["Remove"] = ("Hapus dari daftar", "Remove from list"),
        ["SecMachine"] = ("Mesin", "Machine"),
        ["MarginLbl"] = ("Margin reg mark (mm)", "Reg mark margin (mm)"),
        ["AddMachine"] = ("Tambah mesin", "Add machine"),
        ["MName"] = ("Nama", "Name"),
        ["MMaxW"] = ("Lebar potong maks (mm)", "Max cut width (mm)"),
        ["MMaxH"] = ("Panjang potong maks (mm)", "Max cut length (mm)"),
        ["Save"] = ("Simpan", "Save"),
        ["SecPaper"] = ("Kertas", "Paper"),
        ["PaperW"] = ("Lebar (mm)", "Width (mm)"),
        ["PaperH"] = ("Tinggi (mm)", "Height (mm)"),
        ["PaperRot"] = ("Putar", "Rotate"),
        ["SecCut"] = ("Garis potong", "Cut lines"),
        ["Kiss"] = ("Kiss-cut", "Kiss-cut"),
        ["Die"] = ("Die-cut", "Die-cut"),
        ["Offset"] = ("Offset", "Offset"),
        ["Smooth"] = ("Halus", "Smooth"),
        ["CutUnit"] = ("Offset & halus dalam mm. Halus 0 = mati.", "Offset & smooth in mm. Smooth 0 = off."),
        ["Gap"] = ("Jarak antar stiker (mm)", "Gap between stickers (mm)"),
        ["Nest"] = ("Susun otomatis", "Auto layout"),
        ["Compact"] = ("Compact lembar terakhir", "Compact last sheet"),
        ["RotSel"] = ("Putar terpilih 90° (R)", "Rotate selected 90° (R)"),
        ["DelSel"] = ("Hapus terpilih (Del)", "Delete selected (Del)"),
        ["Export"] = ("Export PDF…", "Export PDF…"),
        ["Fit"] = ("Pas layar", "Fit"),
        ["Hint"] = ("Seret stiker untuk menggeser. Panah = geser 1 mm, Shift+panah = 10 mm. Ctrl+roda = zoom. Magenta = kiss-cut, cyan = die-cut, merah = tumpang tindih/keluar margin.",
                    "Drag a sticker to move it. Arrows = move 1 mm, Shift+arrows = 10 mm. Ctrl+wheel = zoom. Magenta = kiss-cut, cyan = die-cut, red = overlap/outside margin."),
        ["LoadFail"] = ("Gagal memuat", "Failed to load"),
        ["NoCorel"] = ("CorelDRAW tidak terpasang. Export file CDR ke PDF dulu, lalu tambahkan PDF-nya.", "CorelDRAW is not installed. Export the CDR file to PDF first, then add the PDF."),
        ["Empty"] = ("Gambar kosong (tidak ada artwork).", "Image is empty (no artwork)."),
        ["Unsupported"] = ("Format tidak didukung.", "Unsupported format."),
        ["NeedCut"] = ("Aktifkan minimal satu garis potong.", "Enable at least one cut line."),
        ["DieInside"] = ("Offset die-cut harus lebih besar dari offset kiss-cut.", "Die-cut offset must be larger than kiss-cut offset."),
        ["NestFirst"] = ("Susun stiker dulu.", "Lay out stickers first."),
        ["Summary"] = ("{0} stiker di {1} lembar. Pemakaian bahan {2:P0}.", "{0} stickers on {1} sheet(s). Material use {2:P0}."),
        ["TooBig"] = ("{0} stiker lebih besar dari area kertas.", "{0} stickers are larger than the paper area."),
        ["Overlap"] = ("{0} stiker tumpang tindih/keluar margin (merah).", "{0} stickers overlap or exceed the margin (red)."),
        ["OverMachine"] = ("Kertas melebihi area potong {0} ({1} × {2} mm).", "Paper exceeds the cutting area of {0} ({1} × {2} mm)."),
        ["SheetHdr"] = ("Lembar {0} · {1:P0}", "Sheet {0} · {1:P0}"),
        ["Saved"] = ("Tersimpan: {0}", "Saved: {0}"),
        ["ExportBad"] = ("Ada stiker tumpang tindih/keluar margin. Tetap export?", "Some stickers overlap or exceed the margin. Export anyway?"),
        ["PdfRaster"] = ("{0}: PDF ditempel sebagai gambar, bukan vektor ({1}).", "{0}: PDF placed as an image, not vector ({1})."),
        ["MachineSaved"] = ("Mesin disimpan: {0}", "Machine saved: {0}"),
    };

    public static void Apply(string code)
    {
        Code = code;
        foreach (var (k, v) in S) Application.Current.Resources[k] = code == "en" ? v.En : v.Id;
    }

    public static string T(string key, params object[] args)
    {
        var f = Code == "en" ? S[key].En : S[key].Id;
        return args.Length == 0 ? f : string.Format(f, args);
    }
}
