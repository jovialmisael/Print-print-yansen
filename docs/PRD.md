# PRD — Software Layout Stiker (Print & Cut)

Status: Draf 1 · Tanggal: 2026-09-18

## 1. Tujuan
Operator toko print & cut stiker mengubah order customer menjadi satu kanvas layout vektor yang siap dicetak dan dipotong, dengan pemakaian bahan sehemat mungkin.

## 2. Pengguna
Operator/staf internal. Customer tidak memakai software ini (tanpa akun, unggah mandiri, atau pembayaran).

## 3. Alur
1. Customer datang dengan salah satu dari tiga kasus: (a) file PNG, (b) file vektor, (c) barang fisik.
2. Operator memasukkan ke software. Semua kasus dikonversi ke vektor (alasan: hasil output lebih akurat).
3. Software menyusun stiker di satu kanvas. Ukuran kanvas bisa diatur.
4. Operator mengedit layout bila perlu, lalu memadatkan (compact) agar bahan tidak terbuang.
5. Export satu file vektor. File itu dipakai printer untuk mencetak dan cutter untuk memotong mengikuti garis vektor.

## 4. Kebutuhan fungsional
| ID | Kebutuhan |
|----|-----------|
| F1 | Impor PNG → siluet di-trace jadi garis potong vektor (Potrace). Warna artwork tetap raster PNG tertanam di v1; trace berwarna belum ada. |
| F2 | Impor file vektor langsung (minimal SVG; AI/CDR lihat Pertanyaan Terbuka). |
| F3 | Kasus fisik: scan/foto → diperlakukan seperti PNG (F1). Tidak ada pipeline terpisah. |
| F4 | Ukuran kanvas bebas diatur operator (versi 1). Mengikuti lebar roll/lembar tetap ditunda. |
| F5 | Layout otomatis (nesting): susun stiker di kanvas dengan jarak antar stiker yang bisa diatur. |
| F6 | Edit manual: geser, putar, hapus, atur jumlah salinan tiap stiker. |
| F7 | Compact: padatkan layout agar area kanvas/bahan terpakai sekecil mungkin. |
| F8 | Export satu file vektor untuk printer dan cutter: SVG cukup untuk saat ini. AI dan CDR ditunda. |

## 5. Metrik keberhasilan (usulan, angka belum ditentukan)
- Persentase bahan terpakai (utilisasi kanvas) setelah compact. Target: Unknown.
- Garis potong presisi mengikuti hasil cetak. Toleransi: Unknown.

## 6. Di luar cakupan
Akun dan pembayaran customer, pelacakan order, kontrol langsung ke mesin (driver/RIP), pengganti Illustrator/CorelDRAW untuk desain kompleks.

## 7. Rekomendasi cakupan versi 1
- Kasus fisik = scan/foto jadi PNG, lalu jalur yang sama dengan F1. Integrasi kamera/scanner langsung ditunda.
- Export SVG dulu. AI dan CDR menyusul setelah cara ekspornya terbukti.
- Auto-trace memakai library yang sudah ada, bukan algoritma buatan sendiri.

## 8. Keputusan
- Platform: aplikasi desktop, OS target Windows.
- Teknologi: C# / .NET (WPF). SDK .NET 10.0.112 terpasang di mesin pengembangan (dicek dengan `dotnet --list-sdks`).
- Ukuran kanvas: bebas diatur operator untuk saat ini.
- Export: SVG cukup untuk saat ini. AI dan CDR ditunda.
- Tujuan proyek saat ini: demo prototipe untuk dipaparkan ke owner.
- Mesin: diberi nama profil "Printech" (nama dari Anda). Spesifikasi mesin Unknown; semua nilai teknis di bawah adalah default umum industri yang bisa diubah.

## 9. Hasil riset (untuk pertanyaan terbuka sebelumnya)
| Topik | Keputusan prototipe | Status |
|-------|--------------------|--------|
| Baca SVG | SharpVectors.Wpf 1.8.6 (lisensi BSD-3, 1,1 jt unduhan) | Terverifikasi di NuGet dan dipakai di prototipe |
| Trace bitmap → vektor | BitmapToVector 1.16.3, port Potrace (2,3rb–21rb unduhan) | Dipakai. **Lisensi GPL-3.0**: jika aplikasi didistribusikan ke luar perusahaan, kodenya harus dibuka. Untuk pemakaian internal aman. Putuskan sebelum produksi. |
| Nesting | Tidak ada library .NET yang layak di NuGet. poly-nest (BSL-1.0) ada di GitHub tetapi tidak di NuGet, 7 commit, tidak aktif. Prototipe memakai nesting sendiri: greedy first-fit di grid 2 mm, 4 rotasi. | Bukan optimal. Ganti dengan NFP/SVGnest bila pemakaian bahan kurang. |
| Offset garis potong | Default 2 mm (praktik umum 2–3 mm), bisa diatur | Default industri, belum dicek ke Printech |
| Format garis potong | Path vektor di layer "CutContour", warna magenta 100% (praktik umum agar RIP membaca sebagai garis potong) | Belum diverifikasi ke Printech |
| Tanda registrasi | 4 kotak hitam 5 mm di sudut, dalam margin 10 mm (bisa dimatikan) | Generik, belum diverifikasi ke Printech |
| Satuan/resolusi | mm; mask trace 6 px/mm; ukuran file bergantung PNG tertanam (base64) | Keputusan prototipe |
| Rotasi | Ya: 0/90/180/270 | Keputusan prototipe |
| Merek "Printech" | Ada produsen mesin potong bernama Printech Machineries (Coimbatore, India) di direktori IndiaMART. Belum diverifikasi sebagai mesin Anda. | Unverified |

Sumber praktik cutline/bleed: Paperlust, Printastic (artikel setup file die-cut sticker).

## 10. Batasan prototipe (diketahui)
- Trace hanya untuk garis potong (siluet). Warna artwork PNG tetap raster; Potrace hanya 1 warna. Menjadikan artwork berwarna vektor penuh butuh pendekatan lain (Unverified).
- Bleed hanya berupa offset garis potong; warna artwork tidak diperluas ke area bleed.
- Foto/scan fisik: siluet diambil dari transparansi, atau dari "bukan putih" bila latar putih. Latar tidak rata butuh penghapus latar (belum ada).
- Ekspor SVG (`<image xlink:href>` + `clipPath`) sudah dibaca ulang dengan SharpVectors, belum diuji di Illustrator, CorelDRAW, atau software Printech.
- Interaksi UI (dialog file, seret, tombol) dicoba terbuka dan responsif, tetapi belum diuji otomatis.

## 11. Menjalankan prototipe
- Jalankan: `dotnet run --project StickerLayout` (perlu .NET SDK/Runtime 10).
- Sampel: folder `samples/` (circle.png, star.png, badge.svg, heart.svg).
- Cek otomatis: `StickerLayout\bin\Debug\net10.0-windows\StickerLayout.exe --selfcheck samples`. Hasil di `samples/selfcheck.txt`.
