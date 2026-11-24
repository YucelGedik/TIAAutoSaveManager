# Third-party notices

Yeni uygulama TIAAutoSaver kaynak kodu/asset'i içermez. Araştırma klonu ürün paketine dahil edilmez. Siemens assembly'leri paketlenmez; kullanıcının lisanslı yerel V21 kurulumundan yüklenir.

Runtime paketleri:

| Paket | Sürüm | Lisans / yayıncı |
|---|---|---|
| Serilog | 4.3.0 | Apache-2.0, Serilog Contributors |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0, Serilog Contributors |
| System.Diagnostics.DiagnosticSource | 8.0.1 | MIT, Microsoft/.NET contributors |
| System.Threading.Channels | 8.0.0 | MIT, Microsoft/.NET contributors |
| System.Threading.Tasks.Extensions | 4.5.4 | MIT, Microsoft/.NET contributors |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 | MIT, Microsoft/.NET contributors |
| System.Memory | 4.5.5 | MIT, Microsoft/.NET contributors |
| System.Buffers | 4.5.1 | MIT, Microsoft/.NET contributors |
| System.Numerics.Vectors | 4.5.0 | MIT, Microsoft/.NET contributors |

Nihai kullanılan sürümler build çıktısının `.deps.json` dosyası ve restore `project.assets.json` kayıtlarındadır. `licenses` dizininde Apache-2.0 ve Microsoft paketlerinin lisans/üçüncü taraf bildirimleri bulunur. Paketler üzerinde kaynak değişikliği yapılmadı.

Test araçları (ürünle gönderilmez): NUnit 4.3.2 (MIT), NUnit3TestAdapter 5.0.0 (MIT), Microsoft.NET.Test.Sdk 17.14.1 ve test platformu (MIT).

Setup dağıtımı: Inno Setup 6.7.3, Copyright © 1997–2026 Jordan Russell; portions Copyright © 2000–2026 Martijn Laan. Değiştirilmemiş kurulum motoru kullanılır. Lisans: https://github.com/jrsoftware/issrc/blob/is-6_7_3/license.txt ; https://jrsoftware.org/ . Derleyici ürünle gönderilmez.

Çevrimdışı Setup ayrıca Microsoft'un değiştirilmemiş, dijital imzalı .NET 8.0.34 Windows Desktop Runtime x64 ve .NET Framework 4.8 kurucularını içerir. Microsoft bileşenleri kendi lisanslarıyla dağıtılır; kurucular lisans/üçüncü taraf bildirimlerini korur. https://dotnet.microsoft.com/dotnet/terms ; https://dotnet.microsoft.com/download/dotnet-framework/net48 . Kaynak URL'ler, sürümler ve hash'ler `installer/dependencies.json` ile setup build manifestinde kayıtlıdır.

Kaynaklar: https://github.com/serilog/serilog ; https://github.com/serilog/serilog-sinks-file ; https://github.com/dotnet/runtime ; https://github.com/nunit/nunit . Yeni uygulamanın kendi dağıtım lisansı henüz seçilmedi; bu dosya üçüncü taraf lisanslarını değiştirmez.
