using MudBlazor.Services;
using TourGuide.WebQR.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents();

// Loại bỏ MudBlazor vì chúng ta dùng HTML tĩnh hoàn toàn
// builder.Services.AddMudServices();

// KẾT NỐI API: Cấu hình HttpClient trỏ về Backend Local
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7095/")
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

app.MapRazorComponents<App>();

app.Run();