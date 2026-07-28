// Gezinmeden sonra odağı yeni sayfanın başlığına taşır.
//
// Neden Blazor'ın <FocusOnNavigate> komponenti değil: o komponent odağı *ilk
// yüklemede de* h1'e alıyordu. Odak h1'e oturunca DOM'da h1'den önce gelen her
// şey — "İçeriğe atla" bağlantısı ve navbar'ın tamamı — ileri Tab ile
// erişilemez hale geliyordu; klavye kullanıcısı menüye ancak Shift+Tab ile
// ulaşabiliyordu. Faz 6.3'te eklenen atlama bağlantısı bu yüzden hiç
// çalışmıyordu.
//
// 'enhancedload' yalnızca enhanced navigation güncellemelerinde tetiklenir,
// ilk sayfa yüklemesinde tetiklenmez. Böylece ilk yüklemede odak belgenin
// başında kalır ve sekme sırası doğal olur; gezinmede ise ekran okuyucu yeni
// sayfayı duyurabilsin diye odak başlığa gider.
(function () {
    'use strict';

    var lastPath = location.pathname;

    function focusHeading() {
        // Aynı yoldaki tetiklenmeler gezinme değil, streaming render
        // güncellemesidir; kullanıcı bir şeye odaklanmışken odağı çalmayalım.
        if (location.pathname === lastPath) {
            return;
        }
        lastPath = location.pathname;

        // 'enhancedload' DOM yamasından sonra tetikleniyor ama son yama değil:
        // interaktif komponentler devreye girerken h1 düğümü değiştirilebiliyor
        // ve odaklanmış düğüm koparsa odak body'ye düşüyor. Bu yüzden kısa bir
        // pencerede birkaç kez deneniyor; başlığa oturunca duruluyor.
        [0, 50, 200].forEach(function (gecikme) {
            setTimeout(function () {
                var heading = document.querySelector('h1');
                if (!heading || document.activeElement === heading) {
                    return;
                }

                // Başlıklar doğal olarak odaklanabilir değil; sekme sırasına da
                // girmesinler diye -1.
                if (!heading.hasAttribute('tabindex')) {
                    heading.setAttribute('tabindex', '-1');
                }

                heading.focus();
            }, gecikme);
        });
    }

    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancedload', focusHeading);
    }
})();
