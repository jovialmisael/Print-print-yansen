# PRD — Printech (Layout Stiker Print & Cut)

Status: Draf 3 · Tanggal: 2026-10-14 · Nama sementara: "Printech" (dikonfirmasi 2026-10-13).

## 1. Tujuan
Ini alat internal toko. Operator mengubah file dari customer menjadi layout stiker yang siap dicetak dan dipotong. Layout harus hemat bahan dan warnanya akurat.

Tahap sekarang: prototipe → cek kualitas → uji oleh operator. Status "production" tercapai bila tidak ada error lagi.

**Tujuan utama: mempercepat proses** (konfirmasi 2026-10-14). Saat ini Photoshop dipakai hanya untuk merapikan gambar, bukan untuk layout, dan pekerjaan itu memakan banyak waktu. Printech tidak bertujuan mengurangi pemakaian Adobe.

**Cakupan:** stiker saja (kiss-cut dan die-cut). Nametag, kartu, dan dokumen tidak termasuk.

## 2. Pengguna
- Ada dua peran: operator layout dan operator mesin. Di kantor pusat keduanya dipegang orang berbeda; di cabang bisa dipegang orang yang sama.
- Software dipakai internal perusahaan. Ini terhitung pemakaian komersial, tetapi software tidak didistribusikan ke luar.

## 3. Observasi AP 55 (PPT leader proyek, 2026-10-14)
- Cabang AP 55 (Angkasa Putra). Tiga operator memegang semua mesin. Mesin cetak stiker: printer laser **Develop**; mesin potong: **Graphtec**.
- Alur stiker: file masuk lewat WhatsApp, email, atau flashdisk → operator mengecek file → layout garis potong dan posisi cetak di CorelDRAW → cetak di Develop → kiss-cut di Graphtec.
- Operator mengirim tampilan file ke customer untuk dikonfirmasi ("apakah sesuai?") sebelum bon dicetak.
- Photoshop dipakai untuk merapikan gambar. File AI diubah dulu ke PDF, lalu dilanjutkan di CorelDRAW.
- Produk teratas: stiker vinyl kiss-cut (offline) dan stiker dalam jumlah besar (online).
- Kertas stiker di Develop: A3+ 32×48 cm.

## 4. Alur di Printech
1. Customer menyerahkan file: 80% CorelDRAW & PDF, 20% AI & PNG/JPG. SVG jarang. Barang fisik belum diterima untuk cutting.
2. Operator menambahkan file. Aplikasi membuat garis kiss-cut dan die-cut secara otomatis.
3. Operator mengatur lebar dan jumlah per desain, ukuran kertas, dan margin tepi, lalu menjalankan "Susun otomatis". Jika satu lembar penuh, lembar berikutnya dibuat otomatis.
4. Operator mengedit hasil susunan bila perlu, lalu export ke PDF.
5. PDF dibuka di CorelDRAW atau Illustrator. Reg mark dan pengiriman ke mesin potong dilakukan lewat Cutting Master 5 (Graphtec), dengan garis potong dipilih berdasarkan warna.

