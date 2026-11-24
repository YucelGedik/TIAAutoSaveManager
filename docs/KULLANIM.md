# V0.4.3 — Auto Save ve sürümlü Backup

## V0.4.3 pencereyi yeniden açma

Manager arka planda veya tepsideyken masaüstü/Başlat kısayoluna tekrar basın. Yeni örnek açılmaz; mevcut pencereye açılma isteği gönderilir. Pencere gösterilir ve ekrana sığdırılır; tepsi simgesi de yenilenir. Windows Gezgini yeniden başladığında simge yeniden oluşturulur. Başlatma/kapanma sürüyorsa veya eski sürüm yanıt vermiyorsa artık simgenin göründüğünü varsayan “tepsiden açın” uyarısı yerine açıklayıcı bir mesaj gösterilir.

`TIAAutoSaveManager-v0.4.3-Update-win-x64.exe` önceki sürümün açık ana penceresiyle birlikte “Uygulama zaten açık” bildirimini de normal kapanma isteğiyle kapatabilir. Kurucunun kapatma sorusunda Evet seçin. TIA Portal ve aktif kayıt işlemleri zorla sonlandırılmaz.

## V0.4.2 kurulum düzeltmesi

`TIAAutoSaveManager-v0.4.2-Update-win-x64.exe`, açık Manager algılanırsa **Manager kapatılsın mı?** onayı ister. **Evet** normal pencere kapanma isteği gönderir; Manager'ın kendi güvenli ayrılma akışı çalışır. 30 saniye boyunca devam eden işlemin ve host süreçlerinin bitmesi beklenir. Süreçler zorla sonlandırılmaz, TIA Portal'a kapanma isteği gönderilmez. **Hayır** seçildiğinde kurulum dosyaları değiştirilmez. Süre dolarsa işlem bittikten sonra kurulumu yeniden çalıştırın; mesajdaki Manager/host PID'leri hangi sürecin kaldığını gösterir.

Süreç algılama WMI yerine Windows'un yerel süreç listesiyle yapılır. Liste okunamıyorsa ayrı bir hata gösterilir; bu durum yanlış biçimde “Manager açık” sayılmaz. Başka Windows oturumundaki Manager o oturumdan kapatılmalıdır.

## V0.4.1 güncellemesi

Mevcut Manager'ı sistem tepsisindeki **Exit** ile kapatıp `TIAAutoSaveManager-v0.4.1-Update-win-x64.exe` dosyasını çalıştırın. Kaldırmanız gerekmez. Ayarlar ve yedekler korunur; yeniden açıldıktan sonra otomatik işlemleri proje bazında yeniden etkinleştirin.

Bu güncelleme ilk açılışta ekrana/DPI'a sığdırmayı, ekran değişikliklerinde pencereyi görünür alana getirmeyi ve yardım rehberinin son görsel düzenlemelerini içerir. Kullanıcı adını bulma rehberi üç ekran görüntüsüyle eklendi; bölüm içi numaralar ve kare adım rozetleri tutarlı hale getirildi. V20/V21 desteği korunur.

Küçük Update paketi .NET kurucularını içermez; önceki tam Setup ile yüklenen önkoşulları kullanır. Eksik .NET uyarısı varsa önce aşağıdaki tam Setup'ı, ardından bu güncellemeyi uygulayın.

## V20 + V21 desteği

V20 ve V21 aynı listede kendi sürüm etiketiyle görünür. Her sürüm ayrı bir host sürecinde kendi yerel Siemens SDK'sını kullanır; V20 bilgisayarında V21 kurulumu gerekmez. V20 yedekleri `.zap20`, V21 yedekleri `.zap21` olarak üretilir. Yedeği kaynak TIA sürümüyle geri açın.

V20 Openness API'si, yerel ve kimliği doğrulanmış `Siemens.Engineering.dll` üzerinden çalışma anında bağlanır. Geliştirme bilgisayarında yalnız V21 kurulu olduğundan V20 bağlantısı için gerçek V20 bilgisayarında kabul testi gereklidir.

## Setup ile kurulum

`TIAAutoSaveManager-v0.4.0-Setup-win-x64.exe` dosyasını çalıştırın ve Windows yönetici iznini onaylayın. Kurulum Türkçedir; varsayılan konum `C:\Program Files\TIA Auto Save Manager` olur. Başlat menüsü ve isteğe bağlı masaüstü kısayolu oluşturulur.

