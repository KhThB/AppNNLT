var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles(); // Tự động tìm file index.html
app.UseStaticFiles();  // Mở khóa thư mục wwwroot chứa HTML/CSS/JS

app.Run();