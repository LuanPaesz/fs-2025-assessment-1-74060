using DublinBikes.Blazor;
using DublinBikes.Blazor.Components;
using DublinBikes.Blazor.Configuration;
using DublinBikes.Blazor.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Blazor .NET 8 (Razor Components) ---
// Registra os componentes Razor e habilita modo interativo no servidor
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// --- Configuração da API (appsettings.json -> "Api": { "BaseUrl": ... } ) ---
builder.Services.Configure<ApiOptions>(
    builder.Configuration.GetSection("Api"));

// --- HttpClient tipado para chamar a nossa DublinBikes API V2 ---
builder.Services.AddHttpClient<StationsApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<ApiOptions>>().Value;

    if (string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        throw new InvalidOperationException("Api:BaseUrl is not configured in appsettings.json");
    }

    client.BaseAddress = new Uri(options.BaseUrl);
});

var app = builder.Build();

// --- Pipeline padrão do template novo ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Mapeia o componente raiz App.razor (geralmente em Components/App.razor)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
