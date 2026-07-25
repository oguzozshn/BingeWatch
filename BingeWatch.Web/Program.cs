using BingeWatch.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Razor Components (Blazor Server / Interactive Server)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 🔑 API için Named HttpClient (EN DOĞRUSU)d
builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5054/");
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
    );
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// Antiforgery Blazor Server için gerekli
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
