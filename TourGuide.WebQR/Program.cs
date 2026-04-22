using MudBlazor.Services;
using TourGuide.WebQR.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Khởi tạo MudBlazor
builder.Services.AddMudServices();

// KẾT NỐI API: Cấu hình HttpClient trỏ về Backend trên Render
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://appnnlt.onrender.com/")
});

var app = builder.Build();

// Configure the HTTP request pipeline.
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