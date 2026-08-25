using Domovoy.DeviceManager.Service.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MassTransit;
using OpenIddict.Validation.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. PostgreSQL
builder.Services.AddDbContext<DeviceManagerDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// 2. OpenIddict Validation - introspection via Auth Service (for JWE/OpenIddict tokens)
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(builder.Configuration["OpenIddict:Issuer"]
            ?? "http://localhost:8086/");
        options.UseIntrospection()
            .SetClientId(builder.Configuration["OpenIddict:ClientId"] ?? "domovoy-device-manager")
            .SetClientSecret(builder.Configuration["OpenIddict:ClientSecret"] ?? "device-manager-secret");
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

// 3. Authentication: Policy Scheme - accepts both OpenIddict JWE and plain HS256 JWT
//    Scheme choice based on token structure: 3 parts (xxx.yyy.zzz) = JWT, otherwise = OpenIddict JWE
var jwtSecret = builder.Configuration["Jwt:Secret"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "SmartBearer";
    options.DefaultChallengeScheme = "SmartBearer";
})
.AddPolicyScheme("SmartBearer", "JWT or OpenIddict", options =>
{
    options.ForwardDefaultSelector = ctx =>
    {
        var auth = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
        // Plain JWT = 3 parts (header.payload.sig)
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = auth["Bearer ".Length..].Trim();
            if (token.Split('.').Length == 3)
                return JwtBearerDefaults.AuthenticationScheme;
        }
        return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    };
})
.AddJwtBearer(opts =>
{
    opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "domovoy",
        ValidAudiences = new[]
        {
            builder.Configuration["Jwt:Audience"] ?? "domovoy-users",
            "DomovoyClients"   // fallback for Docker configurations
        },
        IssuerSigningKey = jwtSecret != null
            ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            : null
    };
});

builder.Services.AddAuthorization();

// 4. MassTransit + RabbitMQ
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

// 5. MVC + Health Checks
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

// 6. Swagger
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

    opts.AddSecurityRequirement(new OpenApiSecurityRequirement
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

// Ensure DB schema initialized
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
    Predicate = _ => false
});
app.Run();
