# PRD — Printech (Layout Stiker Print & Cut)

Status: Draf 2 · Tanggal: 2026-10-13 · Nama sementara: "Printech" (dikonfirmasi 2026-10-13).

## 1. Tujuan
Ini alat internal toko. Operator mengubah file dari customer menjadi layout stiker yang siap dicetak dan dipotong. Layout harus hemat bahan dan warnanya akurat.

Tahap sekarang: prototipe → cek kualitas → uji oleh operator. Status "production" tercapai bila tidak ada error lagi.

## 2. Pengguna
- Ada dua peran: operator layout dan operator mesin. Di kantor pusat keduanya dipegang orang berbeda; di cabang bisa dipegang orang yang sama.
- Software dipakai internal perusahaan. Ini terhitung pemakaian komersial, tetapi software tidak didistribusikan ke luar.

## 3. Alur
1. Customer menyerahkan file: 80% CorelDRAW & PDF, 20% AI & PNG/JPG. SVG jarang. Barang fisik belum diterima untuk cutting.
2. Operator menambahkan file. Aplikasi membuat garis kiss-cut dan die-cut secara otomatis.
3. Operator mengatur lebar dan jumlah per desain, ukuran kertas, dan mesin, lalu menjalankan "Susun otomatis". Jika satu lembar penuh, lembar berikutnya dibuat otomatis.
4. Operator mengedit hasil susunan bila perlu, lalu export ke PDF.
5. PDF dibuka di CorelDRAW atau Illustrator. Reg mark dan pengiriman ke mesin potong dilakukan lewat Cutting Master 5 (Graphtec), dengan garis potong dipilih berdasarkan warna.

## 4. Kebutuhan fungsional
| ID | Kebutuhan | Status v2 |
|----|-----------|-----------|
| F1 | Input PDF, AI (PDF-compatible), CDR (lewat CorelDRAW yang terpasang), PNG, JPG, SVG. PDF multi-halaman = satu desain per halaman | Ada. Jalur CDR belum diuji |
| F2 | Crop otomatis ke batas artwork. PDF memakai ukuran asli. Lebar dan jumlah bisa diatur per desain | Ada |
| F3 | Garis kiss-cut dan die-cut, masing-masing dengan offset dan penghalus (smooth) sendiri | Ada |
| F4 | Kertas: preset A0–A6, SRA3, SRA4, custom, dan tombol putar orientasi | Ada |
| F5 | Profil mesin Graphtec CE7000 dan FCX2000, operator bisa menambah sendiri. Muncul peringatan bila kertas lebih besar dari area potong | Ada |
| F6 | Susun otomatis ke banyak lembar; lembar baru dibuat otomatis | Ada |
| F7 | Edit hasil susunan: seret, putar 90°, hapus, tombol Delete/R/panah. Muncul peringatan bila tumpang tindih atau keluar margin | Ada |
| F8 | Zoom ala Word: Ctrl+roda, persen, tombol "Pas layar" | Ada |
| F9 | Export PDF: artwork tetap vektor dengan warna asli. Kiss-cut = magenta 100%, die-cut = cyan 100% (CMYK) | Ada |
| F10 | Bahasa UI: Indonesia dan English | Ada |

## 5. Keputusan
- **Platform:** Windows, C#/.NET 10, WPF.
- **Reg mark:** tidak dibuat oleh aplikasi. Reg mark dibuat oleh Cutting Master 5 di CorelDRAW/Illustrator, yang mendukung FCX2000 dan CE7000. Aplikasi hanya menyisakan margin per mesin (default 10 mm, Unverified).
- **Cutting Master 5:** bisa mengatur kondisi potong, urutan, dan potong/tidak per warna atau per layer.
- **Export:** PDF menggantikan SVG dari v1. Alasannya: vektor dan CMYK tetap terjaga, dan satu file bisa berisi banyak halaman.
- **Nesting:** greedy first-fit di grid 2 mm. Pada uji 130 stiker, grid 1 mm 5× lebih lambat dengan jumlah lembar yang sama.
- **Tidak dibangun** (sesuai jawaban Anda): hide copy, magic eraser, background remover, simpan proyek, dan reg mark buatan aplikasi.

## 6. Dependensi dan lisensi
| Paket | Fungsi | Lisensi |
|---|---|---|
| SharpVectors.Wpf 1.8.6 | Membaca SVG | BSD-3 |
| BitmapToVector 1.16.3 | Potrace, untuk garis potong | GPL-3.0. Aman untuk pemakaian internal; jangan didistribusikan ke luar perusahaan |
| Docnet.Core 2.6.0 | Render PDF/AI (PDFium) | MIT |
| PDFsharp 6.2.4 | Menulis PDF | MIT |

## 7. Batasan yang diketahui
- Jalur CDR lewat COM CorelDRAW belum diuji, karena CorelDRAW tidak terpasang di PC pengembangan.
- PDF yang diputar (/Rotate) atau tidak bisa dibaca PDFsharp akan ditempel sebagai gambar (±254 dpi, RGB). Aplikasi memberi peringatan bila ini terjadi.
- SVG di-export sebagai gambar ±300 dpi.
- Hanya kontur luar yang dipotong. Lubang di bagian dalam desain tidak ikut dipotong.
- Nesting belum optimal (greedy).
- Belum ada undo. Stiker belum bisa dipindah antar-lembar. Rotasi hanya kelipatan 90°.
- Warna di layar hanya preview. Akurasi warna dijaga di file export.

## 8. Pertanyaan terbuka
1. Setelah export, file dibuka di aplikasi apa (CorelDRAW + Cutting Master 5?), format apa yang diterima, dan garis potong dikenali lewat warna atau layer apa?
2. File format reg mark milik operator (akan dikirim menyusul).
3. Ukuran media yang dipakai dan margin printer. Juga offset, jarak antar-stiker, dan bleed standar toko.
4. Volume order harian dan spesifikasi PC operator.
5. Model mesin yang dipakai: CE7000-40/-60/-130/-160 dan FCX2000-60VC/-120VC/-180VC.

## 9. Checklist uji operator
1. Tambahkan file CDR. Apakah CorelDRAW terbuka dan desainnya ikut masuk?
2. Export PDF, lalu buka di CorelDRAW. Apakah artwork tajam dan warnanya sama dengan file asli?
3. Di Cutting Master 5, pilih per warna: magenta = kiss-cut, cyan = die-cut. Apakah potongannya sesuai?
4. Cetak dan potong fisik di FCX2000 atau CE7000. Berapa mm selisih potongannya?
5. Catat setiap error atau kejanggalan, lalu kirim ke pengembang.

## 10. Menjalankan
- **Menjalankan aplikasi:** `dotnet run --project StickerLayout` (perlu .NET 10). File juga bisa dibuka sebagai argumen, misalnya lewat "Open with" atau dengan menyeret file ke ikon aplikasi.
- **File contoh:** folder `samples/` (circle.png, star.jpg, badge.svg, logo.pdf, logo.ai, dan lainnya).
- **Cek otomatis:** `StickerLayout\bin\Debug\net10.0-windows\StickerLayout.exe --selfcheck samples`. Hasilnya ditulis ke `samples/selfcheck.txt`.
