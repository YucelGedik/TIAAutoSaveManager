# V0.3 — Logo, yardım rehberi ve arayüz düzenlemeleri

19 Kasım 2026. Kullanıcının sağladığı logo ve dört kurulum ekran görüntüsü uygulama kaynaklarına eklendi. Asıl PNG'ler değiştirilmedi. Windows için 16/24/32/48/64/128/256 boyutlu ICO türetildi; tekrar üretim betiği `scripts/build-brand-icon.ps1`.

## İçerik

- Logo: ana ekran, pencere/exe ikonu, sistem tepsisi, temalı onay pencereleri ve bildirimler.
- İlerleme çubuğu: 4 px yerine 12 px; genişlik 132 px, yuvarlatılmış koyu track ve çalışırken turkuaz nabız animasyonu. Yüzde/tamamlanma tahmini yapılmaz.
- Üst düzey Yönetim / Yardım sekmeleri. Yardım, F1 veya tray menüsünden de açılır; internet olmadan çalışır.
- Dört gerçek ekran görüntüsüyle Bilgisayar Yönetimi → Openness grubu → Ekle → kullanıcı adını doğrulama adımları. Ardından Windows oturumunu yenileme, bağlantı/izin istemi, Save/Auto Save, Backup, Pause/Resume ve sorun giderme anlatılır.
- Görseller büyütülebilir; ayrı temalı pencerede ekrana sığdır, %100 ve %25–%300 zoom bulunur.
- Önceki yerleşim/onay teması düzeltmeleri ve v0.2.1 kısa Archive yolu düzeltmesi bu sürüme dahildir.

## Kaynak ekranların sırası

Kaynak klasör: `<kullanıcı>\Desktop\oto save kullanım`.

1. `Annotation 2026-11-19 152233.png` → `01-computer-management.png`
2. `Annotation 2026-11-19 152454.png` → `02-openness-group.png`
3. `Annotation 2026-11-19 152549.png` → `03-add-user.png`
4. `Adsız.png` → `04-check-user-name.png`

Ekranlardaki ‘utente’ ve bilgisayar adı örnek olarak açıklanır; kullanıcıya kendi hesabını eklemesi söylenir. Program grup üyeliğini veya Windows güvenlik ayarlarını değiştirmez.

## Doğrulama

- Release build: hata/uyarı yok.
- 53 işlev testi + 13 WPF/arayüz testi geçti.
- Ayrı gerçek V21 salt okunur Discover testi: 1/1 geçti.
- Tüm PNG/ICO kaynakları ve dört yardım görseli internetsiz decode testinden geçti; kaynak logo hash'iyle kopya eşleşiyor.
- Yönetim, Yardım ve onay penceresi uygulamanın kendi WPF renderer'ıyla görsel olarak kontrol edildi. Bu, gerçek masaüstü etkileşim testi değildir.
- Gerçek TIA'da yeni Save/Archive veya Windows ayar değişikliği yapılmadı. Kullanıcının bildirdiği Auto Save başarısı korunur; v0.2.1 Archive düzeltmesinin gerçek Backup Now / Retrieve kabul testi hâlâ gereklidir.

