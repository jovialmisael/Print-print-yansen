# Riset Fitur Lanjutan — StickerLayout / Printech

Riset awal: 2026-10-12. Pembaruan: 2026-10-13. Dokumen ini bahan belajar. Keputusan resminya ada di [PRD.md](PRD.md).

Cara membaca: setiap topik berisi konsep, cara software profesional mengerjakannya, kondisi prototipe, dan pilihan yang diurutkan dari yang paling murah. **Unverified** berarti sumbernya pihak ketiga atau belum diuji. Kolom "kondisi sekarang" di bawah menggambarkan prototipe **v1**.

## Pembaruan setelah jawaban operator (2026-10-13)
- **Mesin:** Graphtec FCX2000 (flatbed) dan CE7000 (roll). Keduanya didukung [Cutting Master 5](https://www.graphteccorp.com/cutting/cm5/) (CM5), plugin Graphtec untuk CorelDRAW (2021–2026) dan Illustrator.
- **Reg mark:** CM5 sendiri yang menambahkan reg mark ke artboard di CorelDRAW/Illustrator (10 pola), jadi aplikasi tidak perlu membuatnya. Aplikasi cukup menyisakan margin.
- **Garis potong:** CM5 bisa mengatur kondisi potong, urutan, dan potong/tidak **per warna garis atau per layer**. Karena itu v2 memakai kiss-cut = magenta 100% dan die-cut = cyan 100%.
- **Format:** 80% input berupa CDR & PDF, dan warna harus akurat. Karena itu v2 meng-export PDF yang menjaga artwork tetap vektor dengan warna asli.
- **Dilewati sesuai jawaban:** hide copy, magic eraser, background remover, simpan proyek. Riset di bawah tetap disimpan sebagai referensi.

## Ringkasan (kondisi v1)

| # | Fitur | Kondisi v1 | Langkah termurah | Biaya |
|---|---|---|---|---|
| 1 | Reg mark | 4 kotak 5 mm, generik | Pastikan dulu software pemotong yang dipakai | Bergantung info mesin |
| 2 | Garis potong bisa diatur | Offset bisa diatur, kehalusan tetap | Slider kehalusan, bentuk kotak/lingkaran | Kecil |
| 3 | Ukuran kanvas | Diisi manual dalam mm | Preset A/SRA/Letter, custom, tombol putar | Sangat kecil |
| 4 | Banyak kanvas | Satu kanvas; sisanya "tidak muat" | Sisa stiker pindah ke lembar 2, 3, dst. | Kecil–sedang |
| 5 | Sembunyikan salinan kanvas | Belum ada | Lembar identik tampil sekali dengan label ×N | Kecil (setelah #4) |
| 6 | Rendering | Bitmap resolusi penuh; kanvas dibangun ulang tiap klik | Ukur dulu, baru pakai preview diperkecil | Kecil, tunggu bukti |
| 7 | UI ramah operator | Panel kiri sederhana | Undo/redo, drag-drop, zoom roda + geser dengan Spasi | Kecil–sedang |
| 8 | Garis potong dari banyak format | PNG, SVG | JPG, lalu PDF/AI lewat PDFium | Kecil–sedang |
| 9 | Background remover | Latar putih atau transparan | Latar warna polos apa pun + toleransi | Kecil; versi AI besar |
| 10 | Magic eraser | Belum ada | Klik + toleransi ala Photoshop, garis potong dihitung ulang | Kecil |

## 1. Registration mark (reg mark)

**Konsep.** Setelah dicetak, bahan dipindahkan ke mesin potong. Di perjalanan, bahan bisa bergeser, miring, atau sedikit menyusut. Sensor optik atau kamera di cutter membaca mark yang ikut tercetak. Dari situ cutter menghitung posisi, rotasi, dan skala bahan yang sebenarnya, lalu menyesuaikan garis potong. Tanpa mark, garis potong meleset dari gambar.

**Jumlah titik.**
- **4 titik** adalah default Graphtec ARMS dan paling akurat. Manual Cutting Master 5 menyarankan metode 4 titik dengan panjang mark maksimum bila butuh akurasi sangat tinggi.
- **2–3 titik** lebih cepat dan area potongnya lebih luas, tetapi kompensasinya terbatas. Pergeseran bahan sekecil apa pun bisa membuat mark gagal terbaca.
- **Bahan panjang** memakai mark segmen atau mark antara. Fiery menambahkan mark segmen OPOS untuk job lebih dari 600 mm. Untuk bahan yang menyusut atau melengkung karena suhu, Graphtec memakai mode XY Segment dengan mark antara. Mark antara arah Y hanya tersedia bila jarak antar-mark minimal 600 mm.

**Bentuk mark berbeda per merek:**

| Merek/sistem | Bentuk dan ukuran | Catatan |
|---|---|---|
| Graphtec (ARMS, Cutting Master 5) | Mark Graphtec Type 1/Type 2; crop/trim mark gaya Roman/Japanese; mark berwarna atau putih di atas latar berwarna (FC9000/CE7000) | Panjang dan tebal bisa diatur. Contoh di manual: tebal 1,0 mm, panjang 20 mm untuk media reflektif. Manual FC8000 menyebut akurasi ±0,3 mm (Unverified, dikutip lewat ManualsLib) |
| Summa (OPOS) | Kotak hitam 3 mm (rentang 1,2–10 mm), tanpa outline | Butuh margin putih 3–4 kali ukuran mark |
| Silhouette | Type 1 untuk CAMEO/Portrait (kotak di kiri atas + mark sudut); Type 2 untuk mesin lama | Length, Thickness, dan Inset bisa diatur. Mark tipis lebih sulit terbaca. Area berarsir tidak boleh dicetak |
| Cricut | Sudut atau bingkai hitam dari Design Space | Cahaya terang dan kertas glossy mengganggu sensor |
| Zünd (flatbed berkamera) | Lingkaran hitam sekitar 6 mm (1/4"), minimal 3 titik | Kamera OCC: disarankan 6–12 mm |
| Roland (VersaWorks) | Garis potong berupa spot color bernama persis `CutContour`, stroke hairline | Jangan konversi spot color ke CMYK karena namanya hilang |

**Aturan umum** (dari Graphtec CM5, Silhouette, dan Cricut):
- Cetak dengan skala 100%, bukan "fit to page", dan posisi cetak harus sama dengan desain.
- Sisakan margin bersih di sekitar mark. Desain tidak boleh masuk area mark.
- Letakkan garis potong di layer tersendiri. Sembunyikan layer potong saat mencetak, dan sembunyikan layer cetak saat mengirim data potong.
- Gunakan kontras tinggi (hitam di atas putih). Bahan glossy atau reflektif bisa gagal terbaca; Graphtec menyediakan mark berwarna dan mask latar untuk kasus ini. Ini relevan untuk bahan vinyl glossy, silver, dan gold.

**Siapa yang membuat mark?** Biasanya software pemotong, karena cutter harus tahu bentuk dan posisi persis mark tersebut. Untuk Graphtec, itu Cutting Master 5 (lihat pembaruan di atas).

## 2. Garis potong yang bisa diatur

**Menghaluskan garis kasar**, dari yang paling murah:
1. **Parameter Potrace.**
   - `alphamax` adalah ambang sudut: 0 menghasilkan poligon tajam, 1,3333 menghasilkan kurva tanpa sudut. Default 1,0.
   - `opttolerance` adalah toleransi penyederhanaan kurva, default 0,2.
   - `turdsize` membuang bintik kecil, default 2.
2. **Morfologi pada mask sebelum trace.** Closing (dilasi lalu erosi) menutup takik dan celah sempit. Opening membuang duri kecil. **v2 memakai closing ini sebagai pengaturan "Halus".**
3. **Clipper2** (Boost Software License 1.0, C#). `SimplifyPaths` membuang titik berlebih; `InflatePaths` membuat offset vektor dengan sudut Round atau Miter.

**Kiss-cut vs die-cut.**
- **Kiss-cut:** pisau hanya memotong lapisan stiker, backing tetap utuh.
- **Die-cut/through-cut:** pisau menembus backing sehingga setiap stiker lepas.
- v2 menyediakan keduanya, masing-masing dengan offset dan penghalus sendiri.

**Bleed.** Praktik umum: bleed 2–3 mm, dan detail penting minimal 2 mm di dalam garis potong. Prototipe tidak memperluas warna artwork sampai garis potong, sehingga celah di antaranya tampil putih (gaya "border putih").

## 3. Ukuran kanvas

| Ukuran | mm |
|---|---|
| A0 / A1 / A2 | 841 × 1189 / 594 × 841 / 420 × 594 |
| A3 / A4 / A5 / A6 | 297 × 420 / 210 × 297 / 148 × 210 / 105 × 148 |
| SRA3 / SRA4 | 320 × 450 / 225 × 320 |
| Letter / Legal / Tabloid | 215,9 × 279,4 / 215,9 × 355,6 / 279,4 × 431,8 |

Seri A memakai rasio 1:√2. Membelah satu lembar menjadi dua menghasilkan ukuran berikutnya: A3 dibelah menjadi dua lembar A4.

Area yang benar-benar bisa dipakai lebih kecil dari ukuran kertas, karena ada margin yang tidak bisa dicetak printer dan area reg mark.

## 4. Banyak kanvas sekaligus
- **CorelDRAW Multipage View** menampilkan semua halaman sekaligus. Default-nya grid 4 kolom; jumlah kolom dan jaraknya bisa diatur.
- **Illustrator** memakai banyak artboard dalam satu dokumen. Ctrl+Alt+0 menampilkan semua artboard.
- **Di WPF**, daftar lembar cukup ditampilkan dengan `ItemsControl` + `WrapPanel`. Jika lembarnya sangat banyak, pakai virtualisasi (`VirtualizingStackPanel`, hanya satu arah).

## 5. "Hide a copy of the same canvas" (dilewati)
Software nesting mengelompokkan lembar yang susunannya identik:
- **PlotNest** menampilkan layout yang berulang lima kali sebagai satu entri ×5.
- **Autodesk Inventor Nesting** punya "stack size": jumlah lembar duplikat yang ditumpuk lalu dipotong sekaligus.
- **SheetNest** menghasilkan satu halaman per layout plus jumlah lembar yang harus dipotong.
- **Graphtec** punya Matrix Copy dan ARMS Copy untuk memotong salinan tanpa PC.

## 6. Rendering ("render saat ditekan")

**Konsep: proxy.** Saat mengedit, yang ditampilkan versi resolusi rendah; versi penuh hanya dibuat saat dibutuhkan. Contohnya InDesign Display Performance:
- **Fast**: gambar tampil sebagai kotak abu-abu.
- **Typical**: proxy resolusi rendah (default).
- **High Quality**: resolusi penuh.

Adobe menegaskan pengaturan tampilan ini tidak memengaruhi resolusi output.

**Soal akurasi.** Data tampilan boleh beresolusi rendah. Data produksi (garis potong vektor dan artwork asli) selalu diambil dari sumber asli. v2 bekerja dengan cara ini: preview PDF dirender 10 px/mm, sedangkan export memakai vektor asli.

**Pilihan bila nanti terasa lambat:**
1. Ukur dulu.
2. `DecodePixelWidth`/thumbnail.
3. Jangan bangun ulang kanvas saat yang berubah hanya pilihan stiker.
4. `BitmapCache` (uji dan profil dulu).

## 7. UI yang ramah operator
- **Konvensi Adobe:** V (Selection), H/Spasi (Hand), Z (Zoom), Ctrl+1 (100%), Ctrl+Alt+0 (semua artboard), Ctrl+Z, Delete.
- **Operator bukan desainer:** alurnya tetap (Import, Garis potong, Susun, Export), dengan opsi lanjutan disembunyikan.
- **Paling berdampak:**
  - Undo/redo (belum ada).
  - Drag-and-drop file.
  - Zoom dengan roda mouse.
  - Penggaris mm dan snap.
  - Tema Fluent bawaan WPF .NET 9+ (`ThemeMode`; masih experimental WPF0001 di .NET 10).

## 8. Garis potong otomatis dari banyak format

| Format | Cara | Catatan |
|---|---|---|
| PDF | Render dengan PDFium untuk garis potong; artwork ditempel vektor saat export | v2: Docnet.Core + PDFsharp |
| AI | File yang disimpan dengan "Create PDF Compatible File" bisa dibuka sebagai PDF | Jika opsi itu dimatikan, aplikasi lain tidak bisa membacanya |
| CDR | (a) Export ke PDF dari CorelDRAW. (b) Otomasi CorelDRAW lewat COM (`OpenDocument`, `PublishToPDF`). (c) libcdr (MPL-2.0) | v2 memakai (b), dengan (a) sebagai cadangan. Versi CDR yang didukung libcdr simpang siur |
| PNG/JPG | Siluet dari alpha atau latar putih | JPG tanpa alpha → jalur latar putih |
| SVG | Dirender lalu diperlakukan sebagai gambar | Jarang dipakai |

## 9. Smart background remover (dilewati)

| Tingkat | Cara | Catatan |
|---|---|---|
| 1 | Flood-fill dari tepi, warna latar diambil dari sudut, dengan toleransi | Tanpa dependensi |
| 2 | Klik latar (magic wand) | Sama dengan magic eraser |
| 3 | GrabCut (OpenCvSharp4, Apache-2.0) | Butuh library native OpenCV |
| 4 | Model AI lewat ONNX Runtime (MIT) | U²-Net (Apache-2.0), BiRefNet (kode MIT). **BRIA RMBG: non-komersial / berbayar, hindari untuk toko** |
| 5 | Windows AI `ImageObjectExtractor` | Hanya Copilot+ PC, aplikasi harus dikemas MSIX |

## 10. Magic eraser (dilewati)
1. **Photoshop Magic Eraser:** klik satu piksel, dan semua piksel yang warnanya mirip menjadi transparan. Opsinya Tolerance 0–255, Contiguous, dan Opacity.
2. **Hapus objek generatif** (Google Magic Eraser, Windows `ImageObjectRemover`): area yang dihapus diisi ulang dengan latar. Model terbuka: LaMa (Apache-2.0).

Untuk stiker, menghapus elemen biasanya cukup dengan membuatnya transparan. Inpainting hanya perlu bila elemen itu menumpuk di atas artwork lain.

## 11. Merapikan gambar untuk stiker (riset umum, 2026-10-14)
Operator AP 55 merapikan gambar di Photoshop dan langkah ini lama. Langkah yang umum dilakukan percetakan stiker:
1. **Cek resolusi:** target 300 ppi pada ukuran cetak; lebar piksel ÷ 300 = lebar stiker maksimal dalam inci. Memperbesar gambar kecil hanya perkiraan, walau dengan AI.
2. **Hapus latar:** latar polos cukup dengan Magic Wand/Quick Selection plus toleransi; latar rumit memakai Remove Background (AI), Pen Tool, atau Select and Mask.
3. **Rapikan tepi:** perluas atau kikis seleksi 1–2 px, atau Layer > Matting > Defringe 1–2 px, untuk membuang halo. Cek di latar terang dan gelap.
4. **Buang sisa piksel dan elemen tidak perlu** secara manual pada zoom besar.
5. **Simpan PNG transparan, trim area kosong**, lalu cek di ukuran cetak sebenarnya.

Pemetaan ke Printech: langkah 5 (trim, offset/border, ukuran mm) sudah otomatis. Langkah 1 = angka dpi (F12). Langkah 2–4 = jendela Rapikan gambar (F15): hapus latar otomatis dari warna tepi, penghapus klik, kikis tepi 0–3 px. Hapus latar foto dengan AI dan upscale ditunda (lihat bagian 9 untuk pilihan model dan lisensinya).

## 12. Reduksi node garis potong: RDP vs optimasi kurva Potrace (2026-10-16)
- **RDP (Ramer-Douglas-Peucker)** menyederhanakan polyline (garis lurus): titik yang jaraknya ke garis penghubung di bawah epsilon dibuang. Hasilnya tetap garis lurus.
- **Potrace** mengubah bitmap menjadi kurva Bezier. Parameter `opticurve`/`opttolerance` menggabungkan segmen kurva berurutan selama kesalahannya di bawah toleransi, jadi ini sudah "RDP untuk kurva". `alphamax` mengatur kapan sudut dianggap tajam; nilai 1,3334 berarti tidak ada sudut sama sekali.
- **Ukuran pada pipeline app** (6 px/mm, offset 2 mm). Teks "PRINTECH":
  - Bezier Potrace: 72 node.
  - Bezier dipecah jadi garis: 351 titik.
  - RDP 0,1 mm: 117 titik garis lurus.
  - Jadi RDP menambah node dan menghasilkan gerakan pisau yang lebih patah-patah.
- **Batas optimasi Potrace:** segmen hanya bisa digabung bila kelengkungannya searah (tanpa titik belok). Kontur bergelombang dari foto JPG tetap butuh banyak node, kecuali bentuknya dihaluskan dulu. Karena itu Printech memakai blur Gaussian (sigma = Halus) sebelum trace, ditambah penjaga jarak ke artwork.
- **Hasil penghalusan:**
  - Logo kasar: kiss -77%, die -72%.
  - Lingkaran: -67 sampai -75%.
  - Teks: -33 sampai -59%.
  - Bintang: -42 sampai -46%.
  - Jarak minimum ke artwork tetap di atas offset - 0,43 mm.
- Angka akhir di app (blur kemudian diganti 3 lintasan box blur supaya stiker besar tetap cepat): lihat [UJI-GARIS-POTONG.md](UJI-GARIS-POTONG.md).

## Sumber
- **Reg mark:**
  - Graphtec: [ARMS](https://www.graphteccorp.com/cutting/arms/), [Cutting Master 5](https://www.graphteccorp.com/cutting/cm5/), [manual CM5](https://mygraphtec.jp/site_download/cm5/data/CM5_Manual_ENG.pdf), [manual FC8000](https://www.manualslib.com/manual/950580/Graphtec-Fc8000-Series.html?page=103)
  - Summa: [Fiery – Summa OPOS](https://communities.efi.com/s/article/Setting-up-Summa-OPOS-marks-in-Digital-Factory?language=en_US), [manual Summa DC5](https://www.manualslib.com/manual/1410878/Summa-Dc5.html?page=52), [panduan OPOS](https://www.summa.com/media/lccbhxat/opos20_en.pdf)
  - Silhouette: [Silhouette School](https://www.silhouetteschoolblog.com/2018/10/tips-for-mastering-print-and-cut-with.html), [Silhouette Secrets](https://silhouette-secrets.com/2020/01/21/lets-explore-registration-marks-v4-3/)
  - Cricut: [Cricut Help](https://help.cricut.com/hc/en-us/articles/360009429814-How-large-can-I-Print-Then-Cut)
  - Zünd: [Zünd OCC](https://www.zund.com/media/502/download/pri_Over%20Cutter%20Camera%20%E2%80%93%20OCC_7_EN-us.pdf?v=4), [GSD FabLab](https://fablab.gsd.harvard.edu/places/zund-cutter/zund-tutorial/registration-dot/)
  - Roland: [Roland DGA](https://www.rolanddga.com/blog/2016/06/17/15/18/the-perfect-setup-file-preparation-for-printcut-production)
- **Mesin:** [Graphtec FCX2000](https://www.graphteccorp.com/cutting/fcx2000/), [spesifikasi CE7000](http://www.graphteccorp.com/product/ce7000/spec.html)
- **Garis potong:**
  - Potrace: [potracelib](https://potrace.sourceforge.net/potracelib.pdf)
  - Clipper2: [lisensi](https://www.angusj.com/clipper2/Docs/License.htm), [InflatePaths](https://www.angusj.com/clipper2/Docs/Units/Clipper/Functions/InflatePaths.htm)
  - Kiss-cut vs. die-cut: [Sticker Mule](https://www.stickermule.com/write/stickermule/die-cut-stickers-vs-kiss-cut-stickers)
  - Bleed: [Paperlust](https://printshop.paperlust.co/blog/die-cut-sticker-printing)
- **Kanvas dan tampilan:** [Paper size](https://en.wikipedia.org/wiki/Paper_size), [CorelDRAW Multipage View](https://learn.corel.com/tutorials/coreldraw-multipage-view/), [shortcut Illustrator](https://helpx.adobe.com/illustrator/using/default-keyboard-shortcuts.html), [PlotNest](https://retechlasers.com/software/plotnest-nesting-software/), [virtualisasi WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls)
- **Rendering dan UI:** [InDesign display performance](https://helpx.adobe.com/in/indesign/kb/fix-image-display-performance.html), [WPF thumbnail](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-load-an-image-as-a-thumbnail), [BitmapCache](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-improve-rendering-performance-by-caching-an-element), [WPF Fluent](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90)
- **Format file:**
  - PDF: [PDFtoImage](https://github.com/sungaila/PDFtoImage), [Docnet](https://github.com/GowenGit/docnet), [PDFsharp format gambar](https://docs.pdfsharp.net/PDFsharp/Topics/Bitmap-Images/Formats.html)
  - AI: [Adobe – file AI](https://www.adobe.com/creativecloud/file-types/image/vector/ai-file.html)
  - CDR: [libcdr](https://github.com/LibreOffice/libcdr), [CorelDRAW PublishToPDF](https://community.coreldraw.com/sdk/api/draw/19/m/document.publishtopdf), [CorelDRAW OpenDocument](https://community.coreldraw.com/sdk/api/draw/18/m/application.opendocument)
- **Merapikan gambar untuk stiker:** [Sticker it – 6 cara hapus latar](https://www.stickerit.co/blog/remove-background-photoshop), [4over4 – foto jadi stiker](https://www.4over4.com/guide/how-to-turn-a-photo-into-a-sticker), [Kadsticky](https://kadsticky.com/blogs/news/how-to-remove-background-from-an-image-for-sticker-printing), [OZ Sticker Printing](https://www.ozstickerprinting.com/blog/blurry-ai-sticker-designs-fix-it-fast), [DTF Ghost – tepi](https://dtfghost.com/blogs/news/how-to-fix-edges-in-photoshop-for-dtf-printing), [Adobe – decrease fringe](https://helpx.adobe.com/photoshop/desktop/make-selections/refine-modify-selections/decrease-fringe-on-selection.html), [Photoshop Training Channel – Defringe](https://photoshoptrainingchannel.com/tips/removing-edge-halos-defringe/), [Kumparan – hapus background rapi](https://kumparan.com/how-to-tekno/cara-menghapus-background-di-photoshop-dengan-rapi-1xOBC1u6vjK)
- **Background remover dan eraser:** [rembg](https://github.com/danielgatis/rembg), [RMBG-1.4](https://huggingface.co/briaai/RMBG-1.4), [BiRefNet](https://github.com/ZhengPeng7/BiRefNet), [ONNX Runtime](https://github.com/microsoft/onnxruntime), [OpenCvSharp](https://github.com/shimat/opencvsharp), [Windows Image Object Erase](https://learn.microsoft.com/en-us/windows/ai/apis/image-object-erase), [Photoshop Magic Eraser](https://helpx.adobe.com/photoshop/using/tool-techniques/magic-eraser-tool.html), [LaMa ONNX](https://huggingface.co/Carve/LaMa-ONNX)
