using TourGuide.WebAdmin.Components;
using MudBlazor.Services;
using TourGuide.Domain.Models;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// Thêm 2 dòng cấu hình này TRƯỚC dòng builder.Build()
builder.Services.AddMudServices();
// Nhớ thay đổi cổng 7095 thành cổng mà API (Swagger) của bạn đang chạy
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7095/") });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
