# PRD — Printech (Layout Stiker Print & Cut)

Status: Draf 4 · Tanggal: 2026-10-17 · Nama sementara: "Printech" (dikonfirmasi 2026-10-13).

## 1. Tujuan
Ini alat internal toko. Operator mengubah file dari customer menjadi layout stiker yang siap dicetak dan dipotong. Layout harus hemat bahan dan warnanya akurat.

Tahap sekarang: prototipe → cek kualitas → uji oleh operator. Status "production" tercapai bila tidak ada error lagi.

**Cakupan prototipe** (2026-10-17): automasi layout saja, yaitu import file, garis potong otomatis, susun otomatis, dan export PDF/preview JPG. Estimasi pengerjaan 2 minggu. Dokumentasi disimpan di repo GitHub ini.

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
| F1 | Input PDF, AI (PDF-compatible), CDR (lewat CorelDRAW yang terpasang, di proses worker terpisah yang dihentikan setelah 30 detik bila CorelDRAW tidak merespons), PNG, JPG, SVG. PDF multi-halaman = satu desain per halaman | Ada. Jalur CDR belum diuji |
| F2 | Crop otomatis ke batas artwork. PDF memakai ukuran asli. Lebar dan jumlah bisa diatur per desain | Ada |
| F3 | Garis kiss-cut dan die-cut, masing-masing dengan offset dan penghalus (smooth) sendiri | Ada |
| F4 | Kertas: preset A0–A6, **A3+ 320×480 (default)**, SRA3, SRA4, custom, dan tombol putar orientasi | Ada |
| F5 | Margin tepi kertas bisa diatur (area kosong untuk reg mark/printer) | Ada |
| F6 | Susun otomatis ke banyak lembar; lembar baru dibuat otomatis | Ada |
| F7 | Edit hasil susunan: seret, putar 90°, hapus, tombol Delete/R/panah. Muncul peringatan bila tumpang tindih atau keluar margin | Ada |
| F8 | Zoom ala Word: Ctrl+roda (titik di bawah kursor tetap), persen, tombol "Pas layar". Alat Hand/Zoom, tahan Spasi atau tombol tengah mouse untuk geser tampilan | Ada |
| F9 | Export PDF: artwork tetap vektor dengan warna asli. Kiss-cut = magenta 100%, die-cut = cyan 100%, DeviceCMYK murni (tanpa RGB/sRGB atau profil ICC; dicek otomatis di S8) | Ada |
| F10 | Bahasa UI: Indonesia dan English | Ada |
| F11 | UI gaya Photoshop: tema gelap, menu, options bar, batang alat, panel lipat (Desain dengan thumbnail, Pilihan, Kertas, Garis potong), status bar dengan meter bahan dan peringatan, drag & drop file, file langsung disusun saat ditambahkan | Ada |
| F12 | Resolusi cetak (dpi) tiap desain yang ditempel sebagai gambar. Merah dan masuk peringatan bila di bawah 150 dpi (target 300 dpi). Gunanya menangkap foto terkompres dari WhatsApp sebelum dicetak | Ada |
| F13 | Export preview JPG (Ctrl+Shift+E): satu JPG per lembar, sisi panjang ±2000 px, untuk konfirmasi ke customer lewat WhatsApp | Ada |
| F14 | Shortcut ala CorelDRAW: Ctrl+I = tambah file, F4 / Shift+F4 = pas layar (selain Ctrl+O dan Ctrl+0) | Ada |
| F15 | Rapikan gambar (pengganti langkah Photoshop), untuk desain yang ditempel sebagai gambar: hapus latar otomatis, penghapus klik, toleransi warna, kikis tepi 0–3 px, urungkan dan kembalikan. Dibuka lewat ikon penghapus atau dobel-klik thumbnail. Garis potong dihitung ulang dari hasilnya | Ada. Di luar cakupan prototipe: tidak dikembangkan |
| F16 | Garis potong dengan node sedikit dan kurva mulus untuk pisau Graphtec: tanpa sudut tajam; slider "Halus" sekarang juga menghaluskan bentuk sehingga node berkurang (lihat Keputusan) | Ada |
| F17 | Bleed untuk gambar PNG/JPG (opsional, 0 = mati): warna tepi diperluas sampai bleed mm di luar garis kiss, supaya pergeseran pisau tidak menyisakan tepi putih. Maksimal sama dengan jarak antar stiker | Ada |
| F18 | Indikator bahan: "Bahan terpakai" dihitung terhadap area kertas di dalam margin, berwarna hijau/kuning/merah menurut target bentuk reguler (70–85%, hijau ≥ 75%) atau irregular (55–70%, hijau ≥ 60%). Yield per lembar ("24 pcs / lembar A3+") dan sisa bahan | Ada |
| F19 | Snap magnet: stiker yang digeser atau diputar pindah ke posisi kosong terdekat (tidak bertabrakan, jarak antar stiker tetap). Preset bahan (Vinyl kiss-cut, Chromo die-cut, Akrilik/tebal) mengisi garis potong, margin, dan jarak | Ada |
| F20 | Print Ticket: PDF A4 1 halaman berisi total lembar, total pcs, efisiensi bahan, rincian per lembar dan per desain, serta pengaturan potong, untuk lampiran nota kasir/SPK | Ada |

