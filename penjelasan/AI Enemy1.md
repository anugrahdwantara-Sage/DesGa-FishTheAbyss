# AI Enemy1

Dokumen ini menjelaskan perilaku `Enemy1AI` dan komponen yang mendukungnya.
Implementasi utama berada di `Assets/Scripts/Enemy1AI.cs`.

## Komponen yang dibutuhkan

GameObject Enemy1 harus memiliki:

- `Rigidbody2D` untuk pergerakan horizontal.
- `Animator` dengan state animasi bernama `idle`, `walk`, `attack`, dan `breach`.
- `PlayerAttack` untuk memeriksa serangan dan memberikan damage.
- `SpriteRenderer` disarankan agar sprite dapat menghadap target. Komponen ini
  tidak diwajibkan oleh skrip.

`PlayerAttack` juga membutuhkan `CharacterHealth` pada GameObject yang sama,
serta Collider2D serangan yang ditetapkan pada field `attackArea` atau tersedia
sebagai child bernama `attackarea`.

## Target

Field `target` dapat diisi melalui Inspector. Jika kosong, saat `Start` AI akan
mencari GameObject dengan tag `Player`. Jika tetap tidak ditemukan, peringatan
akan ditampilkan dan Enemy1 berhenti bergerak sampai target tersedia.

Setelah mendapatkan target, Enemy1 mendaftarkan target tersebut ke
`PlayerAttack`. Pendaftaran ini membatasi objek yang dapat terkena serangan
Enemy1.

## Perilaku AI

AI memeriksa jarak horizontal antara Enemy1 dan target setiap frame; jarak
vertikal tidak dipakai dalam keputusan deteksi maupun serangan.

1. **Di luar jangkauan deteksi** — Enemy1 berhenti secara horizontal dan
   memainkan `idle`.
2. **Target terdeteksi, tetapi belum dalam jangkauan serang** — Enemy1 bergerak
   menuju target dengan kecepatan `moveSpeed` dan memainkan `walk`.
3. **Target dalam jangkauan serang** — Enemy1 berhenti secara horizontal.
   Jika cooldown sudah selesai, AI memulai serangan; jika belum, Enemy1
   memainkan `idle`.
4. **Selama serangan** — Selama `attackDuration`, AI tidak bergerak secara
   horizontal. Kecepatan vertikal Rigidbody2D tetap dipertahankan. Setelah
   durasi selesai, animasi kembali ke `idle`.

Saat target berada di kiri atau kanan, `SpriteRenderer.flipX` diatur agar
Enemy1 menghadap target. Bila `SpriteRenderer` tidak tersedia, AI tetap
berjalan dan menyerang, tetapi sprite tidak dibalik oleh skrip ini.

## Nilai bawaan

Nilai berikut ditetapkan di Inspector melalui field serialized pada
`Enemy1AI`:

| Pengaturan | Nilai bawaan | Keterangan |
|---|---:|---|
| `moveSpeed` | 2 | Kecepatan gerak horizontal |
| `detectionRange` | 8 | Jarak horizontal maksimum untuk mendeteksi target |
| `attackRange` | 1.4 | Jarak horizontal untuk memulai serangan |
| `attackCooldown` | 1.5 detik | Jeda minimum antarserangan |
| `attackDuration` | 0.75 detik | Lama AI menahan gerakan selama serangan |

## Damage dan animasi serangan

Saat cooldown selesai, AI mengatur timer serangan dan memainkan state `attack`.
Animasi serangan Enemy1 memiliki Animation Event bernama `AttackHitFrame`.
Event tersebut memanggil `PlayerAttack.PerformAttack`, yang memeriksa Collider2D
serangan pada frame itu. Target terdaftar yang terkena akan menerima damage
sesuai nilai `damage` pada komponen `PlayerAttack`; satu target hanya menerima
damage satu kali dalam satu pemeriksaan serangan.

Pastikan event `AttackHitFrame` tetap ada pada animasi serangan dan ejaan
namanya sama persis dengan method di skrip.

## Animasi breach dan kemunculan

`BeginBreach()` dipanggil saat Enemy1 mulai dimunculkan oleh alur memancing di
`CharMovement`. Method ini menandai Enemy1 sedang breach, menghentikan gerakan,
membatalkan timer serangan, dan memainkan animasi `breach`. Selama keadaan ini,
`Update` tidak menjalankan logika deteksi atau serangan.

Setelah animasi perpindahan/kemunculan selesai, `CharMovement` mengaktifkan
kembali fisika dan collider Enemy1 lalu memanggil `FinishBreach()`. Method itu
mengakhiri keadaan breach dan mengembalikan animasi ke `idle`, sehingga AI
dapat berjalan normal.

## Ringkasan alur

`Cari target → cek jarak horizontal → idle atau kejar → berhenti dan serang
saat dekat → berikan damage pada Animation Event → ulangi setelah cooldown.`

Enemy1 yang baru dimunculkan menjalani alur `breach` terlebih dahulu sebelum
perilaku AI normal dimulai.
