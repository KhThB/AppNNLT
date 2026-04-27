using Blazored.LocalStorage;
using MudBlazor.Services;
using TourGuide.WebAdmin.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => { options.DetailedErrors = true; });
builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
var backendBaseUrl = builder.Configuration["Backend:BaseUrl"]
    ?? throw new InvalidOperationException("Missing Backend:BaseUrl configuration.");
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(backendBaseUrl),
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
