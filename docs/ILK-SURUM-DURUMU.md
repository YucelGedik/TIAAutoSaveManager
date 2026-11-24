# İlk manuel Save sürümü — 19 Kasım 2026

Bu dosya v0.1'in tarihsel durumudur. Sonrasında kullanıcı kendi V21 ortamından Connected ve başarılı Project.Save ekranlarını paylaştı. Güncel geliştirme için `V02-DURUM.md` ve `KULLANIM.md` dosyalarına bakın.

## Uygulananlar

- Altı projeli solution: Contracts (net8/net48), Core, Infrastructure, App (net8 WPF), OpennessHost (net48/x64), NUnit testleri.
- MVVM dark arayüz, instance listesi, PID/sürüm/proje yolu, bağlantı durumu ve son başarılı Save zamanı.
- Gerçek V21 GetProcesses / Attach / Projects / Project.Save adapter'ı. Sahte Siemens sınıfları veya taklit Save başarısı yok.
- Kullanıcı SID'siyle kısıtlı named pipe, protokol versiyonu, istek kimliği, bounded JSON frame; Siemens tipleri süreç sınırını geçmez.
- Tek MTA host thread'i, oturum registry'si ve işlem kapısı. Uzun Save sırasında başka istek kabul edilmez; iptal yalnız dispatch öncesinde geçerlidir.
- PID + process başlangıç zamanı + session nesli + proje yolu kontrolü; kayıt sonucu belirsizken yeniden yazma engeli.
- Kaynak proje kapatma/portal sonlandırma komutu yok. Cleanup yalnız host PID'sine ait TiaPortalSession.Dispose kullanır. GetProcesses handle'ları Dispose edilmez.
- Group membership ve SDK/assembly identity denetimi; erişim reddinde anlaşılır hata.
- Atomik JSON ayarlar, bozuk ayardan toparlanma, ayrı desktop/host Serilog rolling dosyaları.
- Sistem tepsisi, Save Now/Logs/Exit menüsü, güvenli çıkış ve aynı oturumda tek Manager kilidi.

## Sonuçlar ve sınırlar

| Kontrol | Sonuç |
|---|---|
| Release build | Başarılı; sıfır hata, sıfır uyarı |
| NUnit | 18/18 başarılı; concurrency, cancellation, eski kimlik, IPC çerçevesi, ayar kurtarma/kalıcılık |
| Gerçek V21 salt okunur keşif | 1/1 başarılı, gerçek Base 21.0.0.0 yüklenerek Discover çalıştı. İlk kontrolde 0, son kontrolde 1 V21 instance bulundu; Attach yapılmadı |
| WPF görsel ağacı | Uygulamanın kendi RenderTargetBitmap yolu ile PNG üretildi, görsel incelendi |
| Masaüstünde interaktif arayüz/tray | Tamamlanmadı; Computer Use yeni uygulamaya erişimi onaylamadı. Render kontrolü bunun yerine geçmiş sayılmıyor |
| Gerçek Attach / Save / güvenli disconnect | Çalıştırılmadı; Openness grup üyeliği eksik |
| Auto Save / Archive / rotation | Henüz uygulanmadı; UI açıkça Kapalı gösteriyor |
| Start With Windows / Pause | Henüz yok; scheduler sonrası değerlendirilecek |

Windows sandbox'ında ilk denemeler File.Replace ve named pipe erişiminde engellendi. Normal Windows izinleriyle yeniden çalıştırılan testler geçti. Güvenlik kontrolleri kaldırılmadı. İlk cancellation testi NUnit exact exception beklentisi nedeniyle düzeltildi; test artık OperationCanceledException alt sınıflarını da doğru kabul ediyor.

## Kullanıcının belirlediği gerçek test projesi

Kaynak:

`D:\TestProjects\Machine_V21`

`.ap21` projesi bulundu; kaynakta yazma yapılmadan şu izole test dizinine kopyalandı:

`<workspace>\artifacts\integration-test-project\Machine_V21`

Son kontrolde kaynak proje dosyaları başka süreç tarafından kilitlendi ve iki portal işletim sistemi süreci görüldü. Bu nedenle kaynak/kopya tam hash karşılaştırması tamamlanamadı; **kopyanın tutarlılığı ve TIA'da açılabilirliği doğrulanmadı**. Bu dizin geçerli backup olarak sunulmaz. Açık kaynağın dosya kopyasını tekrar almak yerine, sonraki aşamada test kaynağı kapalıyken yeniden hazırlama veya resmi arşivden ayrı test projesi çıkarma kullanılmalı.

Etkin Windows hesabı `DOMAIN\testuser`, Siemens TIA Openness grubunda değil; yerel grup üye sayısı 0. Yönetici grup üyeliği eklemeli ve kullanıcı oturumu yenilenmeli. Uygulama sistem güvenlik ayarlarını değiştirmedi. Kaynak proje üzerinde Save/Archive/Close/Compile/Download çağrısı yapılmadı.

Bu önkoşul sağlanınca izole `.ap21` dosyası V21'de açılarak `V21-TEST-PLANI.md` içindeki I02–I12 uygulanmalı. İlk gerçek Save doğrulanmadan otomatik kayıt/yedekleme aşamasına geçilmiyor.

## Çıktılar

- `artifacts/TIAAutoSaveManager/`: framework-dependent x64 yerel uygulama paketi; tüm klasör birlikte kullanılır.
- `artifacts/TIAAutoSaveManager-manual-v0.1.0-win-x64.zip`: aynı paketin ZIP çıktısı; `package-manifest.json` dosya hash'lerini içerir.
- `artifacts/test-results/unit-tests.trx` ve `read-only-v21.trx`: birbirinden ayrı sonuçlar.
- `artifacts/final-validation.txt`: derleme/test/publish çıktısı.
- `artifacts/ui-preview.png`: salt WPF render görüntüsü, gerçek TIA bağlantısı değildir.
- `artifacts/integration-test-preflight.json`: belirlenen proje kopyası ve grup üyeliği bulgusu.
- `docs/THIRD-PARTY-NOTICES.md` ve `docs/licenses`: dağıtılan bağımlılık bildirimleri.

Uygulama çalıştırılabilir geliştirme sürümüdür. Gerçek Save, çoklu canlı instance, aktif çağrı sırasında kapanış ve tray davranışı kabul testleri tamamlanmadan üretime hazır olduğu söylenemez.

Yayımlanan raporda yerel hesap ve proje yolları anonimleştirilmiştir.

