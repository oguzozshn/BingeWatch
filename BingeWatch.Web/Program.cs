using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using BingeWatch.Web.Components;
using BingeWatch.Web.Models;
using Serilog;
using Microsoft.AspNetCore.HttpOverrides;
using BingeWatch.Web.Seo;

var builder = WebApplication.CreateBuilder(args);

// Serilog — API ile aynı kurulum; konteynerde loglar stdout'a gidiyor.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Razor Components (Blazor Server / Interactive Server)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API için Named HttpClient. Adres yapılandırmadan geliyor: konteynerde API
// "localhost" değil compose ağındaki servis adı üzerinden görünüyor
// (Api__BaseUrl ortam değişkeni). Yerelde appsettings'teki varsayılan geçerli.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Api:BaseUrl yapılandırılmamış. appsettings.json ya da Api__BaseUrl ortam değişkeniyle verilmeli.");

builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
    );
});

// Tarayıcı oturumu: cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Ters vekil arkasında şema ve istemci IP'si başlıklardan gelir. Bunlar
// okunmazsa üretilen mutlak bağlantılar http:// olur ve cookie'nin Secure
// bayrağı yanlış değerlendirilir. Zincirde tek vekil varsayılıyor.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Konteynerde TLS ters vekilde sonlanıyor ve uygulama yalnızca HTTP dinliyor;
// böyle bir kurulumda HTTPS'e yönlendirmek var olmayan bir porta yönlendirmek
// demek. Yerelde açık kalsın diye varsayılan true.
if (builder.Configuration.GetValue("EnableHttpsRedirection", true))
    app.UseHttpsRedirection();

// Blazor Server tarafında sınanacak bir bağımlılık yok: API'ye ulaşamamak
// sayfaları boş bırakır ama süreci yeniden başlatmak bunu düzeltmez.
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
   .AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();

// Antiforgery Blazor Server için gerekli
app.UseAntiforgery();

// Login/Register/Logout: API'yi çağırıp aldığı JWT'yi cookie'nin claim'i olarak saklar.
app.MapPost("/account/login", async (HttpContext http, IHttpClientFactory factory) =>
{
    var form = await http.Request.ReadFormAsync();
    var usernameOrEmail = form["usernameOrEmail"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var client = factory.CreateClient("ApiClient");
    var response = await client.PostAsJsonAsync("api/auth/login", new { usernameOrEmail, password });
    if (!response.IsSuccessStatusCode)
        return Results.Redirect("/login?error=1");

    var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
    if (auth is null)
        return Results.Redirect("/login?error=1");

    await SignInAsync(http, auth);
    return Results.Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
});

app.MapPost("/account/register", async (HttpContext http, IHttpClientFactory factory) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var displayName = form["displayName"].ToString();

    var client = factory.CreateClient("ApiClient");
    var response = await client.PostAsJsonAsync("api/auth/register", new { username, email, password, displayName });
    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return Results.Redirect("/register?error=" + Uri.EscapeDataString(error));
    }

    var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
    if (auth is null)
        return Results.Redirect("/register?error=1");

    await SignInAsync(http, auth);
    return Results.Redirect("/");
});

// Şifre sıfırlama. Token API'de üretiliyor; Web yalnızca formu taşıyor.
app.MapPost("/account/forgot-password", async (HttpContext http, IHttpClientFactory factory) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString();

    // Kullanıcının tıklayacağı sayfa Web'de; adresi isteğin kendisinden
    // üretiyoruz ki yerel/staging/üretim aynı yapılandırmayı paylaşabilsin.
    var resetUrlBase = $"{http.Request.Scheme}://{http.Request.Host}/reset-password";

    var client = factory.CreateClient("ApiClient");
    var response = await client.PostAsJsonAsync("api/auth/forgot-password",
        new { email, resetUrlBase });

    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        return Results.Redirect("/forgot-password?unavailable=1");

    // Hesap var mı yok mu ayrımı yapılmıyor: her durumda aynı ekran.
    return Results.Redirect("/forgot-password?sent=1");
});

app.MapPost("/account/reset-password", async (HttpContext http, IHttpClientFactory factory) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var token = form["token"].ToString();
    var password = form["password"].ToString();

    var client = factory.CreateClient("ApiClient");
    var response = await client.PostAsJsonAsync("api/auth/reset-password",
        new { email, token, newPassword = password });

    if (response.IsSuccessStatusCode)
        return Results.Redirect("/login?reset=1");

    var problem = await response.Content.ReadFromJsonAsync<ApiMessage>();
    var message = string.IsNullOrWhiteSpace(problem?.Message)
        ? "Şifre güncellenemedi."
        : problem.Message;

    // Token ve e-posta geri konuyor ki kullanıcı formu baştan doldurmasın.
    return Results.Redirect(
        $"/reset-password?email={Uri.EscapeDataString(email)}" +
        $"&token={Uri.EscapeDataString(token)}" +
        $"&error={Uri.EscapeDataString(message)}");
});

app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

// robots.txt ve sitemap.xml — katalogdan üretiliyor, statik dosya değil.
app.MapSeoEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task SignInAsync(HttpContext http, AuthResponse auth)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, auth.UserId),
        new(ClaimTypes.Name, auth.Username),
        new("display_name", auth.DisplayName),
        new("api_token", auth.Token),
    };

    // Roller cookie'ye de yazılır; <AuthorizeView Roles="Admin"> sunucuya sormadan çalışsın.
    claims.AddRange(auth.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
}

/// <summary>API'nin hata gövdelerindeki tek alanlı mesaj zarfı.</summary>
internal sealed class ApiMessage
{
    public string? Message { get; set; }
}
