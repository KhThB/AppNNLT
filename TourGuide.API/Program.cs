using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using TourGuide.API.Infrastructure.Auth;
using TourGuide.API.Infrastructure.Mongo;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Services.Abstractions;
using TourGuide.API.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AdminBootstrapOptions>(builder.Configuration.GetSection("AdminBootstrap"));
builder.Services.Configure<TranslationProviderOptions>(builder.Configuration.GetSection("Translation"));
builder.Services.Configure<ModerationProviderOptions>(builder.Configuration.GetSection("Moderation"));
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection("Payment"));

var mongoSettings = builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>() ?? new MongoDbSettings();
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
builder.Services.AddScoped(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoSettings.DatabaseName));
builder.Services.AddScoped<MongoCollections>();
builder.Services.AddScoped<MongoIndexInitializer>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<PresenceTracker>();

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpClient<GoogleCloudTranslationProvider>();
builder.Services.AddHttpClient<MyMemoryTranslationProvider>();
builder.Services.AddScoped<ITranslationProvider, CompositeTranslationProvider>();
builder.Services.AddScoped<ITranslationService, TranslationService>();
builder.Services.AddScoped<IModerationService, ModerationService>();
builder.Services.AddScoped<IPoiService, PoiService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IImageStorageService, CloudinaryImageStorageService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddHttpClient<PayOsPaymentGateway>();
builder.Services.AddScoped<IPaymentGateway>(sp => sp.GetRequiredService<PayOsPaymentGateway>());

builder.Services.AddCors(options =>
{
    options.AddPolicy("Portal", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sessionId = context.Principal?.FindFirstValue("sessionId");
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    context.Fail("Missing session.");
                    return;
                }

                var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                if (!await authService.IsSessionActiveAsync(sessionId, context.HttpContext.RequestAborted))
                {
                    context.Fail("Session is no longer active.");
                }
            },
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "TourGuide API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var statusCode = exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var details = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status500InternalServerError ? "Unexpected server error." : exception?.Message,
            Detail = app.Environment.IsDevelopment() ? exception?.ToString() : null,
            Instance = context.Request.Path,
        };

        await context.Response.WriteAsJsonAsync(details);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("Portal");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    var indexInitializer = scope.ServiceProvider.GetRequiredService<MongoIndexInitializer>();
    await indexInitializer.InitializeAsync();
    await authService.EnsureBootstrapAdminAsync();
}

app.Run();