Setup, .NET 8 **Windows Desktop Runtime x64** (paket sürümü 8.0.34 veya daha yeni 8.0 yaması) ve .NET Framework 4.8 veya üstünü kontrol eder. Eksik bileşenler pakete dahil, Microsoft imzalı kurucularla yüklenir; hedef bilgisayarda internet gerekmez. .NET 9/10'un tek başına kurulu olması .NET 8 gereksinimini karşılamaz. Yeniden başlatma istenirse çalışmalarınızı kaydedip Windows'u yeniden başlatın ve Setup'ı tekrar çalıştırın.

Güncellemeden veya kaldırmadan önce Manager'ı sistem tepsisindeki **Exit** ile kapatıp işlemin bitmesini bekleyin. Kurucu açık Manager/host algılarsa durur; süreçleri zorla kapatmaz. TIA Portal açık kalabilir. ZIP sürümünden geçiyorsanız eski klasör yerine kurulumun oluşturduğu kısayolu kullanın.

TIA Portal V20/V21 ve aynı sürümün Openness bileşeni, Siemens kurulum medyasıyla ayrıca kurulmalıdır. Kullanıcı grubu üyeliği ve TIA erişim onayı için Yardım sekmesini izleyin. Kaldırma, proje/yedek dosyalarını, kullanıcı ayarlarını ve paylaşılan .NET bileşenlerini silmez.

## Yardım sayfası

Üstteki **Yardım** sekmesini, **F1** tuşunu veya tepsi menüsündeki Yardım seçeneğini kullanın. İlk kurulum ve kullanıcı adını bulma adımları yedi ekran görüntüsüyle anlatılır. Bir görsele tıklayarak büyütebilir, ekrana sığdırabilir veya yakınlaştırabilirsiniz. Rehber ve görseller uygulamanın içinde bulunur; internet gerekmez.

Bilgisayar ayarlarındaki **05 / Windows kullanıcı adınızı bulun** adımı, Başlat → kullanıcı simgesi → Hesap ayarlarını değiştir → Hesaplar / Bilgileriniz yolunu gösterir. Örnek `utente/UTENTE` yerine kendi hesabınızı kullanın. Görünen ad gerçek oturum adından farklı olabileceğinden `Win + R → cmd → Enter` ile Komut İstemi açıp `whoami` çalıştırın; `BİLGİSAYAR\kullanıcı` veya `ETKİALANI\kullanıcı` sonucundaki tam hesabı gruba eklerken **Adları Denetle** ile doğrulayın. Grup ayarlarını 03–04 adımlarında tamamladıktan sonra **06 / Windows oturumunu yenileyin** adımını uygulayın.

Manuel Save, kullanıcı tarafından gerçek V21 projesinde doğrulandı. Bu sürüm proje bazında Auto Save, bağımsız zamanlı Backup, Pause/Resume ve rotation ekler. Yeni otomatik/Archive akışları için gerçek V21 kabul testi ayrıca yapılmalıdır.

## V0.1'den güncelleme

Manager'ı ve host'unu normal şekilde kapatın, ZIP'i **yeni bir klasöre** çıkarıp yeni exe'yi açın. Yalnız exe'yi veya yalnız host'u değiştirmeyin; v0.2 IPC protokolü birlikte güncellenmelidir. Önceki JSON ayarlar okunur. Host exe değiştiği için TIA erişim onayını yeniden isteyebilir.

## Çalıştırma

Ana pencere ilk açılışta bulunduğu monitörün görev çubuğu dışındaki kullanılabilir alanına ve DPI ölçeğine göre boyutlanır. Küçük ekranlarda minimum pencere boyutu da uyarlanır; içerik kaydırılabilir, Yönetim ve Yardım sekmeleri üstte kalır. Çözünürlük/ölçek değiştiğinde, başka monitöre taşıma bittiğinde ve tepsiden geri açıldığında pencere yeniden ekrana sığdırılır.

`TIAAutoSaveManager.App.exe` dosyasını aynı klasördeki tüm DLL'ler ve `host` alt klasörüyle birlikte çalıştırın. Host'u tek başına başlatmayın. Uygulama x64'tür.

Önkoşullar:

