using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using BingeWatch.Web.Components;
using BingeWatch.Web.Models;
using Serilog;
using Microsoft.AspNetCore.HttpOverrides;

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

app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

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
