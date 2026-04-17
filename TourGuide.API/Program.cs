using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// 1. CẤU HÌNH MONGODB
var connectionString = builder.Configuration["MongoDbSettings:ConnectionString"];
var databaseName = builder.Configuration["MongoDbSettings:DatabaseName"];

builder.Services.AddSingleton<IMongoClient>(new MongoClient(connectionString));
builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(databaseName);
});

// 2. CẤU HÌNH CORS (Mở cửa cho WebAdmin & WebQR)
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", policy => {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// 3. ĐĂNG KÝ CONTROLLER & SWAGGER
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- THỨ TỰ PIPELINE QUAN TRỌNG ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// CORS phải gọi trước StaticFiles và Controllers
app.UseCors("AllowAll");

app.UseStaticFiles(); // Mở khóa thư mục chứa ảnh
app.UseAuthorization();
app.MapControllers();

app.Run();