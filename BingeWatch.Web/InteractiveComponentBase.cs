using Microsoft.AspNetCore.Components;

namespace BingeWatch.Web
{
    /// <summary>
    /// Prerender edilmiş HTML, SignalR devresi bağlanana kadar ölüdür: o
    /// aralıkta yapılan tıklamalar sessizce kaybolur. Kullanıcı butona basar,
    /// hiçbir şey olmaz ve neden olmadığını anlamaz.
    ///
    /// Bu taban, ilk gerçek render'ı (yani devrenin kurulduğu anı) yakalayıp
    /// <see cref="Interactive"/> bayrağını kaldırıyor. Bileşenler kontrollerini
    /// <c>disabled="@(!Interactive)"</c> ile bağladığında buton kısa bir an
    /// devre dışı görünür ve tıklama yutulmaz.
    ///
    /// <para>
    /// E2E süiti bunu <c>WaitForInteractiveTabsAsync</c> ile elle bekliyordu;
    /// gerçek kullanıcıya aynı koruma verilmemişti (bkz. ROADMAP §7.2).
    /// </para>
    /// </summary>
    public abstract class InteractiveComponentBase : ComponentBase
    {
        /// <summary>Devre kuruldu mu? Prerender sırasında <c>false</c>.</summary>
        protected bool Interactive { get; private set; }

        protected override void OnAfterRender(bool firstRender)
        {
            if (!firstRender)
                return;

            Interactive = true;
            // İlk render'dan sonra çağrılıyor; bayrağın ekrana yansıması için
            // ayrıca bir render turu gerekiyor.
            StateHasChanged();
        }
    }
}