## 6. Keputusan
- **Platform:** Windows, C#/.NET 10, WPF.
- **Reg mark:** tidak dibuat oleh aplikasi. Reg mark dibuat oleh Cutting Master 5 di CorelDRAW/Illustrator, yang mendukung FCX2000 dan CE7000. Aplikasi hanya menyisakan margin tepi kertas (default 10 mm, Unverified).
- **Batas aplikasi** (keputusan leader proyek, 2026-10-14): aplikasi hanya membuat layout. Pengaturan dan urusan mesin tetap dikerjakan operator; profil mesin dihapus dari aplikasi.
- **Cutting Master 5:** bisa mengatur kondisi potong, urutan, dan potong/tidak per warna atau per layer.
- **Export:** PDF menggantikan SVG dari v1. Alasannya: vektor dan CMYK tetap terjaga, dan satu file bisa berisi banyak halaman.
- **Nesting:** greedy first-fit di grid 2 mm. Pada uji 130 stiker, grid 1 mm 5× lebih lambat dengan jumlah lembar yang sama.
- **Ringan:** render tampilan lewat CPU, bukan GPU. Diukur: RAM 118–134 MB (lewat GPU 467 MB, sebagian besar driver grafis), CPU saat diam 0%, jendela muncul ±2 detik. Ganti zoom 0,1–0,4 detik CPU. Layar memakai salinan gambar maksimal 1600 px (export tetap resolusi asli): dengan satu gambar 4000×3000 px RAM 186 MB, jendela Rapikan 310 MB, kembali ke 267 MB setelah ditutup. Hapus latar 4000×3000 px: 0,5 detik.
- **Aman:** tanpa koneksi jaringan. File asli customer hanya dibaca. Yang ditulis hanya PDF export (lokasi dipilih operator) dan folder temp milik aplikasi, yang dihapus saat aplikasi ditutup. Error tak terduga tampil sebagai pesan, aplikasi tetap jalan. PDFium versi terbaru (build 8100, 2026) dipakai untuk membaca PDF customer.
- **Kertas default A3+ 320×480 mm** (kertas stiker Develop di AP 55). F4/folio tidak ditambahkan karena cakupan hanya stiker.
- **Ambang resolusi 150 dpi** masih tebakan awal (target riset 300 ppi); disesuaikan setelah melihat hasil cetak Develop.
- **Rapikan gambar hanya untuk raster** (PNG, JPG, SVG, PDF yang gagal dibaca vektor). File vektor (PDF/AI/CDR) tetap dirapikan di CorelDRAW supaya warna CMYK dan vektornya utuh. Rapikan hanya mengubah transparansi, tidak menggambar ulang piksel.
- **Reduksi node garis potong (2026-10-16).** RDP literal tidak dipakai. Potrace sudah menghasilkan kurva Bezier yang dioptimasi; RDP hanya bekerja pada garis lurus, jadi kurva harus dipecah dulu. Hasil ukur: teks butuh 117 titik garis lurus untuk akurasi 0,1 mm, sedangkan Bezier cukup 72 node. Yang dipakai:
  - Tuning Potrace: `AlphaMax` 1,3334 (tidak ada sudut tajam, pisau tidak berbelok mendadak) dan `OptTolerance` 1 px = 0,17 mm (lebih banyak segmen kurva digabung).
  - Penghalusan bentuk sebelum trace: blur dengan sigma = nilai Halus (3 lintasan box blur, mendekati Gaussian), lalu threshold. Lekukan kecil hilang. Halus 1–2 mm paling efektif; di atas 2 mm node bisa naik lagi.
  - Penjaga jarak: garis tidak pernah lebih dekat dari offset - 0,3 mm ke artwork. Garis die selalu membungkus garis kiss.
  - Hasil di app (kiss 2/1 mm, die 4/2 mm), node kiss/die sebelum -> sesudah: rough.jpg 64/43 -> 16/13 (-73%), lingkaran 11/10 -> 3/4 (-67%), logo.pdf 21/15 -> 8/7 (-58%), bintang 29/27 -> 15/17 (-43%), badge 9/8 -> 8/8. Sudut tajam 2 -> 0. Muat per desain 20-70 ms, stiker 300 mm ±1 detik (Release).
  - Skenario uji S1–S11 beserta hasilnya: [UJI-GARIS-POTONG.md](UJI-GARIS-POTONG.md).
