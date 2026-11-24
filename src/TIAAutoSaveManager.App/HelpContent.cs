using System;
using System.Collections.Generic;

namespace TIAAutoSaveManager.App;

public sealed record HelpStep(string Number, string Title, string Description, string ImageUri);

public static class HelpContent
{
    private const string Root = "pack://application:,,,/TIAAutoSaveManager.App;component/Assets/Help/";
    public static IReadOnlyList<HelpStep> UserNameSteps { get; } = new[]
    {
        new HelpStep("1", "Başlat → kullanıcı simgesi", "Başlat menüsünün solundaki kullanıcı simgesine gelin. Örnekte görünen ad utente; kendi hesabınızdaki adı kontrol edin.", Root + "05-start-user-name.png"),
        new HelpStep("2", "Hesap ayarlarını değiştir", "Kullanıcı simgesine tıklayıp ‘Change account settings / Hesap ayarlarını değiştir’ seçin.", Root + "06-account-settings.png"),
        new HelpStep("3", "Hesaplar → Bilgileriniz", "‘Your info / Bilgileriniz’ sayfasında hesap adını görebilirsiniz. Örnekteki UTENTE adı bu bilgisayara aittir.", Root + "07-your-info.png")
    };
    public static IReadOnlyList<HelpStep> SetupSteps { get; } = new[]
    {
        new HelpStep("01", "Bilgisayar Yönetimi’ni yönetici olarak açın",
            "Bu işlemi TIA Portal’ın çalıştığı bilgisayarda yapın. Başlat menüsünde ‘Bilgisayar Yönetimi’ arayın, sağ tıklayıp ‘Yönetici olarak çalıştır’ seçin. Yönetici yetkiniz yoksa IT sorumlusundan yardım isteyin.", Root + "01-computer-management.png"),
        new HelpStep("02", "Siemens TIA Openness grubunu bulun",
            "Soldan Sistem Araçları → Yerel Kullanıcılar ve Gruplar → Gruplar yolunu açın. Listede ‘Siemens TIA Openness’ grubuna çift tıklayın. Grup veya bu bölüm görünmüyorsa Openness kurulumunu sistem yöneticinizle kontrol edin.", Root + "02-openness-group.png"),
        new HelpStep("03", "Kullanıcı ekleme penceresini açın",
            "Grup özelliklerinde ‘Ekle…’ düğmesine basın. Üyeler listesinde TIA Portal’ı çalıştırırken kullandığınız hesabın bulunması gerekir. Örnekteki ‘utente’ adı yalnız o bilgisayara aittir; kendi hesabınızı kullanın.", Root + "03-add-user.png"),
        new HelpStep("04", "Hesabınızı doğrulayın ve değişikliği uygulayın",
            "‘Seçilecek nesne adlarını girin’ alanına kendi Windows kullanıcı adınızı yazın. Ekrandaki ‘PC_KULLANICI_ADINI_GIRINIZ’ bir yer tutucudur. ‘Adları Denetle’ ile hesabı doğrulayın, ardından Tamam → Uygula → Tamam seçin. Gerekirse Konumlar alanından doğru bilgisayar veya etki alanını seçin.", Root + "04-check-user-name.png")
    };
}
