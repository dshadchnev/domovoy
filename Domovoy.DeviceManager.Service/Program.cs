using Domovoy.DeviceManager.Service.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using MassTransit;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. PostgreSQL
builder.Services.AddDbContext<DeviceManagerDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// 2. OpenIddict Validation — introspection через Auth Service (поддерживает JWE-токены)
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        // URL Auth Service внутри Docker / локально
        options.SetIssuer(builder.Configuration["OpenIddict:Issuer"]
            ?? "http://localhost:8086/");
        options.UseIntrospection()
            .SetClientId(builder.Configuration["OpenIddict:ClientId"] ?? "domovoy-device-manager")
            .SetClientSecret(builder.Configuration["OpenIddict:ClientSecret"] ?? "device-manager-secret");
        // Использовать ASP.NET Core Data Protection для расшифровки токенов (опционально)
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

// 3. MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:User"] ?? string.Empty);
            h.Password(builder.Configuration["RabbitMQ:Pass"] ?? string.Empty);
        });
        cfg.ConfigureEndpoints(context);
    });
});

// 4. MVC Controllers
builder.Services.AddControllers();

// 5. Health Checks (встроено в ASP.NET Core 8 SDK)
builder.Services.AddHealthChecks();

// 5. Swagger / OpenAPI (Swashbuckle 10.x + Microsoft.OpenApi 2.x)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new OpenApiInfo { Title = "Domovoy Device Manager API", Version = "v1" });

    opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization. Enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // Microsoft.OpenApi 2.x: OpenApiSecurityRequirement использует List<string>
    opts.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

var app = builder.Build();

// Инициализация схемы БД
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DeviceManagerDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions 
{ 
    Predicate = _ => false // Для readiness probe (опционально)
});
app.Run();