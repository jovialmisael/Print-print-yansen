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
        ["MFile"] = ("File", "File"),
        ["MEdit"] = ("Edit", "Edit"),
        ["MView"] = ("Tampilan", "View"),
        ["Exit"] = ("Keluar", "Exit"),
        ["ZoomIn"] = ("Perbesar", "Zoom in"),
        ["ZoomOut"] = ("Perkecil", "Zoom out"),
        ["Fit"] = ("Pas layar", "Fit"),
        ["TipMove"] = ("Pilih dan geser stiker (V)", "Select and move stickers (V)"),
        ["TipHand"] = ("Geser tampilan (H, atau tahan Spasi)", "Pan the view (H, or hold Space)"),
        ["TipZoom"] = ("Zoom (Z). Alt+klik untuk memperkecil", "Zoom (Z). Alt+click to zoom out"),
        ["SecFiles"] = ("Desain", "Designs"),
        ["Add"] = ("Tambah file…", "Add files…"),
        ["DefWidth"] = ("Lebar awal PNG/JPG/SVG (mm)", "Initial width PNG/JPG/SVG (mm)"),
        ["WidthTip"] = ("Lebar artwork (mm)", "Artwork width (mm)"),
        ["QtyTip"] = ("Jumlah stiker", "Number of stickers"),
        ["Remove"] = ("Hapus dari daftar", "Remove from list"),
        ["SecPaper"] = ("Kertas", "Paper"),
        ["PaperW"] = ("Lebar (mm)", "Width (mm)"),
        ["PaperH"] = ("Tinggi (mm)", "Height (mm)"),
        ["PaperRot"] = ("Putar kertas", "Rotate paper"),
        ["MarginLbl"] = ("Margin tepi kertas (mm)", "Paper edge margin (mm)"),
        ["SecCut"] = ("Garis potong", "Cut lines"),
        ["Kiss"] = ("Kiss-cut", "Kiss-cut"),
        ["Die"] = ("Die-cut", "Die-cut"),
        ["Offset"] = ("Offset", "Offset"),
        ["Smooth"] = ("Halus", "Smooth"),
        ["CutUnit"] = ("Offset dan halus dalam mm. Halus 0 = mati.", "Offset and smooth in mm. Smooth 0 = off."),
        ["Gap"] = ("Jarak antar stiker (mm)", "Gap between stickers (mm)"),
        ["SecSel"] = ("Pilihan", "Selection"),
        ["SelNone"] = ("Klik stiker di lembar untuk memilihnya.", "Click a sticker on a sheet to select it."),
        ["SelSize"] = ("Artwork", "Artwork"),
        ["SelRot"] = ("Rotasi", "Rotation"),
        ["SelPos"] = ("Posisi", "Position"),
        ["Nest"] = ("Susun otomatis", "Auto layout"),
        ["Compact"] = ("Compact lembar terakhir", "Compact last sheet"),
        ["RotSel"] = ("Putar terpilih 90°", "Rotate selected 90°"),
        ["DelSel"] = ("Hapus terpilih", "Delete selected"),
        ["Export"] = ("Export PDF…", "Export PDF…"),
        ["Material"] = ("Bahan terpakai", "Material used"),
        ["EmptyTitle"] = ("Mulai dengan file desain", "Start with design files"),
        ["EmptyBody"] = ("Tarik file ke sini, atau klik Tambah file (Ctrl+O).", "Drag files here, or click Add files (Ctrl+O)."),
        ["DropHint"] = ("Lepaskan untuk menambahkan file", "Drop to add files"),
        ["SheetN"] = ("Lembar {0}", "Sheet {0}"),
        ["LoadFail"] = ("Gagal memuat", "Failed to load"),
        ["NoCorel"] = ("CorelDRAW tidak terpasang. Export file CDR ke PDF dulu, lalu tambahkan PDF-nya.", "CorelDRAW is not installed. Export the CDR file to PDF first, then add the PDF."),
        ["Empty"] = ("Gambar kosong (tidak ada artwork).", "Image is empty (no artwork)."),
        ["Unsupported"] = ("Format tidak didukung.", "Unsupported format."),
        ["NeedCut"] = ("Aktifkan minimal satu garis potong.", "Enable at least one cut line."),
        ["DieInside"] = ("Offset die-cut harus lebih besar dari offset kiss-cut.", "Die-cut offset must be larger than kiss-cut offset."),
        ["NestFirst"] = ("Tambahkan file desain dulu.", "Add design files first."),
        ["Summary"] = ("{0} stiker di {1} lembar.", "{0} stickers on {1} sheet(s)."),
        ["TooBig"] = ("{0} lebih besar dari kertas", "{0} larger than the paper"),
        ["Overlap"] = ("{0} tumpang tindih/keluar margin", "{0} overlapping/outside margin"),
        ["Saved"] = ("Tersimpan: {0}", "Saved: {0}"),
        ["ExportBad"] = ("Ada stiker tumpang tindih/keluar margin. Tetap export?", "Some stickers overlap or exceed the margin. Export anyway?"),
        ["PdfRaster"] = ("{0}: PDF ditempel sebagai gambar, bukan vektor ({1}).", "{0}: PDF placed as an image, not vector ({1})."),
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