- Windows x64 ve Siemens'in desteklediği TIA Portal V20 veya V21 kurulumu.
- .NET 8 Windows Desktop Runtime x64 ve .NET Framework 4.8.
- Kullandığınız sürümün Openness kurulumu ve registry kaydı: V20 için `PublicAPI\V20\Siemens.Engineering.dll`; V21 için `PublicAPI\V21\net48\Siemens.Engineering.Base.dll`.
- Bağlanacak Windows kullanıcısının **Siemens TIA Openness** yerel grubunda etkin üyeliği. Yönetici ekleme yaptıktan sonra Windows oturumunu kapatıp yeniden açın. Program bu üyeliği veya sistem güvenlik ayarlarını değiştirmez.
- Host exe için TIA'nın erişim istemine kullanıcı onayı. Güncellemeden sonra exe hash/tarihi değişeceği için yeniden istenebilir.

## Test projesinde Save Now

1. Ayrı test kopyasını kaynak sürümüyle açın: V20 için `.ap20`, V21 için `.ap21`.
2. Manager'da **Yenile** seçin. Keşif ayrıca 15 saniyede bir otomatik yapılır; keşif Save değildir.
3. Projeyi/PID'yi seçin, **Bağlan** seçin. Gerekirse TIA erişim istemini cevaplayın.
4. Gösterilen tam proje yolunun test kopyası olduğunu doğrulayın.
5. **Save Now** seçin; proje yolu/PID/Undo uyarısı bulunan onayı kontrol edin. Varsayılan cevap Hayır'dır.
6. Gerçek API başarıyla dönerse Son Kayıt alanı güncellenir. Bu, her çağrının dosya değişikliği yaptığı garantisi değildir; değişiklik yoksa Save yine başarıyla dönebilir.

Save Undo/Redo geçmişini temizler. Program proje kapatma, derleme, PLC'ye download, SaveAs veya portal sonlandırma komutu göndermez.

## Otomatik kayıt

1. Instance'a bağlanın. Sağdaki **Ayarlar** sekmesinden kayıt aralığını seçin: 1/5/10/15/30/60 dakika veya 1–1440 özel değer.
2. **Ayarları kaydet** seçin. Yalnız metin alanına yazmak aralığı uygulamaz.
3. **Otomatik** sekmesinde **Auto Save’i etkinleştir** seçin, proje yolu ve Undo uyarısını onaylayın.
4. İlk Save tam aralık sonunda yapılır. Seçili proje geri sayımı ve listede **S/B** etkinlik göstergesi görünür; S=Save, B=Backup.
5. Her instance için seçimi ayrı yapın. Yeni projeler veya yeniden açılan uygulama kendiliğinden etkinleşmez.

**Pause** bütün otomatik işleri duraklatır. Başlamış Siemens çağrısı kesilmez; manuel Save/Backup kullanılabilir. **Resume** sayaçları tam aralıkla yeniden başlatır. Uyku/uyanma veya yavaş işlem sonrasında kaçırılan tick'ler kuyruğa yığılmaz. Başarılı manuel Save de ilgili projenin Auto Save sayacını yeniden başlatır.

Proje/PID/session değişirse veya keşif güvenilirliğini kaybederse ilgili otomatik seçim kapatılır; yeniden bağlanıp bilinçli olarak etkinleştirin. Bağlantı belirsizliği sonrası otomatik yazma tekrar edilmez.

## Backup ve kayıtların yeri

**Save Now / Auto Save**, açık projeyi kendi konumuna kaydeder. Ayrı yedek dosyası üretmez.

**Backup Now / Otomatik Backup**, önce Save ve sonra seçili sürümün Archive API'sini kullanır. V20 için `.zap20`, V21 için `.zap21` üretir. Kaynak klasörü dosya kopyalama yöntemiyle yedeklenmez. Backup da Save içerdiğinden Undo geçmişini etkiler. Aşağıdaki adlandırma ve geri açma örnekleri V21 içindir; V20 için uzantıları `.zap20`/`.ap20` olarak kullanın.

Varsayılan hedef Windows Belgeler klasörünüz altındadır:

```text
Documents\TIAAutoSaveManager\Backups\ProjeAdı_20261120_0835\
  ProjeAdı_20261120_0835.zap21
  ProjeAdı_20261120_0835.zap21.manifest.json
```

