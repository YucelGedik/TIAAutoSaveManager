# FAZ 1 — TIAAutoSaver incelemesi ve V21 mimari kararı

İnceleme tarihi: **19 Kasım 2026**, Europe/Istanbul.

## 1. Sonuç ve kapsam

Mevcut TIAAutoSaver, resmi Openness üzerinden çoklu instance seçerek kayıt yapma fikrini somutlaştırıyor. Ancak V21'e hazır, güvenli bir ürün temeli olarak doğrudan alınmamalı. Paylaşılan koleksiyonlar korunmuyor, bağlantılar her döngüde yeniden açılıyor, hata ve kapanış yönetimi yetersiz. WPF, sürümlü arşivleme, rotation, tray ve kalıcı yapılandırılmış log yok.

**Karar:** Upstream kodu yeni uygulamaya taşımadan, resmi Siemens belgelerinden bağımsız uygulama geliştirmek. İlk kilometre taşı V21 instance keşfi → kullanıcı seçimi → bağlantı → proje bilgisi → ayrı test projesinde manuel Save Now. Auto Save ve backup bu doğrulamadan sonra.

Kullanıcının son talebine uygun olarak bu aşamada uygulama kodu yazılmadı. Depo klonlandı, kaynak ve yerel SDK belgeleri okundu, ortam kontrol edildi ve upstream derlemesi denendi. TIA başlatılmadı; Attach, Save, Archive, Close, Compile veya Download çalıştırılmadı. Bu rapor çalışma zamanı güvenilirlik sertifikası değildir.

## 2. İncelenen sürüm ve izlenebilirlik

| Alan | Sonuç |
|---|---|
| Asıl kaynak | https://codeberg.org/Schievel/TIAAutoSaver.git |
| Yerel klon | `research/upstream/TIAAutoSaver` |
| İncelenen HEAD | `02379e39ac42759999d897ad7329ba5d9c5818d3` |
| HEAD tarihi | 2021-07-06T15:35:38+02:00 |
| Son kaynak ekleme commit'i | `ec6749c7cc5919d9ea18d40e5ec987712fc6c24c` |
| Kaynak bütünlüğü | İnceleme sonunda `git status --short` temiz |

GitHub deposu 6 Nisan 2026'da arşivlenmiş ve Codeberg'e yönlendiriyor. İncelemede güncel kaynak olarak Codeberg klonu kullanıldı; HEAD'in 2021 tarihli olması klonda yeni V21 çalışması bulunmadığını gösterir. Bu, projenin başka dallarda veya gelecekte güncellenmeyeceği anlamına gelmez. [GitHub depo durumu](https://github.com/Schievel1/TIAAutoSaver), [Codeberg](https://codeberg.org/Schievel/TIAAutoSaver).

Satır referansları yukarıdaki sabit commit'e aittir. Denetlenebilir ortam ve derleme çıktıları `research/evidence` altındadır. Kaynak ZIP/EXE çalıştırılmadı.

## 3. Dil, framework ve mimari

| Bileşen | Kaynak ve değerlendirme |
|---|---|
| Ana uygulama | C#, Windows Forms, .NET Framework 4.5; `TIAAutoSave.csproj:9-14` |
| Starter | C# konsol, SDK tarzı proje, `net461`; starter `.csproj:1-6` |
| Siemens referansı | Portal V16 kurulumundaki V15.1 `Siemens.Engineering.dll` dosyasına geliştiriciye özgü göreli HintPath; ana `.csproj:79-82` |
| Arayüz | İki ListView ile kayıt dışı / kayıt seçili instance listeleri; code-behind |
| İş motoru | `TIAAutoSave.Run()` içinde sürekli Thread.Sleep + Save döngüsü |
| Keşif | Ayrı background Thread; UI Invoke ile beş saniyelik yenileme |
| IPC | .NET Remoting IpcChannel + MarshalByRefObject; isteğe bağlı Starter PID ekliyor |
| Ayarlar | EXE klasöründe `TIAAutosave.ini`, tek decimal interval |
| Log | Console.WriteLine; kalıcı sink, seviye veya yapılandırılmış olay yok |
| Test | İzlenen ağaçta test projesi bulunmadı |

