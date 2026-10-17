# Skenario uji: garis potong dengan node sedikit dan kurva mulus

Fitur yang diuji: tuning Potrace (tanpa sudut tajam), penghalusan bentuk lewat slider **Halus**, penjaga jarak ke artwork, dan die yang selalu membungkus kiss. Lihat [PRD.md](PRD.md) F16.

- **Otomatis:** dijalankan oleh `StickerLayout.exe --selfcheck <folder>`. Hasilnya tertulis di `selfcheck.txt` sebagai baris `S1 LULUS ...` atau `S1 GAGAL ...`. Bila satu skenario gagal, selfcheck keluar dengan kode 1.
- **Manual:** dikerjakan operator dengan CorelDRAW, Cutting Master 5, dan mesin Graphtec.
- Pengaturan default: kiss offset 2 mm, halus 1 mm; die offset 4 mm, halus 2 mm.

| ID | Skenario | Kriteria lulus | Cara | Hasil 2026-10-17 |
|---|---|---|---|---|
| S1 | Logo JPG bertepi kasar (`rough.jpg`, 40 mm) | Node turun ≥60% dibanding Halus 0; tidak ada sudut tajam | Otomatis | Lulus: kiss 66 → 16 (−76%), die 45 → 13 (−71%) |
| S2 | Lingkaran 40 mm | Maksimal 4 node; garis tetap bulat (selisih lebar dan tinggi < 0,3 mm); diameter 44 mm ± 0,5 | Otomatis | Lulus: 3 node, 44,03 × 44,02 mm |
| S3 | Teks tebal "PRINTECH" 60 mm | Node lebih sedikit dari Halus 0; tidak ada sudut; huruf tidak terpotong (luas artwork di luar garis < 0,5 mm²) | Otomatis | Lulus: 43 → 20 node, 0,00 mm² |
| S4 | Bintang dengan offset kecil 0,5 mm dan offset 0 | Garis tidak masuk artwork: luas di luar garis < 0,5 mm² (offset 0,5) dan < 3 mm² (offset 0, toleransi piksel) | Otomatis | Lulus: 0,00 dan 0,49 mm² |
| S5 | Halus 0 / 0,5 / 1 / 2 / 4 mm pada logo kasar | Semua nilai jalan tanpa error; node selalu lebih sedikit dari Halus 0 | Otomatis | Lulus: 66 / 32 / 16 / 17 / 47 node (lihat catatan 1) |
| S6 | Kiss selalu di dalam die, termasuk pengaturan tak biasa (kiss halus 3, die halus 0) | Luas kiss di luar die < 1 mm² | Otomatis | Lulus: 0,00 mm² pada 9 desain |
| S7 | Dua bagian artwork berdekatan (celah 3,0–3,9 mm) | Penghalusan tidak memecah satu stiker menjadi dua (jumlah kontur tidak bertambah) | Otomatis | Lulus pada keempat celah |
| S8 | Export PDF lalu dibuka di CorelDRAW | Otomatis: garis potong hanya berisi kurva (0 garis lurus) dan warnanya DeviceCMYK murni (hanya `0 1 0 0 K` dan `1 0 0 0 K`, tanpa operator RGB atau ruang warna lain). Manual: klik tiap garis dengan Shape tool (F10), lihat jumlah node. Harapan untuk lebar 40 mm, nilai default: `circle.png` kiss 3, die 4; `rough.jpg` kiss 16, die 13. Selisih 1 node (titik awal/akhir) masih wajar | Otomatis + manual | Bagian otomatis lulus: 275 kurva, 0 garis lurus, 0 operator RGB. Bagian CorelDRAW belum diuji |
| S9 | Stiker besar 300 mm (gambar 3000 px), kiss + die | Dimuat < 5 detik di build Debug | Otomatis | Lulus: 2,0 detik (Debug), ±1 detik (Release) |
| S10 | Potong fisik di Graphtec | Lihat langkah di bawah | Manual | Belum diuji |
| S11 | Cincin "C" (celah 3 mm) dan 12 titik melingkar + lingkaran tengah. Offset menutup celah sehingga terbentuk lubang | Kiss dan die masing-masing tepat 1 kontur; lubang tidak ikut dipotong | Otomatis | Lulus: 1 / 1 kontur. Sebelum perbaikan: cincin 2 / 2, titik 3 / 1 |

## S10 — langkah potong fisik (operator)
1. Siapkan `rough.jpg`, `circle.png`, dan 2–3 teks/logo customer yang biasa dipotong. File contoh ada di folder `samples/`, atau buat ulang dengan `StickerLayout.exe --selfcheck <folder>`.
2. Di Printech: lebar 40–60 mm, 4 salinan per desain, kertas A3+. Centang kiss dan die, nilai offset dan halus default.
3. Export PDF, buka di CorelDRAW, pasang reg mark dengan cara yang biasa dipakai operator, lalu cetak di Develop.
4. Potong di Graphtec lewat Cutting Master 5. Sebagai pembanding, potong juga desain yang sama dengan garis potong yang biasa dibuat di CorelDRAW.
5. **Lulus bila:**
   - pisau bergerak mulus, tidak tersendat atau berbelok mendadak, terutama di teks dan logo bertepi kasar;
   - tepi potongan halus, tidak bergerigi;
   - garis potong tidak mengenai artwork dan lebar border tampak rata;
   - die tidak memotong stiker kiss;
   - waktu potong tidak lebih lama dari pembanding.
6. Catat hasil per desain. Bila pisau tersendat, catat bentuknya dan nilai Halus yang dipakai.

## Catatan
1. **Halus lebih dari 2 mm tidak menambah kehalusan.** Pada logo kasar 40 mm, Halus 4 mm justru menaikkan node lagi menjadi 47, padahal Halus 1–2 mm hanya 16–17. Dugaan penyebab (belum diukur terpisah): penghalusan besar menarik garis ke dalam, lalu penjaga jarak ke artwork mengembalikan lekukan aslinya. Nilai yang disarankan: Halus 1–2 mm.
2. Skenario S1–S9 membuktikan bentuk garisnya. Apakah gerakan pisau benar-benar mulus hanya bisa dibuktikan lewat S10.
