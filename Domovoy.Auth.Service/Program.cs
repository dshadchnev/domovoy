using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Domovoy.Auth.Service;
using Domovoy.Auth.Service.Data;
using Domovoy.Auth.Service.Services;
using Domovoy.Auth.Service.Data.Entities;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"] ?? "/var/domovoy/dataprotection";
var certificatePath = builder.Configuration["Certificates:Path"] ?? "/var/domovoy/certs/openiddict.pfx";
var certificatePassword = builder.Configuration["Certificates:Password"];
var certificateSubject = builder.Configuration["Certificates:Subject"] ?? "CN=Domovoy Auth";

X509Certificate2? serviceCertificate = null;

var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName("Domovoy.Auth.Service")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

// 🔑 1. EF Core + PostgreSQL
builder.Services.AddDbContext<AuthDbContext>(opts =>
{
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
    opts.UseOpenIddict<Guid>();
});

// 🔑 2. Identity + OpenIddict
builder.Services.AddIdentity<AuthUser, AuthRole>(opts =>
{
    opts.Password.RequireNonAlphanumeric = false;
    opts.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AuthDbContext>()
.AddDefaultTokenProviders();

if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(certificatePassword))
    {
        throw new InvalidOperationException("Certificates:Password must be configured in Production.");
    }

    serviceCertificate = LoadOrCreateCertificate(certificatePath, certificatePassword, certificateSubject);
    dataProtectionBuilder.ProtectKeysWithCertificate(serviceCertificate);
}

builder.Services.AddOpenIddict()
    .AddCore(opts =>
    {
        opts.UseEntityFrameworkCore()
            .UseDbContext<AuthDbContext>()
            .ReplaceDefaultEntities<Guid>();
    })
    .AddServer(opts =>
    {
        opts.SetTokenEndpointUris("/connect/token")
            .AllowPasswordFlow()
            .AllowRefreshTokenFlow();

        if (builder.Environment.IsProduction())
        {
            opts.AddEncryptionCertificate(serviceCertificate!)
                .AddSigningCertificate(serviceCertificate!);
        }
        else
        {
            opts.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
        }

        opts.UseAspNetCore()
            .EnableTokenEndpointPassthrough()
            .DisableTransportSecurityRequirement();
    })
    .AddValidation(opts =>
    {
        opts.UseLocalServer();
        opts.UseAspNetCore();
    });

builder.Services.AddScoped<OpenIddictServerEventHandlers>();

// 🔑 3. JWT Authentication (если другие сервисы будут валидировать токены)
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts => 
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// 🔑 4. MassTransit (RabbitMQ)
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:User"] ?? "admin");
            h.Password(builder.Configuration["RabbitMQ:Pass"] ?? "admin");
        });
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        cfg.ConfigureEndpoints(ctx);
    });
});

// 🔑 5. Сервисы (только те, что реально существуют)
builder.Services.AddScoped<IUserAuthService, UserAuthService>();
builder.Services.AddScoped<IDeviceAuthService, DeviceAuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddHostedService<ClientRegistrationWorker>();
// builder.Services.AddHostedService<TokenCleanupWorker>(); // Раскомментируйте, когда создадите класс

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Domovoy Auth Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 🔑 Apply pending database migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    try
    {
        db.Database.Migrate();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("✅ Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ Error applying database migrations");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();

static X509Certificate2 LoadOrCreateCertificate(string certificatePath, string certificatePassword, string certificateSubject)
{
    var certificateDirectory = Path.GetDirectoryName(certificatePath);

    if (!string.IsNullOrWhiteSpace(certificateDirectory))
    {
        Directory.CreateDirectory(certificateDirectory);
    }

    if (File.Exists(certificatePath))
    {
        return new X509Certificate2(
            certificatePath,
            certificatePassword,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
    }

    var subjectName = certificateSubject.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)
        ? certificateSubject
        : $"CN={certificateSubject}";

    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest(
        new X500DistinguishedName(subjectName),
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
    request.CertificateExtensions.Add(
        new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            false));
    request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

    using var certificate = request.CreateSelfSigned(
        DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddYears(5));

    var pfxBytes = certificate.Export(X509ContentType.Pfx, certificatePassword);
    File.WriteAllBytes(certificatePath, pfxBytes);

    return new X509Certificate2(
        pfxBytes,
        certificatePassword,
        X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
}
