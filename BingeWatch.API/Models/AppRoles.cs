namespace BingeWatch.API.Models
{
    /// <summary>
    /// Identity rolleri. Tek rol var: moderasyon paneline erişen moderatör.
    /// Rol atamaları <c>Admin:Usernames</c> yapılandırmasından açılışta yapılır;
    /// uygulamadan rol veren bir uç bilerek yok.
    /// </summary>
    public static class AppRoles
    {
        public const string Admin = "Admin";
    }
}