Klasör ve dosya adında yedek işleminin başladığı **yerel bilgisayar saati**, `yyyyMMdd_HHmm` biçiminde kullanılır. Aynı dakika içindeki ek yedekler `_02`, `_03` eki alır; mevcut dosyalar ezilmez. Proje yolundan üretilen kimlik yalnız yönetim manifestinde saklanır. Aynı adlı farklı projelerin yedekleri ayrı klasörlere alınır ve retention hesabı bu iç kimlikle ayrılır. **Ayarlar → Klasör seç… → Ayarları kaydet** ile hedefi değiştirin. **Otomatik → Yedek klasörünü aç** kaydedilmiş gerçek hedefi açar.

V0.2.1, bildirilen V21 Archive **143 karakter** sınırını muhafazakâr biçimde uzantı dahil uygular. Native Archive, seçilen backup kökü altındaki kısa `.pending-*\a.zap21` yoluna çalışır. Dosya doğrulandıktan sonra tarihli son adına taşınır. Uzun proje adları gerektiğinde kısaltılır; tarih/saat ve manifestteki proje kimliği korunur. En kısa üretilebilir yol bile sınıra sığmıyorsa Save/Archive çağrısından önce açıklayıcı hata verilir; örneğin `C:\TIA_Backups` gibi kısa bir hedef seçin. Windows uzun yol ayarını değiştirmek bu uygulama/API sınırı için çözüm değildir.

V0.2'den kalmış `.pending-*` klasörleri otomatik silinmez ve başarılı backup sayılmaz. Otomatik işlemleri durdurup uygulamadan çıktıktan sonra içeriklerini inceleyebilirsiniz. Eski hash adlı klasörler ve dosyalar kendiliğinden yeniden adlandırılmaz. Rotation, aynı proje kimliğine ait geçerli manifestleri hem eski klasörlerde hem yeni tarihli klasörlerde sayar. İlgili arşivler silindikten sonra yalnız boş kalan yönetilen klasörler kaldırılır; kullanıcı dosyaları ve geri açılmış projeler korunur.

Otomatik Backup varsayılan aralığı 60 dakikadır ve Auto Save'den ayrı etkinleştirilir. **Backup Now** tek seferlik çalışır. Saklama sayısı 5/10/20, sınırsız için 0; varsayılan 10'dur. Yeni arşiv başarılı olup ZIP okunabilirlik ve SHA-256 kontrolünden geçmeden rotation yapılmaz. Manifest, doğrulama ve sahiplik bilgisidir; arşiv dosyasıyla birlikte tutulmalıdır.

Rotation yalnız uygulamanın aynı projeye ait geçerli manifestle yönettiği arşivleri siler. Yabancı dosyalar, sonradan değiştirilen arşivler ve `.pending-*` başarısız işlem dizinleri silinmez. Bu durumlarda klasörde limitten fazla dosya kalabilir. Arşiv doğrulaması **TIA'da geri yükleme garantisi değildir**; ayrı test ortamında Retrieve testi gerekir.

Save ile Archive arasında kullanıcı yeniden düzenleme yaparsa Archive hata verebilir; Save başarısı ayrıca korunur ve eski yedekler silinmez. Hedefte en az 256 MB boş alan ön kontrolü yapılır; bu eşik büyük bir projenin sığacağını garanti etmez. Junction/symlink hedefleri ve açık proje klasörünün içindeki backup hedefleri kabul edilmez.

Projeyi veya portalı kapatırsanız bağlantı geçersizleşir. Kayıt sırasında bağlantı koparsa sonuç **bilinmiyor** olarak gösterilir ve tekrar yazma engellenir. TIA'da projenin durumunu kontrol edip Manager'ı yeniden başlatın. Aktif çağrının bittiğini doğrulamadan ikinci bir Manager çalıştırmayın.

Uzun API çağrısı zorla iptal edilmez. Manager çıkışı, devam eden işlem varken reddedilir; tamamlandıktan sonra yeniden çıkabilirsiniz. **Bağlantıları bırak** yalnız bu host'un TiaPortalSession bağlantılarını bırakır. Canlı portalın açık kalması gerçek testte ayrıca doğrulanmalıdır.

## Yedeği geri açma: hedef klasör ve uzun yol hatası