- **Stabilitas dan performa** (2026-10-17, spesifikasi leader bagian 2):
  - CorelDRAW COM berjalan di proses worker (exe yang sama dengan argumen `--cdr2pdf`), bukan di proses utama. Selama menunggu, UI tetap jalan. Lewat 30 detik, worker dihentikan dan operator mendapat pesan; Printech tetap jalan. CorelDRAW sendiri tidak dimatikan supaya pekerjaan operator yang belum disimpan aman.
  - 3 percobaan nesting dan hapus latar dihitung paralel dengan `Parallel.For`. Spesifikasi menyebut `Parallel.ForEachAsync`, tetapi API itu untuk kerja async (I/O), sedangkan kerja ini murni CPU. Hasilnya identik bit per bit dengan versi berurutan.
  - Hasil ukur (Release, CPU 12 thread): nesting 130 stiker 57 → 43 ms. Gambar 4000×3000: hapus latar 119 → 97 ms (flood fill tetap berurutan), kikis tepi 103 → 31 ms, tampilan 62 → 20 ms. RAM saat diam tidak berubah (versi lama 148–152 MB, versi baru 148–153 MB, 4 kali ukur).
  - Skenario uji P1–P10: [UJI-STABILITAS.md](UJI-STABILITAS.md).
- **Prepress** (2026-10-17, spesifikasi leader bagian 3):
  - Garis potong ditulis DeviceCMYK murni lewat operator `K`: kiss `0 1 0 0`, die `1 0 0 0`. Tidak ada operator RGB, ruang warna ICC, atau OutputIntent, jadi tidak ada konversi ke sRGB. Operator lain yang ada hanya `0 g`/`0 G` (DeviceGray), yang ditulis PDFsharp sebelum artwork vektor ditempel; itu bukan warna garis potong.
  - Bleed: gambar bleed dibuat saat desain dimuat (maksimal 300 dpi), lalu dipakai untuk layar, preview JPG, dan PDF. Area yang dicetak = garis kiss (tanpa kiss: garis die) diperlebar sebesar bleed. Pada JPG berlatar putih, tepi luar 2 px diganti warna dari dalam supaya bleed tidak pucat. File vektor tidak diberi bleed.
  - Bleed ikut mengisi border putih antara artwork dan garis kiss. Pakai bersama offset kecil (0–0,5 mm) untuk stiker tanpa border.