## 5. Kebutuhan fungsional
| ID | Kebutuhan | Status v2 |
|----|-----------|-----------|
| F1 | Input PDF, AI (PDF-compatible), CDR (lewat CorelDRAW yang terpasang), PNG, JPG, SVG. PDF multi-halaman = satu desain per halaman | Ada. Jalur CDR belum diuji |
| F2 | Crop otomatis ke batas artwork. PDF memakai ukuran asli. Lebar dan jumlah bisa diatur per desain | Ada |
| F3 | Garis kiss-cut dan die-cut, masing-masing dengan offset dan penghalus (smooth) sendiri | Ada |
| F4 | Kertas: preset A0–A6, **A3+ 320×480 (default)**, SRA3, SRA4, custom, dan tombol putar orientasi | Ada |
| F5 | Margin tepi kertas bisa diatur (area kosong untuk reg mark/printer) | Ada |
| F6 | Susun otomatis ke banyak lembar; lembar baru dibuat otomatis | Ada |
| F7 | Edit hasil susunan: seret, putar 90°, hapus, tombol Delete/R/panah. Muncul peringatan bila tumpang tindih atau keluar margin | Ada |
| F8 | Zoom ala Word: Ctrl+roda (titik di bawah kursor tetap), persen, tombol "Pas layar". Alat Hand/Zoom, tahan Spasi atau tombol tengah mouse untuk geser tampilan | Ada |
| F9 | Export PDF: artwork tetap vektor dengan warna asli. Kiss-cut = magenta 100%, die-cut = cyan 100% (CMYK) | Ada |
| F10 | Bahasa UI: Indonesia dan English | Ada |
| F11 | UI gaya Photoshop: tema gelap, menu, options bar, batang alat, panel lipat (Desain dengan thumbnail, Pilihan, Kertas, Garis potong), status bar dengan meter bahan dan peringatan, drag & drop file, file langsung disusun saat ditambahkan | Ada |
| F12 | Resolusi cetak (dpi) tiap desain yang ditempel sebagai gambar. Merah dan masuk peringatan bila di bawah 150 dpi (target 300 dpi). Gunanya menangkap foto terkompres dari WhatsApp sebelum dicetak | Ada |
| F13 | Export preview JPG (Ctrl+Shift+E): satu JPG per lembar, sisi panjang ±2000 px, untuk konfirmasi ke customer lewat WhatsApp | Ada |
| F14 | Shortcut ala CorelDRAW: Ctrl+I = tambah file, F4 / Shift+F4 = pas layar (selain Ctrl+O dan Ctrl+0) | Ada |

## 6. Keputusan
- **Platform:** Windows, C#/.NET 10, WPF.
- **Reg mark:** tidak dibuat oleh aplikasi. Reg mark dibuat oleh Cutting Master 5 di CorelDRAW/Illustrator, yang mendukung FCX2000 dan CE7000. Aplikasi hanya menyisakan margin tepi kertas (default 10 mm, Unverified).
- **Batas aplikasi** (keputusan leader proyek, 2026-10-14): aplikasi hanya membuat layout. Pengaturan dan urusan mesin tetap dikerjakan operator; profil mesin dihapus dari aplikasi.
- **Cutting Master 5:** bisa mengatur kondisi potong, urutan, dan potong/tidak per warna atau per layer.
- **Export:** PDF menggantikan SVG dari v1. Alasannya: vektor dan CMYK tetap terjaga, dan satu file bisa berisi banyak halaman.
- **Nesting:** greedy first-fit di grid 2 mm. Pada uji 130 stiker, grid 1 mm 5× lebih lambat dengan jumlah lembar yang sama.
- **Ringan:** render tampilan lewat CPU, bukan GPU. Diukur: RAM 118–134 MB (lewat GPU 467 MB, sebagian besar driver grafis), CPU saat diam 0%, jendela muncul ±2 detik. Ganti zoom 0,1–0,4 detik CPU.
- **Aman:** tanpa koneksi jaringan. File asli customer hanya dibaca. Yang ditulis hanya PDF export (lokasi dipilih operator) dan folder temp milik aplikasi, yang dihapus saat aplikasi ditutup. Error tak terduga tampil sebagai pesan, aplikasi tetap jalan. PDFium versi terbaru (build 8100, 2026) dipakai untuk membaca PDF customer.
- **Kertas default A3+ 320×480 mm** (kertas stiker Develop di AP 55). F4/folio tidak ditambahkan karena cakupan hanya stiker.
- **Ambang resolusi 150 dpi** masih tebakan awal (target riset 300 ppi); disesuaikan setelah melihat hasil cetak Develop.
- **Tidak dibangun:** hide copy, simpan proyek, reg mark buatan aplikasi, dan mode nametag/kartu.