`.zap21` sıkıştırılmış proje arşividir. TIA'nın **Select target directory** sorusu kütüphane değil, arşivin çıkarılacağı klasörü ister. Çıkarma sonrasında proje açılır. Sonraki açılışlar çıkarılan `.ap21` üzerinden yapılabilir.

Eski uzun yollu yedek için güvenli deneme:

1. Ayrı TIA oturumunda açık proje olmamasını sağlayın; mevcut çalışmalarınızı kaydedin.
2. Yalnız arşiv dosyasını kısa bir yere **kopyalayın**, örneğin `C:\TIA_Test\b.zap21`. Orijinal yedeği ve manifestini taşımayın/değiştirmeyin.
3. `C:\TIA_Test\R01` gibi yeni, boş bir hedef oluşturun.
4. TIA **Project → Open → Browse** ile `b.zap21` dosyasını seçin. Ardından **Select target directory** penceresinde boş `R01` klasörünü seçin.
5. Hedef olarak mevcut projenin içindeki `System`, `IM`, `src` vb. klasörleri seçmeyin. Yeni denemelerde yeni boş `R02` gibi bir hedef kullanın.

**Subfolder already exists** uyarısındaki **Yes**, mevcut proje klasörünü silip arşiv içeriğiyle değiştirir. Mevcut projeyi korumak için **No** seçin ve yeni boş hedef kullanın.

20 Kasım'daki kullanıcı ekranında Retrieve, 161 karakterlik yolun 143 sınırını aştığını bildirdi. Bu mesaj eksik kütüphane veya doğrulanmış arşiv bozulması tanısı değildir. Eski hash adlı klasördeki geçmiş arşivler yeni sürüm kurulunca otomatik kısalmaz; hem arşivin kopya yolunu hem çıkarma hedefini kısa tutun.

Resmî işlem sırası: [Siemens V21 — Retrieving compressed project](https://docs.tia.siemens.cloud/r/en-us/v21/editing-projects/archiving-and-retrieving-projects/retrieving-compressed-project).

## Ayarlar, loglar ve tray

- Normal veri klasörü `%LOCALAPPDATA%\TIAAutoSaveManager`.
- Ayarlar `settings.json`; hatalı dosya ayrı `.invalid-*` kopyasında korunur; önceki geçerli dosya yazmada `.bak` olur.
- Rolling Serilog dosyaları `Logs` altında; desktop ve Openness host ayrı dosyalara yazar. Her sink son 14 dosyayı tutar, 10 MB'da dosya döndürür.
- Küçültme, ayar açıksa sistem tepsisine gizler. Pencerenin X düğmesi uygulamadan çıkar. Tepsi menüsünde Open Manager, Save Now, Backup Now, Pause/Resume, View Logs ve Exit bulunur.
- Arayüz son 100 oturum olayını gösterir; ayrıntılı exception/correlation bilgisi dosyadadır.
- Geliştirme/test için `TIAAUTOSAVE_DATA_DIR` ortam değişkeni veri klasörünü değiştirir.

## Geliştirici komutları

PowerShell'de repository kökünden:

```powershell
.\scripts\build.ps1
.\scripts\build.ps1 -SkipRestore -ReadOnlyV21 -Publish
```

Visual Studio 2022 veya uyumlu yeni MSBuild ve .NET 8 SDK gerekir. SDK sürümü global.json'da sabitlenmiştir. net48 Developer Pack bulunmalı. Özel SDK yolu için `dotnet build ... /p:OpennessBaseAssembly="C:\...\Siemens.Engineering.Base.dll"` kullanılabilir; runtime her durumda kurulu V21 registry kaydını doğrular.

Salt okunur V21 testi yalnız Discover gönderir; Attach/Save yapmaz. Gerçek proje kayıt testi ayrıca ve bilinçli şekilde yürütülür. Windows sandbox'ı File.Replace/named pipe işlemlerini engelliyorsa testleri normal Windows oturumunda çalıştırın; güvenlik kontrollerini koddan kaldırmayın.

Yerleşim kontrolü için `TIAAutoSaveManager.App.exe --render-preview C:\tam\yol\preview.png` yalnız kendi WPF ağacını dosyaya çizer; masaüstünü kontrol etmez ve TIA bağlantısı kurmaz. Bu bir render kontrolüdür; interaktif UI/tray testinin yerine geçmez.