- **Indikator dan UX operator** (2026-10-17, spesifikasi leader bagian 4):
  - Bentuk reguler atau irregular ditentukan otomatis: bila rata-rata isi bentuk terhadap kotaknya (ditimbang luas) ≥ 75%, dihitung reguler (persegi ±1, lingkaran ±0,79, bintang ±0,57). Batas hijau irregular (60%) tidak disebut di spesifikasi; dipakai aturan yang sama dengan reguler, yaitu batas bawah + 5 poin.
  - Snap memakai grid nesting 2 mm (posisi kosong terdekat), bukan NFP poligon. Geser dengan panah keyboard tetap bebas. Bila tidak ada tempat kosong, stiker tetap di posisinya dan ditandai merah.
  - Preset bahan memakai nilai awal dari panduan cetak stiker publik ([RESEARCH.md](RESEARCH.md) bagian 13), belum dikonfirmasi operator. Memilih preset langsung menyusun ulang.
- **Ditunda sampai ada kebutuhan nyata:** hapus latar foto dengan AI dan memperbesar gambar pecah. Untuk gambar pecah, cara tercepat tetap meminta file asli ke customer; angka dpi merah memberi tahu kapan perlu.
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
- Hanya kontur luar yang dipotong. Lubang di bagian dalam desain tidak ikut dipotong, termasuk lubang yang terbentuk saat offset menutup celah artwork (misalnya huruf C besar).
- Nesting belum optimal (greedy).
- Belum ada undo. Stiker belum bisa dipindah antar-lembar. Rotasi hanya kelipatan 90°.
- Warna di layar hanya preview. Akurasi warna dijaga di file export.
- Rapikan gambar: latar foto yang ramai (bukan warna polos) belum bisa dihapus otomatis; gambar di atas 4000 px diperkecil ke 4000 px saat dirapikan.
- Reduksi node: target 60-80% tercapai pada logo kasar dan bentuk bulat. Teks dan bintang turun 33-59% karena sudutnya adalah detail asli. Di area cekung garis bisa bergeser keluar sampai ±0,5 mm (kiss) atau ±0,7 mm (die): border sedikit lebih lebar. Halus 0 mematikan penghalusan bentuk.
- CDR: batas 30 detik belum dikalibrasi di PC operator. Bila CorelDRAW menggantung karena dialog yang tidak terlihat, tutup CorelDRAW lalu tambahkan file lagi.
- Muat ulang desain dan nesting masih berjalan di UI thread (nesting 130 stiker ±43 ms; desain sangat besar bisa ±1 detik). RAM saat diam 136–153 MB, di atas target ±120 MB.
- Bleed: tepi JPG yang sangat kasar (kompresi tinggi) bisa menyisakan garis pucat tipis di area bleed; rapikan gambarnya dulu untuk hasil terbaik. Stiker raster sangat besar (±300 mm) dengan bleed dimuat ±1–2 detik lebih lama.

## 9. Pertanyaan terbuka
| Pertanyaan | Ditanyakan ke | Default sampai dijawab |
|---|---|---|
| Format reg mark milik operator | operator (file menyusul) | margin 10 mm |
| Offset, jarak antar-stiker, dan bleed standar toko | operator | kiss 2/1 mm, die 4/2 mm, jarak 2 mm |
| Spesifikasi PC operator | operator | Windows 10/11 64-bit |
| Ambang dpi yang masih bisa diterima | operator, dari hasil cetak | 150 dpi |
| Seberapa sering latar foto atau gambar pecah muncul | operator, setelah uji | hapus latar dengan AI ditunda |
| Garis potong di Cutting Master 5 dikenali lewat warna atau layer | operator | warna (magenta/cyan) |
| Nilai preset per bahan (offset, halus, jarak, margin) | operator | nilai awal dari panduan publik (RESEARCH §13) |

