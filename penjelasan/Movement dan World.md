# Movement dan World

Dokumen ini merangkum gerak karakter dan dasar perpindahan antar-ruang di
proyek Unity. Acuannya adalah mekanik movement dan room pada folder `contoh`.

## Kontrol dan movement

| Aksi | Kontrol |
|---|---|
| Bergerak | `A`/`D` atau panah kiri/kanan |
| Lompat | `Space` |
| Dash | `Left Shift`, `Right Shift`, atau `L` |
| Serang | `Enter` atau klik kiri |
| Memancing | `F` |

`CharMovement` mendukung coyote time dan jump buffer agar lompatan tetap
responsif di tepi platform, wall slide dan wall jump saat menyentuh dinding,
serta double jump dan dash yang dapat diaktifkan melalui Inspector. Dash dan
double jump dimatikan secara default; aktifkan field `dashUnlocked` atau
`doubleJumpUnlocked` saat kemampuan tersebut sudah didapat di dalam permainan.
Wall cling aktif secara default dan dapat dimatikan melalui
`wallClingEnabled`.

## Efek senar pancing

Saat tombol `F` ditekan, `CharMovement` membuat senar dan pelampung dengan
`LineRenderer` tanpa sprite atau asset animasi tambahan. Pelampung dilempar
dengan lintasan melengkung, mengambang selama fase menunggu, lalu ditarik
kembali sebelum musuh dimunculkan.

Memancing hanya tersedia saat player berada di trigger anak pond bernama
`canFishleft` atau `canfishright` (nama diperiksa tanpa membedakan kapital).
Di area kiri, player harus menghadap kanan; di area kanan, player harus
menghadap kiri. Saat berenang di luar area memancing, serangan dan memancing
dinonaktifkan, sementara gerakan renang tetap tersedia. Pastikan kedua area
memiliki Collider2D dengan `Is Trigger` aktif dan nama GameObject sesuai.
`CharMovement` mengecek trigger yang sedang tumpang tindih langsung dari
collider player menggunakan `OverlapCollider`, jadi izin memancing tidak
bergantung pada event masuk/keluar trigger.

Setelah durasi memancing selesai, musuh muncul di bawah air dan meluncur ke
titik pendaratan di dekat player. Titik awal kemunculan berada lebih jauh ke
arah kolam, sedangkan gerak horizontal dipercepat di awal lintasan agar musuh
tampak melompat keluar dari air menuju player. Jarak awal mendatar diatur
melalui `launchHorizontalOffset` pada komponen `Enemy1AI` di setiap enemy,
sehingga tiap jenis enemy dapat memiliki nilai berbeda. Tinggi lompatan juga
bisa diatur per enemy melalui `launchJumpHeight` di komponen `Enemy1AI`.
Posisi mendarat dapat digeser ke arah player melalui `landingOffsetTowardPlayer`
pada `Enemy1AI`; nilai lebih besar mendarat lebih jauh melewati posisi player
ke sisi darat. Setelah mendarat, enemy melompat saat target berada lebih tinggi
atau saat ada dinding menghalangi, agar bisa keluar dari air dan mengejar
player. Atur `chaseJumpForce`, `chaseJumpCooldown`, dan
`chaseJumpTargetHeight` pada enemy untuk mengubah kekuatan, jeda, dan pemicu
lompatan pengejaran. Jika enemy masih berada di air dan sedang turun, ia juga
dapat melompat ke arah player yang lebih tinggi tanpa harus menyentuh dasar.
Kedalaman muncul (`enemySpawnDepth`) dan durasi lompatan awal
(`enemyJumpDuration`) tetap diatur pada komponen `CharMovement` milik player.

Panjang lemparan, jarak jatuh, tinggi lintasan, durasi lempar/tarik, dan ukuran
visual dapat disesuaikan melalui Inspector. Shader sprite bawaan URP digunakan
untuk merender garis.

## Menyiapkan room dan exit

1. Kelompokkan platform, dekorasi, dan objek gameplay setiap room di bawah
   GameObject root room masing-masing.
2. Buat GameObject trigger pada setiap exit dan tambahkan `RoomTransition`.
   Collider2D pada trigger akan diatur sebagai trigger oleh komponen tersebut.
3. Isi `destination` dengan Transform titik spawn di room tujuan.
4. Isi `enableOnEnter` dengan root room tujuan dan `disableOnEnter` dengan root
   room asal. Jangan masukkan GameObject player ke daftar room.
5. Buat portal balik pada room tujuan dengan konfigurasi yang berlawanan.

Saat player melewati portal, komponen memindahkan player ke titik spawn,
menghentikan momentum gerak, mengosongkan timer jump/dash, lalu mengganti room
yang aktif. Root room perlu disiapkan di scene dan ditetapkan lewat Inspector;
komponen ini tidak membuat geometri room secara otomatis.