WPF assembly referansları bulunması arayüzü WPF yapmaz; form `System.Windows.Forms.Form` türevi ve MVVM kullanılmıyor. UI, process seçimi, ayarlar, IPC ve yaşam döngüsü aynı sınıfta. Release/x86 konfigürasyonunda PlatformTarget=AnyCPU/Prefer32Bit=false kullanılması da açık bir x64 ürün hedefi değil.

## 4. Instance algılama ve bağlantı

`RefreshNoAutosaveProcesses()` (`TIAAutosaveForm.cs:102-130`) ve `RefreshAutosaveProcesses()` (`131-168`) ayrı ayrı `TiaPortal.GetProcesses()` çağırıyor. Seçimler PID üzerinden korunuyor. Projesi olmayan instance normal soldaki listede gösterilmiyor (`247`); Starter ile eklenenler için istisna var.

Keşif thread'i `Invoke` ile UI thread'inde yenileme çalıştırdığı için GetProcesses ve ProjectPath okumaları UI'yi bloke edebilir. Her beş saniyede iki keşif yapılır; keşif sonucunu UI durumundan yeniden üretmek yarışlara ve seçim tutarsızlığına yol açar.

`SavePortals()` (`TIAAutoSave.cs:47-71`) seçili süreçleri gezer, her biri için `process.Attach()` çağırır. İlk açık proje bulunursa `Save()` çağırır. Kalıcı bağlantı registry'si, durum makinesi, Disposed event işleme veya yeniden bağlantı politikası yok. Dönen `TiaPortal` referansı yerel değişkende kalır; deterministik session cleanup yapılmaz. GC'nin ne zaman temizleyeceği belirsiz olduğundan bağlantı/kaynak birikimi riski vardır; bunu ölçülmüş sızıntı olarak sunmuyoruz.

Resmi V21 API, GetProcesses ve Attach yaklaşımını destekliyor; aynı AppDomain içinde farklı TIA sürümlerine erişim desteklenmiyor. Assembly çözümleme Openness tipleri ilk kullanılmadan önce kurulmalı; Siemens referanslarında Copy Local=False öneriliyor. [V21 bağlantı belgesi](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/general-functions/connecting-to-the-tia-portal).

## 5. Save ve interval davranışı

- `TIAAutoSave.cs:20-30`: süre kadar bekler, ardından tüm seçili süreçleri sırayla kaydeder. İlk kayıt açılışta hemen yapılmaz.
- UI aralığı decimal dakika: minimum **0,1**, maksimum **60**, artış **0,5**; Designer `128-153`.
- Designer başlangıcı **0,2 dakika**. Worker, ayar dosyası okunmadan önce bu değerle başlatılır (`TIAAutosaveForm.cs:41-59`). Sonradan INI veya 5 dakika varsayılanı uygulanır. Worker önce uykuya girdiyse ilk kayıt 12 saniye sonra olabilir.
- Süre değişikliği mevcut Sleep'i kesmez; değişen değer sonraki beklemede kullanılır.
- Döngü süresi = interval + bütün save süreleri + UI güncellemesi. Sabit zamanlı scheduler değil; yavaş bir instance diğerlerini geciktirir.
- Tek save worker, kendi döngüsü içinde doğal olarak seri çalışır. Ancak yeniden giriş engeli, Save/Archive koordinatörü veya gelecekte eklenecek manuel Save için kilit yoktur.
- UI'daki son kayıt zamanı `ProjectPath.LastWriteTime` (`TIAAutosaveForm.cs:288-290`). Uygulamanın başarılı API sonucu zamanı değildir. `localDate` de Save öncesinde alınır; başarı sonrası tamamlanma zamanı tutulmuyor.

