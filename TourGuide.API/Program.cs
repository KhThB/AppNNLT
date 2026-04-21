using MongoDB.Driver;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
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
// --- CẤU HÌNH BẢO MẬT JWT ---
var jwtKey = builder.Configuration["Jwt:Key"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true
        };
    });
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });
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
app.UseAuthentication(); // <-- PHẢI THÊM DÒNG NÀY (Nhận diện ai đang vào)
app.UseAuthorization();  // <-- CÓ SẴN (Kiểm tra xem người đó có quyền gì)
app.MapControllers();

app.Run();