namespace BingeWatch.Web
{
    /// <summary>
    /// ARIA durum niteliklerini yazmak için yardımcı.
    /// </summary>
    /// <remarks>
    /// Blazor bir <c>bool</c> nitelik değerini HTML boolean niteliği gibi ele
    /// alır: <c>true</c> için niteliği boş değerle yazar
    /// (<c>aria-selected=""</c>), <c>false</c> için hiç yazmaz. HTML'in kendi
    /// boolean nitelikleri (<c>disabled</c>, <c>checked</c>) için doğru olan bu
    /// davranış ARIA'da yanlış: <c>aria-selected</c>, <c>aria-checked</c>,
    /// <c>aria-expanded</c> birebir <c>"true"</c> ya da <c>"false"</c> metnini
    /// bekler. Boş değer geçersizdir ve niteliğin hiç olmaması "belirtilmemiş"
    /// demektir — ekran okuyucu seçili sekmeyi ya da verilen puanı bildirmez.
    /// <para>
    /// Faz 6.3'te eklenen ARIA desenleri bu yüzden sessizce etkisizdi; Faz
    /// 6.6'daki E2E testleri <c>aria-selected=""</c> çıktısını yakalayınca
    /// ortaya çıktı.
    /// </para>
    /// </remarks>
    public static class AriaAttribute
    {
        public static string Aria(bool value) => value ? "true" : "false";
    }
}
