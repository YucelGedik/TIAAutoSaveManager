# V0.2.1 — Archive yol uzunluğu düzeltmesi

Kullanıcı, gerçek Auto Save'in çalıştığını doğruladı. Backup'ta Save başarılı, Archive başarısız: V21 hata mesajı geçici hedefin uzantısız halinin 190 karakter olduğunu ve en çok 143 karakter kabul ettiğini bildirdi.

Neden: v0.2, uzun proje adı + hash klasörü + 32 karakterlik geçici kimlik + uzun tarihli dosya adını aynı native Archive yolunda birleştiriyordu. Önceki mock adapter bu Siemens sınırını uygulamadığından testler sorunu yakalamadı.

Düzeltmeler:

- Native Archive hedefi backup kökünde kısa `.pending-<16 hex>\a.zap21`. Uzun nihai dosya adı yalnız native çağrı tamamlanıp ZIP doğrulandıktan sonra verilir.
- Hem geçici hem nihai tam yol, muhafazakâr olarak `.zap21` uzantısı dahil 143 karaktere sığdırılır. Tarih UTC `yyyyMMdd_HHmmss` ve benzersiz ek içerir.
- Gerekli olduğunda proje adı kısaltılır; aynı adlı farklı projelerin ayrımı ve rotation manifestindeki tam proje hash'i korunur. Sığan mevcut klasör adları değiştirilmez.
- En kısa hedef de sığmıyorsa **Save/Archive öncesinde** açıklayıcı hata. Host, native çağrıdan önce aynı kuralı bağımsız olarak kontrol eder.
- Uzun hata mesajı alt durum alanında en fazla 70 piksel yer kaplar, kaydırılıp kopyalanabilir; ana işlem alanını sıkıştırmaz.
- Geçmiş `.pending-*` çıktıları ve eski backup klasörleri silinmez. Save scheduler davranışı değiştirilmedi.

Regresyon testleri: paylaşılan tam hedef/proje adları; kısa native ad; sınırın 143/144 karakter iki yanı; uzun proje adında uyarlama; sığmayan hedefte sıfır API çağrısı ve eski backup'ın korunması. Mock adapter artık gerçek vakadaki 143 karakter kuralını da uygular.

Doğrulama: **53/53 birim testi**, **1/1 gerçek salt okunur V21 testi** geçti; derlemede hata/uyarı yok. Sonuçlar `artifacts/v021-validation.txt` / `artifacts/test-results` altında. Uzun hata mesajı kendi WPF renderer'ımızla görsel olarak kontrol edilir. Gerçek V21 Archive/Retrieve bu düzeltme üzerinde henüz çalıştırılmadı; kısa hedef düzeltmesinin kullanıcı ortamında Backup Now ve ayrı bir test konumuna geri açma ile doğrulanması gerekiyor.