## 7. Dependensi dan lisensi
| Paket | Fungsi | Lisensi |
|---|---|---|
| SharpVectors.Wpf 1.8.6 | Membaca SVG | BSD-3 |
| BitmapToVector 1.16.3 | Potrace, untuk garis potong | GPL-3.0. Aman untuk pemakaian internal; jangan didistribusikan ke luar perusahaan |
| bblanchon.PDFium.Win32 157.0.8100 | Render PDF/AI (PDFium, hanya binary Windows) | Apache-2.0 |
| PDFsharp 6.2.4 | Menulis PDF | MIT |

## 8. Batasan yang diketahui
- Jalur CDR lewat COM CorelDRAW belum diuji, karena CorelDRAW tidak terpasang di PC pengembangan.
- PDF yang diputar (/Rotate) atau tidak bisa dibaca PDFsharp akan ditempel sebagai gambar (±254 dpi, RGB). Aplikasi memberi peringatan bila ini terjadi.
- SVG di-export sebagai gambar ±300 dpi.
- Hanya kontur luar yang dipotong. Lubang di bagian dalam desain tidak ikut dipotong.
- Nesting belum optimal (greedy).
- Belum ada undo. Stiker belum bisa dipindah antar-lembar. Rotasi hanya kelipatan 90°.
- Warna di layar hanya preview. Akurasi warna dijaga di file export.

## 9. Pertanyaan terbuka
| Pertanyaan | Ditanyakan ke | Default sampai dijawab |
|---|---|---|
| Format reg mark milik operator | operator (file menyusul) | margin 10 mm |
| Offset, jarak antar-stiker, dan bleed standar toko | operator | kiss 2/1 mm, die 4/2 mm, jarak 2 mm |
| Spesifikasi PC operator | operator | Windows 10/11 64-bit |
| Ambang dpi yang masih bisa diterima | operator, dari hasil cetak | 150 dpi |
| Seberapa sering latar foto atau gambar pecah muncul | operator, setelah uji | hapus latar dengan AI ditunda |
| Garis potong di Cutting Master 5 dikenali lewat warna atau layer | operator | warna (magenta/cyan) |

## 10. Checklist uji operator
1. Tambahkan file CDR. Apakah CorelDRAW terbuka dan desainnya ikut masuk?
2. Tarik file dari Explorer ke jendela Printech. Coba alat Geser tampilan (H atau tahan Spasi), Zoom (Z), dan shortcut Ctrl+I, F4, R, Delete, panah.
3. Export preview JPG, kirim ke WhatsApp. Apakah cukup jelas untuk konfirmasi ke customer?
4. Masukkan foto yang diterima lewat WhatsApp. Apakah angka dpi merah muncul untuk file yang hasil cetaknya memang pecah?
5. Export PDF, lalu buka di CorelDRAW. Apakah artwork tajam dan warnanya sama dengan file asli?
6. Di Cutting Master 5, pilih per warna: magenta = kiss-cut, cyan = die-cut. Apakah potongannya sesuai?
7. Cetak di Develop (A3+) dan potong fisik di Graphtec. Berapa mm selisih potongannya?
8. PDF dari customer yang punya latar putih penuh: apakah garis potongnya ikut menjadi kotak?
9. Catat setiap error atau kejanggalan, lalu kirim ke pengembang.

## 11. Menjalankan
- **Menjalankan aplikasi:** `dotnet run --project StickerLayout` (perlu .NET 10). File juga bisa dibuka sebagai argumen, misalnya lewat "Open with" atau dengan menyeret file ke ikon aplikasi.
- **File contoh:** folder `samples/` (circle.png, star.jpg, badge.svg, logo.pdf, logo.ai, dan lainnya).
- **Cek otomatis:** `StickerLayout\bin\Debug\net10.0-windows\StickerLayout.exe --selfcheck samples`. Hasilnya ditulis ke `samples/selfcheck.txt`.