Resmi V21 belgesi `Project.Save()` metodunu doğruluyor. Belgedeki aç/kaydet/kapat örneğinin `finally Project.Close()` bölümü, kullanıcıya ait açık projeyi yöneten uygulamamıza alınmamalı. Save Now yalnızca seçilmiş açık projeyi kaydetmeli. [V21 Save](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/functions-for-projects-and-project-data/saving-a-project).

**Undo/Redo:** V21 belgesi Saving işleminin action stack'i boşalttığını açıkça belirtiyor. İlk Auto Save etkinleştirmesinde anlaşılır uyarı ve Pause sunulmalı; Save Now ve Save içeren backup akışı da bu davranışı görünür kılmalı. [V21 Undo/Redo](https://docs.tia.siemens.cloud/r/en-us/v21/introduction-to-the-tia-portal/undoing-and-redoing-actions/basics-of-undoing-and-redoing-actions).

## 6. Öncelikli kaynak kod bulguları

Aşağıdakiler statik inceleme bulgularıdır; gerçek TIA üzerinde yeniden üretim yapılmadı.

| Öncelik | Kanıt | Etki ve gerekli düzeltme |
|---|---|---|
| Yüksek | Form `66-68`, `135-163`; motor `50-53` | Gerçek mutable List dışarı veriliyor; UI Clear/Add yaparken save thread foreach çalışıyor. Koleksiyon değişti hatası ve döngüde kalan instance'ların atlanması olası. Immutable snapshot ve tek sahipli registry kullan. |
| Yüksek | Motor `10`, `27`, `42-44` | Decimal interval farklı thread'lerde senkronizasyonsuz okunup yazılıyor; decimal atomik erişim garantisi yok. Thread-safe ayar mesajı/snapshot kullan. |
| Yüksek | Motor `32-34` | Üst seviye exception tamamen yutuluyor. Koleksiyon hatası ve shutdown hatası görünmez kalır. Yapılandırılmış log ve görünür sonuç üret. |
| Yüksek | Motor `58-62` | Her döngü Attach; bağlantı ve proje kimliği tekrar doğrulanmıyor. Kullanıcı aynı PID içinde başka proje açarsa yeni proje önceki seçimi devralabilir. Proje değişiminde Auto Save duraklat, yeniden seçim iste. |
| Yüksek | Form `56-58`, `319-323` | Bozuk/out-of-range INI açılışı kesebilir; EXE klasörüne yazma izni yoksa kapanışta hata. LocalAppData, doğrulanmış JSON, atomik yazım ve bozuk ayardan toparlanma gerekli. |
| Yüksek | Form `318-323`, worker sonsuz döngüler | Cancellation/join/drain yok; Environment.Exit sürmekte olan işin sonucunu belirsiz bırakır. Yeni işleri durdur, aktif çağrı sonucunu bekle, TIA'yı zorla sonlandırma. |
| Orta | Form `92-94`, `106`, `135` | Keşif UI thread'inde; yavaş API arayüzü dondurabilir. Openness erişimini worker'a taşı, UI'ye yalnız DTO gönder. |
| Orta | Form `261-272` | `isStartedAutosaveProcess` foreach dışında; bir instance için true olduktan sonra sonraki satırlara taşınır. Her instance için yeniden hesapla. |
| Orta | Form `143-159` | isStarted kontrolünde güncel `process` yerine eski satırdaki `process2` karşılaştırılıyor. Liste koşulları yanlış etkilenebilir. Seçimi UI satırlarından üretme. |
| Orta | Form `86-99` | Hata durumunda 5000 ms Sleep atlanır; Invoke sürekli başarısız olursa sık hata döngüsü oluşabilir. Her yolda bekleme/backoff ve iptal kontrolü gerekli. |
| Orta | Form `73-82`; Starter `14-22`, `47` | PID tekrar ekleme kontrolü ve hazır olma protokolü yok; args[0] doğrulanmıyor, sabit 10 saniye bekleniyor. İlk sürümde Starter alma. |
| Orta | Motor `59-62` | FirstOrDefault üç kez çağrılıyor; proje arada kapanır/değişirse hata/yanlış metadata riski. Tek yakalama, işlem öncesi kimlik kontrolü, bağlantı kaybı yönetimi. |
| Orta | Form `302-313` | Eski .NET Remoting ve sabit IPC adı; uygulama kodunda erişim kısıtları açıkça kurulmamış. Kanalın gerçekten herkese açık olduğu iddia edilmiyor. Yeni mimaride current-user ACL'li named pipe kullan. |
| Düşük | Form `43-52` | Thread'ler delegate alanlarından önce başlatılıyor; Sleep ile initialization sırası varsayılıyor. Önce bütün durumu kur, sonra worker başlat. |

