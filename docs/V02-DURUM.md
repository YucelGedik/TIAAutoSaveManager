# V0.2 — Zamanlayıcı ve sürümlü yedekleme

19 Kasım 2026. Başlangıç doğrulaması: kullanıcı, gerçek V21 uygulamasında Connected ve `Project.Save() başarıyla tamamlandı` logunu ekran görüntüsüyle bildirdi. Bu kullanıcı kabulü, yeni Archive akışının çalıştığı iddiası değildir.

## Eklenen davranış

- Her bağlı proje için Auto Save ve otomatik Backup ayrı açılır; ilk etkinleştirmede proje yolu, periyot ve Undo/Redo etkisi onaylanır. Uygulama yeniden açıldığında otomatik seçimler kapalıdır.
- 1/5/10/15/30/60 dakika hazır seçenekleri ve özel aralık; varsayılan Save 10, Backup 60 dakika.
- Monotonic saatle geri sayım; tek aktif işlem, kaçırılmış tick kuyruğu yok. Manuel Save/Backup aynı koordinatörü kullanır.
- Pause/Resume, tray komutları ve bildirimler. Resume tam aralıkla başlar; aktif native çağrıyı zorla kesmez.
- Backup Now: Save → Archive(Compressed) → staging dosya kontrolü → ZIP okuma → SHA-256/manifest → son konuma taşıma → rotation.
- Kaynak dosyaların kopyası alınmaz; SaveAs/Close/Compile/Download/portal sonlandırma çağrısı yok.
- 5/10/20/sınırsız rotation; yalnız aynı proje hash'i ve geçerli manifestle yönetilen, değiştirilmemiş dosyalar temizlenir. Son backup, saat geri alınmış olsa da korunur.
- Ayarlar/Otomatik sekmeleri, kayıtlı gerçek backup hedefi, klasör seçici, proje başına sonraki Save ve son başarılı backup bilgisi.
- Keşif logları yalnız değişiklik/hata olduğunda eklenir; 15 saniyelik aynı keşif UI geçmişini doldurmaz.
- IPC protokolü 2, assembly/ürün sürümü 0.2.0. Eski app ve yeni host birlikte kullanılmamalı.

## Doğrulama

- Gerçek V21 net48 assembly'siyle Release build; hata ve uyarı yok.
- NUnit: **47/47 başarılı**; zamanlama, Pause/Resume, cancellation, eski proje/session kimliği, uzun çağrı, IPC, JSON, arşiv commit/rotation ve hata izolasyonu.
- Gerçek V21 salt okunur Discover: **1/1 başarılı**, yeni protokol ve SDK ile çalıştı; Save/Archive gönderilmedi.
- Backup testleri yalnız test klasöründe mock adapter'ın oluşturduğu ZIP'leri kullanır; gerçek TIA Archive testi sayılmaz.
- WPF görsel ağacı PNG'ye render edilip incelenir. Masaüstü etkileşim/tray testiyle eşdeğer değildir.
- Salt okunur V21 Discover testi ayrı raporlanır; bu çalışma yeni otomatik Save veya Archive'ı canlı projede çalıştırmaz.

## Gerçek V21 kabulü için kalanlar

1. ZIP'i yeni klasöre çıkarıp v0.2 ile test projesine bağlanın; host firewall izni yeniden istenebilir.
2. Test projesinde 1 dakika Auto Save etkinleştirip gerçek Save logunu, son kayıt zamanını ve geri sayımın yeniden başlamasını gözleyin.
3. Pause süresince yeni kayıt olmamasını, Resume sonrasında tam aralığın beklenmesini doğrulayın.
4. Backup hedefini test dizini seçin, Backup Now çalıştırın. Dosya, manifest ve kaynak proje yolunun değişmediğini kontrol edin.
5. Oluşan `.zap21` dosyasını ayrı test dizinine TIA ile geri yükleyip içeriğini kontrol edin. ZIP/SHA-256 doğrulaması bu adımın yerine geçmez.
6. Çoklu instance, proje değişimi, bağlantı kaybı, Save–Archive arasında düzenleme ve uygulamadan güvenli çıkış senaryolarını tamamlayın.

Start With Windows henüz eklenmedi. Yeni Archive/zamanlayıcı gerçek kabul testleri tamamlanmadan üretim güvenilirliği iddiası yoktur. `.pending-*` başarısızlık çıktıları otomatik rotation dışında tanılama için korunur; disk kullanımı izlenmelidir.
