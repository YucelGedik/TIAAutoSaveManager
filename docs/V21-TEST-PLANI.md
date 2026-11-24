# V21 ilk sürüm ve test planı

Durum: **Plan; henüz çalıştırılmadı.** 19 Kasım 2026.

## İlk dikey kesit

1. net8.0-windows WPF/MVVM dark arayüz; net48/x64 host; Siemens'siz Contracts ve IPC.
2. Kurulu V21 registry/DLL sürümü kontrolü; eksikse açıklayıcı durum, sahte başarı yok.
3. GetProcesses ile salt okunur liste: sürüm, PID, proje yolu; projesiz instance da görünür.
4. Yalnız seçili instance'a Attach; Name/Path snapshot'ı göster. Otomatik Save kapalı.
5. Ayrı test projesinde Save Now: tek uçtan koordinasyon, gerçek API sonucu sonrası başarı zamanı/log.
6. Disconnect/Exit sonrası portal ve proje açık kalmalı. Ardından scheduler; daha sonra Archive/rotation.

## Gerçek TIA önkoşulları

- TIA V21, Openness net48 SDK ve gerekli proje bileşenleri kurulu olmalı.
- Kullanıcı Siemens TIA Openness grubunda olmalı ve etkin token/oturum üyeliği kontrol edilmeli.
- Host için Openness erişim istemi kullanıcı tarafından cevaplanmalı; allowlist kuralları atlatılmamalı.
- Canlı üretim projesi kullanılmaz. Kullanıcı tarafından ayrılmış, kaybı sorun yaratmayacak test projesi açılır.
- Portal/proje kimliği, V21 build/update sürümü, host assembly sürümü ve test zamanı sonuç formuna yazılır.
- Process Dispose/Kill, Project.Close, Compile ve Download güvenlik testi adı altında dahi çağrılmaz.

## Manuel entegrasyon senaryoları

| ID | İşlem | Kabul |
|---|---|---|
| I01 | Portal yokken aç/Refresh | Boş liste; arayüz kullanılabilir, otomatik portal başlatılmaz |
| I02 | Açık V21 test projesini keşfet | PID, sürüm, Name/Path doğru; hiçbir Save/Archive yapılmamış |
| I03 | Aynı sürümden iki instance, birinde proje yok | İkisi ayrı kimlik; projesiz durumda Save devre dışı |
| I04 | Tek test instance'ına Attach | Doğru proje bilgisi; UI donmaz; izin reddi anlaşılır gösterilir |
| I05 | Test projesini değiştir, Save Now | Gerçek Save tamamlanır; değişiklik kalıcıdır; doğru başarı zamanı/log, proje açık kalır |
| I06 | Undo geçmişi olan test projesinde Save | Undo davranışı uyarıyla tutarlı; koruma garantisi verilmez |
| I07 | Save Now'a art arda bas | Aynı projeye çakışan API çağrısı yok, sınırsız kuyruk yok |
| I08 | Kullanıcı portalı kapatır veya projeyi değiştirir | Uygulama çökmez; eski seçimle yeni projeye Save yapılmaz |
| I09 | Aynı anda iki test instance'ı | Seçilmeyen kaydedilmez; seçilenlerin sonuçları ayrı tutulur |
| I10 | Refresh, disconnect, uygulamadan Exit | Kullanıcının portalı ve projesi açık; process listesi handle'larında Dispose yok |
| I11 | Uzun süren Save ve uygulamadan çıkış | Yeni iş alınmaz; zorla sonlandırma yok; sonuç belirsizse başarı gösterilmez |
| I12 | TIA erişim izni reddi | Hata loglanır; otomatik Attach retry fırtınası yok |
| I13 | Test projesinde Save → Archive | `.zap21` oluşur; orijinal proje yolu aynı; ayrı test ortamında Retrieve ile doğrulanır |
| I14 | Save sonrası Archive öncesi kullanıcı düzenlemesi | Başarısız arşiv başarı görünmez; eski yedekler silinmez |
| I15 | Disk/izin/erişilemeyen backup hedefi | Save sonucu korunur; backup ayrı hata verir, rotation yapılmaz |
| I16 | 10 limitinde 11. başarılı backup | Yalnız uygulamanın aynı projeye ait en eski tamamlanmış arşivi silinir |
| I17 | Pause, interval değişimi, uyku/uyanma | Yeni süre tanımlı politikaya uyar; geçmiş tick yığını yok |

Tüm gerçek testler aynı test projesi üzerinde bilinçli kullanıcı etkileşimiyle yürütülür. I13–I17 sonraki fazlara aittir; ilk Save Now kabulü I01–I12 ve ilgili unit testlerdir. Gerçekten yavaş Save üretilemiyorsa I11 mock test sonucu ayrı kaydedilir; gerçek test yapılmış sayılmaz.

## API bağımsız anlamlı testler

- Sahte saat/adapter ile vade öncesi Save yok, vadesinde tek istek; disabled/paused durumda yeni istek yok.
- Bekletilebilen adapter ile manuel + otomatik Save + Archive aynı projede çakışmaz.
- Başlayan senkron çağrı timeout olduğunda kilit bırakılmaz; sonradan gerçek tamamlanma doğru işlenir.
- Bağlantı kaybı ve proje değişimi eski kimliği geçersiz kılar; yeni projenin seçimi otomatik devralınmaz.
- Keşif başarısızlığı önceki snapshot'ı yanlışlıkla başarılı boş sonuç yapmaz; retry/backoff iptal edilebilir.
- Başarısız/eksik backup'ta eski arşivler korunur; başarılı backup sonrası yalnız yönetilen dosyalar döner.
- Aynı adlı farklı projeler ve timestamp çakışmaları yedekleri karıştırmaz.
- Bozuk JSON ve erişim hatasında uygulama açılır; geçerli ayar atomik yazılıp yeniden yüklenir.
- Host çıkışı/IPC kopuşu bekleyen yazma sonucunu Unknown yapar; istemci kör retry yapmaz.

## Sonuç kaydı

Her senaryo için tarih, commit, ortam, proje kimliği, beklenen/gerçek sonuç, Pass/Fail/Not run, log correlation ID ve kanıt yazılmalı. Mock, build ve gerçek TIA sonuçları ayrı sütunlarda tutulmalı. İlk gerçek test tamamlanana kadar ürünün V21 üzerinde çalıştığı veya üretime hazır olduğu iddia edilmez.
