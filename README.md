# Printech

Alat internal untuk membuat layout stiker print & cut. Operator menambahkan file dari customer. Printech lalu membuat garis kiss-cut dan die-cut secara otomatis, menyusun stiker di kertas A3+, dan meng-export PDF untuk CorelDRAW dan Cutting Master 5 (Graphtec).

**Status:** prototipe. Cakupannya automasi layout saja, dengan estimasi 2 minggu (dicatat 2026-10-17).

## Dokumentasi
- [PRD](docs/PRD.md): tujuan, fitur, keputusan, batasan, checklist uji operator, dan cara menjalankan (§11).
- [Riset](docs/RESEARCH.md): riset pendukung keputusan.
- [Uji garis potong](docs/UJI-GARIS-POTONG.md): skenario uji S1–S11 dan hasilnya.

## Mulai cepat
Butuh Windows dan .NET 10 SDK.
- Jalankan aplikasi: `dotnet run --project StickerLayout`
- Cek otomatis: `StickerLayout\bin\Debug\net10.0-windows\StickerLayout.exe --selfcheck samples`
