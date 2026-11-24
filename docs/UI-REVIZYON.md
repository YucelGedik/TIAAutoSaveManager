# Yerleşim ve tema revizyonu — paketlenmemiş çalışma

Kullanıcı isteği: pencereyi küçültüp büyütürken butonların kesilmesini gider; uygulama uyarılarını mevcut Siemens renklerindeki koyu temayla eşleştir; kullanıcı logosunu bekle ve bu sırada yeni paket paylaşma.

## Değişiklikler

- Tüm sayfa gerektiğinde kaydırılır; işlem butonlarının satırı otomatik boyutlanır, liste yüksekliğiyle çakışmaz. Sağ panel 1080 DIP altında alta geçer, başlıktaki sürüm etiketi de dar pencerede yeni satıra alınır.
- Minimum pencere boyutu 760×520. Küçük ekranlarda kontrollerin karta kesilmesi yerine sayfa kaydırılarak erişim sağlanır. Sağ panelin kendi ayar kaydırması korunur.
- Uygulamaya ait standart MessageBox çağrıları temalı WPF dialog servisiyle değiştirildi. Proje/Undo bilgileri kaydırılıp seçilebilir; turkuaz onay düğmesi, koyu panel ve ortak yazı stilleri kullanılır.
- Onayda varsayılan klavye seçimi **Vazgeç**. Escape ve kapatma düğmesi onay vermez. Modal açıkken yeni zamanlayıcı işlemi başlatmayan mevcut koruma korunur.
- Başlatma hatası ve zaten açık uygulama bildirimi aynı dialog tasarımını kullanır.
- Uygulamanın kendi tepsi bildirimleri, odak çalmayan koyu WPF bildirim kartlarına taşındı. Arka arkaya gelenler biriktirilmez; son bildirim gösterilir, kendiliğinden kapanır.
- Mint arka planlı düğmelerde koyu yazı ve sekmelerde vurgu renginin doğru miras alınması düzeltildi.

## Doğrulama

`TIAAutoSaveManager.UiTests` STA üzerinde kendi WPF ağacını ölçer. Dört pencere boyutu, aynı görsel ağacı küçültüp yeniden büyütme, butonların kart içinde tam kalması, kaydırma erişimi, dialog varsayılanı ve vazgeçildiğinde sıfır API çağrısı kontrol edilir. Bu testler masaüstü/TIA kontrolü veya Save/Archive çağrısı yapmaz.

Görsel önizlemeler `artifacts/ui-review-wide.png`, `ui-review-small.png`, `ui-review-dialog.png` altında; örnek veri içerir ve canlı TIA bağlantısı değildir. Native masaüstü etkileşim testi yerine geçtiği iddia edilmez.

Bu dosya logo öncesi yerleşim çalışmasının kaydıdır; o aşamada publish/ZIP yapılmadı. Sonrasında kullanıcı logosu geldi ve v0.3'e logo, Yardım sekmesi ve kalın ilerleme çubuğu eklendi. Güncel durum için `V03-DURUM.md` dosyasına bakın.