Siemens, Openness nesnelerinin kendiliğinden thread-safe olmadığını; STA üzerinde oluşturulan/attach edilen nesnelerin aynı STA thread'inden kullanılmasını bildiriyor. Çok thread kullanımında MTA öneriliyor. MTA seçimi tek başına veri yarışlarını çözmez. [V21 belge setindeki threading açıklaması](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/major-changes/major-changes-in-tia-portal-openness-v15.1).

## 7. V21 uyumluluğu ve ortam

**Framework kararı:** Siemens V21 yalnızca .NET Framework 4.8 desteği belirtiyor; eski monolitik Siemens.Engineering.dll modüler assembly'lere ayrılmış. Genel portal/proje erişimi için Siemens.Engineering.Base.dll kullanılıyor. Dolayısıyla upstream'in HintPath'ini V21 klasörüne çevirmek yeterli değil. .NET 8 WPF sürecine V21 assembly'sini doğrudan yüklemek desteklenen çözüm olarak sunulmayacak. [V21 kırıcı değişiklikler](https://docs.tia.siemens.cloud/r/en-us/v21/readme-tia-portal-openness/major-changes-for-long-term-stability-in-tia-portal-openness-v21).

| Kontrol | Bu makinede gözlem |
|---|---|
| İşletim sistemi | Windows 10, build 19045, x64; bu gözlem Siemens OS uyumluluk sertifikası değildir |
| .NET SDK | 8.0.425 ve 10.0.302 |
| WPF runtime | Microsoft.WindowsDesktop.App 8.0 kurulu |
| Visual Studio/MSBuild | `C:\Program Files\Microsoft Visual Studio\18\Community`; MSBuild mevcut |
| TIA | `C:\Program Files\Siemens\Automation\Portal V21` mevcut |
| Openness | `PublicAPI\V21\net48\Siemens.Engineering.Base.dll` ve XML mevcut |
| Assembly identity | `Siemens.Engineering.Base, Version=21.0.0.0, Culture=neutral, PublicKeyToken=29bfe5fdf4ba5d3b` |
| Registry | `HKLM\SOFTWARE\Siemens\Automation\Openness\21.0\PublicAPI\21.0.0.0\net48` gerçek Base DLL yolunu içeriyor |
| net48 hedefleme | `v4.8\mscorlib.dll` mevcut |
| Eski hedefleme | v4.5 referans seti eksik; upstream build v4.5 ve v4.6.1 için MSB3644 verdi |
| Yetki | Siemens TIA Openness grubu var; Get-LocalGroupMember boş döndü, mevcut whoami token'ında Openness eşleşmesi yok. Attach yetkisi doğrulanmadı |
| Aktif portal | Anlık process taramasında mühendislik portalı görülmedi; Siemens yardımcı servisleri vardı |
| Çalışma alanı | Başlangıçta boş; üst dizin bir Git repository değildi. Upstream ayrı Git klonu |

Üyelik ve uygulama erişim izni bağımsız gereksinimlerdir. Openness firewall ilk bağlantıda kullanıcı onayı isteyebilir; exe yolu, değiştirilme zamanı veya hash değişirse AllowList onayı yenilenebilir. Uygulamamız bu istemleri atlatmayacak; host exe'nin izni yönetilecek. [V21 firewall](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/general-functions/tia-portal-openness-firewall).

**Yaşam döngüsü güvenliği:** Yerel `Siemens.Engineering.Base.xml:90169`, TiaPortalProcess.Dispose için süreci kapatma tanımı içeriyor. Siemens'in kendi geliştirme deposunda GetProcesses sonuçlarına Dispose çağrısının V21 canlı instance'ını sonlandırabileceği uyarısı bulunuyor. Bu bir yayıncı geliştirme notudur; gerçek sistemde riskli çağrıyla denenmedi. [Siemens geliştirme notu](https://github.com/siemens/tia-portal-ai-extensions/blob/main/openness_development/skills/session-and-project/SKILL.md).

Uygulamamızın process listesi handle'larında Dispose/using kullanması yasak olacak. `TiaPortal`, `TiaPortalProcess`, `TiaPortalSession` ve `System.Diagnostics.Process` aynı yaşam döngüsüne sahipmiş gibi ele alınmayacak. GetCurrentProcess().Dispose da kullanıcıya ait portal için temizlik kısayolu olmayacak. Attached session'ın güvenli ayrılması ayrı olarak test edilmeden genel Dispose kodu yazılmayacak.

## 8. Önerilen yeni mimari

Bu bölüm öneridir; henüz uygulanmadı.

```text
App — net8.0-windows, WPF, MVVM, dark mode, tray
  ↓ Core servisleri + Infrastructure (JSON, Serilog, IPC client)
  ↓ current-user ACL'li named pipe, sürümlü DTO protokolü
OpennessHost — net48, x64, kullanıcı oturumunda çalışan ayrı exe
  ↓ tek sahipli MTA işlem kuyruğu; instance bazında oturum ve iş durumu
Siemens.Engineering.Base.dll — kurulu V21 net48 dizininden
```

Ek projeler: Contracts (`netstandard2.0`, Siemens tipi içermez), Core (`net8.0`), Infrastructure (`net8.0-windows`), Tests; host tarafında adapter testleri. Serilog her iki runtime ile uyumlu sürüm/sink seçimiyle kullanılacak. Host Windows Service olmayacak: Siemens, Windows Service bağlamından çalışan UI portalına erişimi desteklemiyor. [V21 Windows Service sınırı](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/basics/installation/using-windows-service).

- **Kimlik:** PID tek başına yetmez. PID + OS process başlangıç zamanı + TIA sürümü + oturum nesli kullan; AcquisitionTime'ı process başlangıcı sanma. Proje kimliği/yol değişiminde seçimi askıya al. UI'de yalnız salt okunur DTO tut.
- **Keşif ve bağlantı:** Başlangıçta ve makul periyotta tek keşif; boş projeli instance'ı da göster. Yalnız kullanıcı seçtiği instance'a attach. Registry tabanlı, assembly adı/sürümü doğrulayan resolver; Siemens DLL'lerini paket içine kopyalama.
- **Kuyruk:** İlk sürümde bütün Openness çağrılarını özel MTA worker üzerinde serileştir. Aynı instance/projeye Save ve Archive ortak koordinatör kullanır. Manuel/otomatik istekler aynı kapıdan geçer; sınırsız tick biriktirme. Çoklu instance ilk aşamada seri olabilir; yavaş instance izolasyonu ileride ölçülerek ayrı host/worker ile genişletilebilir.
- **Durum:** Disconnected → Connecting → Connected; işlem için Idle/Saving/Archiving/Uncertain. Disposed event yalnız worker'a olay taşır. Hata bir instance'ı etkilesin; UI/diğer kayıtlar uygulama seviyesinde çökmesin.
- **İptal/timeout:** Belgelenmiş Save/Archive CancellationToken parametresi yok. Token yalnız henüz başlamamış işi iptal eder. Timeout sonuç beklemeyi bıraksa bile gerçek çağrıyı bitirmiş sayılmaz; kilit gerçek tamamlanana kadar tutulur. Sonuç belirsizken tekrar Save/Archive gönderme, host/TIA'yı öldürme.
- **Retry:** Yetki reddini otomatik tekrar döngüsüne sokma. Kapanmış session proxy'sini at ve yeniden keşfet; proje seçimini doğrula. Geçici hatalarda sınırlı backoff. Sonucu bilinmeyen yazma çağrısını körlemesine tekrar etme.
- **Scheduler:** Varsayılan 10 dakika, 1/5/10/15/30/60 ve doğrulanmış özel süre. Süre hesabı monotonic; log zamanı UTC, UI yerel. Başarı sonrası son kayıt zamanı; hata zamanı ayrı. Uyku/uyanma sonrası kaçırılmış tick'leri topluca çalıştırma. Pause yeni iş başlatmayı durdurur, aktif senkron çağrıyı zorla kesmez.
- **Shutdown:** Yeni istekleri durdur, bekleyenleri iptal et, aktif çağrının sonucunu izle. Kullanıcıya ait proje için Close, portal için process Dispose/Kill çağırma. Ayrılma işlemi ayrı testin kabul kriteridir.
- **Ayar/log:** LocalAppData JSON, şema/limit doğrulama, atomik replace ve bozuk dosya yedeği. Serilog rolling file ve retention; correlation ID, PID/oturum nesli, proje, süre, hata türü, başarı/başarısızlık. Gizli erişim bilgileri loglanmaz.

## 9. Backup ve rotation tasarımı

Upstream'de bu özelliklerin kodu yok. Resmi V21 imzası `Project.Archive(DirectoryInfo, string, ProjectArchivationMode)`; kaydedilmiş proje gerektirir ve kaydedilmemiş değişikliklerde EngineeringTargetInvocationException oluşabilir. Compressed modu seçilecek, dosya adı açıkça `.zap21` içerecek. [V21 Archive API](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api/functions-for-projects-and-project-data/archiving-and-retrieving-a-project), [V21 arşiv uzantısı](https://docs.tia.siemens.cloud/r/en-us/v21/editing-projects/archiving-and-retrieving-projects/working-with-project-archives).

Save ve backup programları bağımsızdır; fakat güncel değişikliklerin backup'ı için Save gerekebilir. Kullanıcıya bu koşul açıkça sunulmalı. Aynı uygulama kilidi TIA arayüzündeki kullanıcı düzenlemelerini engellemez: Save ile Archive arasında yeniden değişiklik olursa arşiv başarısız sayılır, eski yedekler korunur. ExclusiveAccess'in UI etkisi ayrıca doğrulanmadan sürekli kilit alınmayacak.

Yedek dizini proje adı + normalize proje yolu hash'i ile ayrılır; aynı adlı projeler karışmaz. Dosya adında UTC/milisaniye veya benzersiz ek, geçersiz karakter temizliği ve tam hedef yol doğrulaması gerekir. Önce erişim/disk kontrolü, sonra uygulamaya ait geçici staging dizininde Archive, ardından dosya varlığı/boyut/ZIP okunabilirliği ve hash/manifest kontrolü yapılır. ZIP kontrolü TIA'da geri yüklenebilirlik garantisi değildir; test projesinde Retrieve ayrıca sınanır.

Yeni arşiv başarılı ve doğrulanmış olmadan rotation çalışmaz. Aynı disk üzerinde son konuma taşıma ve manifestin tutarlı yazımı ardından yalnız uygulamanın ilgili proje manifestinde yönettiği tamamlanmış arşivler silinir. Varsayılan son 10; 5/10/20/sınırsız seçenekleri. Klasördeki yabancı dosyalar, başarısız staging çıktıları ve başka projelerin arşivleri retention hesabına katılmaz. Silme hatası loglanır, yeni yedek başarısı geri alınmaz. Açık proje klasörü File.Copy ile yedeklenmez; SaveAs orijinal proje konumunu değiştirdiği için backup alternatifi değildir.

## 10. Yeniden kullanım ve lisans

Codeberg README satır 1 açıkça **GNU GPL V3** diyor; ayrı LICENSE/COPYING dosyası izlenen ağaçta yok. README'deki sorumluluk reddi bunu MIT/BSD yapmaz. İncelenen bildirimden `or later` izni varsayılmamalı. [Upstream README](https://codeberg.org/Schievel/TIAAutoSaver/src/commit/02379e39ac42759999d897ad7329ba5d9c5818d3/README.md).

GPLv3 kodu değiştirip dağıtmak mümkündür; ilgili bildirimler korunmalı, lisans metni sağlanmalı, değişiklikler belirtilmeli ve kapsanan türev eserin karşılık gelen kaynakları/build betikleri alıcılara GPL koşullarıyla sağlanmalıdır. Ticari satış yasak değildir. Dağıtım olmadan kurum içi kullanım genel bir herkese kaynak yayınlama zorunluluğu doğurmaz. [GPLv3 §§2,4–6](https://www.gnu.org/licenses/gpl-3.0.html).

Önerimiz upstream form, worker, IPC veya kaynak parçasını kopyalamamak. GetProcesses → Attach → Save yaklaşımını resmi API belgesinden yeniden uygulamak; instance seçimi, periyodik keşif ve Undo uyarısı gibi davranışlardan gereksinim olarak yararlanmak. Bu bir clean-room geliştirme iddiası değildir; kaynak incelemesi yapıldı ve kod aktarılmaması seçildi.

| Parça | Karar |
|---|---|
| Çoklu instance seçimi, Undo uyarısı | Ürün gereksinimi olarak yararlan |
| GetProcesses/Attach/Save sırası | Resmi V21 belgesine göre bağımsız yaz |
| Sleep worker ve mutable List paylaşımı | Alma; yeniden tasarla |
| WinForms form, INI, Remoting Starter | Alma; WPF/MVVM, JSON ve named pipe geliştir |
| Kaynak kod/asset/EXE | Yeni ürüne aktarılmadı |

İleride doğrudan GPL kodu alınırsa ürünün dağıtım lisansı ve Siemens'in proprietary assembly'leriyle birleşim koşulları ayrıca değerlendirilmelidir; upstream lisans bildirimi Siemens DLL'lerini yeniden dağıtma izni vermez. Süreç ayırmak tek başına GPL yükümlülüklerini kaldıran garanti değildir. Bu aşamada upstream yalnız araştırma klonudur; yeni ürünün lisansı seçilmedi.

## 11. Doğrulama ve sonraki faza geçiş

Değiştirilmemiş upstream solution için Visual Studio MSBuild ile Debug/x86 Build denendi. **Başarısız:** MSB3644, .NET Framework v4.5 ve v4.6.1 referans assembly setleri bulunamadı. V21'e retarget ederek upstream üzerinde değişiklik yapılmadı. Eski Siemens HintPath ve depoda olmayan imza anahtarı da sonraki olası build engelleridir; mevcut build bunlara ulaşmadı.

Unit test çalıştırılmadı: upstream'de test projesi yok; yeni uygulama yazılmadı. Gerçek V21 bağlantı ve kayıt testleri **yapılmadı**. Build başarısızlığı kaynak analizi raporunu engellemez; çalıştırılabilir V21 uyumluluğunun kanıtı sayılmaz.

FAZ 1 araştırma çıktıları tamamlandı. Sonraki iş, önerilen runtime sınırıyla FAZ 2/3'ün ilk dikey kesitini kurmak ve `V21-TEST-PLANI.md` içindeki salt okunur testten başlamaktır. Manuel Save test projesinde doğrulanmadan scheduler ve backup kodlamasına geçilmemeli.
