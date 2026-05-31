using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Domovoy.Auth.Service.Data;
using Domovoy.Auth.Service.Data.Entities;
using Domovoy.Auth.Service.Services;
using MassTransit;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Note: не переключаем среду принудительно — используем реальный ASPNETCORE_ENVIRONMENT

// 🔑 1. DataProtection (кроссплатформенный путь: работает и в Windows, и в Linux/Docker)
var dpKeysPath = Path.Combine(Path.GetTempPath(), "domovoy-dataprotection");
builder.Services.AddDataProtection()
    .SetApplicationName("Domovoy.Auth.Service")
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

// 🔑 2. EF Core + PostgreSQL
builder.Services.AddDbContext<AuthDbContext>(opts =>
{
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
    opts.UseOpenIddict<Guid>();
});

// 🔑 3. Identity + OpenIddict
builder.Services.AddIdentity<AuthUser, AuthRole>(opts =>
{
    opts.Password.RequireNonAlphanumeric = false;
    opts.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AuthDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddOpenIddict()
    .AddCore(opts =>
    {
        opts.UseEntityFrameworkCore()
            .UseDbContext<AuthDbContext>()
            .ReplaceDefaultEntities<Guid>();
    })
    .AddServer(opts =>
    {
        opts.SetIssuer(new Uri(builder.Configuration["OpenIddict:Issuer"] ?? "http://localhost:8086"));
        opts.SetTokenEndpointUris("/connect/token")
            .AllowPasswordFlow()
            .AllowRefreshTokenFlow();
        opts.SetIntrospectionEndpointUris("/connect/introspect");

        // В Docker/Dev используем встроенные тестовые сертификаты (без файлов/паролей)
        opts.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        opts.UseAspNetCore()
            .EnableTokenEndpointPassthrough()
            .DisableTransportSecurityRequirement();
    })
    .AddValidation(opts =>
    {
        opts.UseLocalServer();
        opts.UseAspNetCore();
    });

// 🔑 4. JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "domovoy",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization(options =>
{
    var policyBuilder = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
        JwtBearerDefaults.AuthenticationScheme,
        OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
    options.DefaultPolicy = policyBuilder.RequireAuthenticatedUser().Build();
});

// 🗄 5. Redis
var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddSingleton<IDatabase>(
    sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

// 🐇 6. MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TelemetryConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq";
        var user = builder.Configuration["RabbitMQ:User"] ?? "admin";
        var pass = builder.Configuration["RabbitMQ:Pass"] ?? "admin";
        cfg.Host(host, "/", h => { h.Username(user); h.Password(pass); });
        cfg.ConfigureEndpoints(context);
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
    });
});
// AddMassTransitHostedService() удален: в v8+ он регистрируется автоматически

// 🛠 6. Регистрация сервисов
builder.Services.AddScoped<IUserAuthService, UserAuthService>();
builder.Services.AddScoped<IDeviceAuthService, DeviceAuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddHostedService<ClientRegistrationWorker>();

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Domovoy Auth Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer Token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

// 💾 Безопасное применение миграций
using (var scope = app.Services.CreateScope())
{
    try
    {
        scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.Migrate();
        app.Logger.LogInformation("✅ Migrations applied");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "❌ Migration failed");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
    // Перенаправление с корня на Swagger
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

try
{
    Console.WriteLine("🚀 Starting Domovoy Auth Service...");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"💥 FATAL STARTUP CRASH: {ex.GetType().Name}");
    Console.WriteLine($"📜 Message: {ex.Message}");
    Console.WriteLine($"🔍 Inner: {ex.InnerException?.Message}");
    Console.WriteLine($"📑 Stack: {ex.StackTrace}");
    throw; // Останавливаем контейнер, чтобы увидеть лог
}