## 10. Checklist uji operator
1. Tambahkan file CDR. Apakah CorelDRAW terbuka dan desainnya ikut masuk? Berapa detik prosesnya, dan apakah Printech tetap bisa dipakai selama menunggu?
2. Tarik file dari Explorer ke jendela Printech. Coba alat Geser tampilan (H atau tahan Spasi), Zoom (Z), dan shortcut Ctrl+I, F4, R, Delete, panah.
3. Export preview JPG, kirim ke WhatsApp. Apakah cukup jelas untuk konfirmasi ke customer?
4. Masukkan foto yang diterima lewat WhatsApp. Apakah angka dpi merah muncul untuk file yang hasil cetaknya memang pecah?
5. Rapikan 3–5 gambar customer yang biasanya dibersihkan di Photoshop. Berapa menit dibanding Photoshop? Apa yang belum bisa dikerjakan?
6. Export PDF, lalu buka di CorelDRAW. Apakah artwork tajam dan warnanya sama dengan file asli?
7. Di Cutting Master 5, pilih per warna: magenta = kiss-cut, cyan = die-cut. Apakah potongannya sesuai?
8. Cetak di Develop (A3+) dan potong fisik di Graphtec. Berapa mm selisih potongannya? Apakah gerakan pisau mulus pada teks dan logo bertepi kasar? Langkah lengkap: [UJI-GARIS-POTONG.md](UJI-GARIS-POTONG.md) S10.
9. PDF dari customer yang punya latar putih penuh: apakah garis potongnya ikut menjadi kotak?
10. Buka PDF di CorelDRAW lalu klik garis kiss dan die: warnanya harus C0 M100 Y0 K0 dan C100 M0 Y0 K0 (CMYK, bukan RGB). Cetak satu desain PNG dengan bleed 2 mm dan offset kiss 0,5 mm, lalu potong: apakah tepi stiker tetap berwarna tanpa garis putih?
11. Pilih preset sesuai bahan yang dicetak. Apakah offset, halus, dan jaraknya cocok? Export Print Ticket lalu lampirkan ke SPK: apakah isinya cukup untuk kasir?
12. Catat setiap error atau kejanggalan, lalu kirim ke pengembang.

## 11. Menjalankan
- **Untuk operator (tanpa memasang .NET):**
  1. Salin `Printech-win-x64.zip` (64 MB) ke PC operator.
  2. Klik kanan zip → Properties → centang **Unblock** → OK. Langkah ini mencegah peringatan Windows SmartScreen, karena exe belum ditandatangani.
  3. Ekstrak (142 MB), lalu jalankan `Printech\StickerLayout.exe`. Bila SmartScreen tetap muncul: **More info → Run anyway**.
  4. Butuh Windows 10/11 64-bit (belum diuji di PC operator). Build Release: jendela muncul ±0,7 detik, RAM ±124 MB.
- **Membuat zip untuk operator:** `dotnet publish StickerLayout -c Release -r win-x64 --self-contained -o publish\Printech`, lalu zip folder `publish\Printech`. Folder `publish/` tidak masuk git.
- **Menjalankan aplikasi:** `dotnet run --project StickerLayout` (perlu .NET 10). File juga bisa dibuka sebagai argumen, misalnya lewat "Open with" atau dengan menyeret file ke ikon aplikasi.
- **File contoh:** folder `samples/` (circle.png, star.jpg, badge.svg, logo.pdf, logo.ai, logo-on-blue.jpg untuk mencoba Rapikan, dan lainnya).
- **Cek otomatis:** `StickerLayout\bin\Debug\net10.0-windows\StickerLayout.exe --selfcheck samples`. Hasilnya ditulis ke `samples/selfcheck.txt`. Skenario garis potong (S1–S9, S11) dan stabilitas (P1–P5) ikut dijalankan; satu saja gagal, exit code 1.
