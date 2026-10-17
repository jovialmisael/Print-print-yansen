# Skenario uji: stabilitas dan performa (spesifikasi bagian 2)

Fitur yang diuji:
- konversi CDR di proses worker, dengan batas waktu 30 detik;
- UI tetap jalan selama menunggu CorelDRAW;
- 3 percobaan nesting yang dijalankan paralel;
- hapus latar yang dijalankan paralel.

Lihat [PRD.md](PRD.md), bagian Keputusan "Stabilitas dan performa".

Cara menjalankan:
- **Otomatis:** jalankan `StickerLayout.exe --selfcheck <folder>`. Hasilnya tertulis di `selfcheck.txt` sebagai baris `P1 LULUS ...` sampai `P5`. Bila satu skenario gagal, exit code 1.
- **Pengembang:** P6 diukur dengan Task Manager. P1 juga dicek lewat UI.
- **Operator:** P7–P10 butuh PC dengan CorelDRAW terpasang.

| ID | Skenario | Kriteria lulus | Cara | Hasil 2026-10-17 |
|---|---|---|---|---|
| P1 | Tambah file CDR di PC tanpa CorelDRAW | Muncul pesan "CorelDRAW tidak terpasang"; aplikasi tidak crash | Otomatis + UI | Lulus. Selfcheck: pesan dalam 0,3 detik. Lewat UI: pesan muncul 1,1 detik setelah file ditambahkan, aplikasi tetap responsif |
| P2 | Worker melewati batas waktu | Worker dihentikan, muncul pesan batas waktu, tidak ada worker yang tersisa | Otomatis (batas diset 1 ms) | Lulus: dihentikan dalam 20–36 ms, 0 worker tersisa |
| P3 | Nesting paralel dijalankan 5 kali | Hasilnya identik setiap kali | Otomatis | Lulus: 5 kali, 1 hasil. Jumlah lembar dan pemakaian bahan (8 lembar A4 / 45,2%; 2 lembar / 36,1%) sama dengan versi sebelum paralel |
| P4 | Hapus latar paralel dibandingkan dengan versi berurutan | Kikis tepi 1–3 px dan gambar tampilan identik bit per bit | Otomatis | Lulus |
| P5 | Lama kerja di UI thread | Susun 130 stiker < 500 ms; satu langkah Rapikan pada gambar 4000×3000 < 2000 ms (waktu terbaik dari 3 kali) | Otomatis | Lulus. Release: 36–39 ms dan 292–350 ms. Debug: 86–289 ms dan 680–819 ms. Angka naik bila aplikasi lain sedang memakai CPU |
| P6 | RAM saat diam, dengan 4 file contoh | Target spesifikasi ±120 MB | Pengembang | Belum tercapai: 148–153 MB. Versi sebelum perubahan juga 148–152 MB, jadi tidak naik |
| P7 | CDR asli, saat CorelDRAW tertutup dan saat terbuka | Desain masuk; lama proses dicatat untuk mengkalibrasi batas 30 detik | Operator | Belum diuji |
| P8 | CorelDRAW sedang dipakai dan ada dokumen yang belum disimpan | Dokumen operator tetap utuh dan CorelDRAW tidak tertutup | Operator | Belum diuji |
| P9 | CorelDRAW sedang menampilkan dialog | Printech tetap bisa dipakai selama menunggu; bila CorelDRAW tidak merespons, pesan muncul setelah 30 detik; setelah dialog ditutup, tambah ulang berhasil | Operator | Belum diuji |
| P10 | Tiga file CDR sekaligus, lalu ubah offset dan klik Susun | Ketiganya masuk berurutan dan UI tetap responsif; saat Susun, CorelDRAW tidak dibuka lagi | Operator | Belum diuji |

## Langkah operator (P7–P10)
1. **P7:** Tutup CorelDRAW. Tambahkan satu file CDR yang biasa dipakai, lalu catat berapa detik dari klik sampai desain muncul. Ulangi dengan CorelDRAW dalam keadaan terbuka.
2. **P8:** Buka CorelDRAW, buat dokumen baru, gambar sesuatu, dan jangan disimpan. Tambahkan file CDR di Printech. Setelah selesai, pastikan dokumen di CorelDRAW masih ada.
3. **P9:**
   - Di CorelDRAW, buka sebuah dialog (misalnya Options, Ctrl+J) dan biarkan terbuka.
   - Tambahkan file CDR di Printech. Selama menunggu, coba geser dan zoom lembar di Printech.
   - Bila CorelDRAW tidak merespons, setelah ±30 detik harus muncul pesan "CorelDRAW tidak merespons".
   - Tutup dialog di CorelDRAW, lalu tambahkan file itu lagi.
4. **P10:**
   - Pilih tiga file CDR sekaligus. Selama proses, status bar menampilkan "Membuka … di CorelDRAW".
   - Setelah semua masuk, ubah offset kiss lalu klik Susun. Teks "Membuka … di CorelDRAW" tidak boleh muncul lagi.
5. Catat hasil setiap skenario: lulus atau gagal, waktu, dan catatan.

## Catatan
1. **"60 FPS" tidak diukur langsung.** Yang diukur adalah lama kerja di UI thread (P5):
   - susun 130 stiker sekitar 40 ms, atau 2–3 frame;
   - satu langkah Rapikan pada gambar 4000×3000 sekitar 0,3 detik, jadi tampilan sempat diam sebentar;
   - konversi CDR tidak memblok UI.
2. **`Parallel.For`, bukan `Parallel.ForEachAsync`.** Spesifikasi menyebut `Parallel.ForEachAsync`, tetapi kerja di sini murni CPU (lihat PRD).
3. **Pembanding P4.** Hapus latar paralel dibandingkan dengan salinan algoritma lama di `SelfCheck.ErodeRef`.
