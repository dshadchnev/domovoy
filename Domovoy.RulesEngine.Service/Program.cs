using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenIddict.Validation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MassTransit;
using Domovoy.RulesEngine.Service.Data;
using Domovoy.RulesEngine.Service.Consumers;

var builder = WebApplication.CreateBuilder(args);

// 1. PostgreSQL
// AddDbContextFactory (Singleton) + AddDbContext (Scoped) combination:
// Consumer (MassTransit Singleton) receives IDbContextFactory, Controller receives RulesEngineDbContext.
var connStr = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContextFactory<RulesEngineDbContext>(
    opts => opts.UseNpgsql(connStr));
builder.Services.AddDbContext<RulesEngineDbContext>(
    opts => opts.UseNpgsql(connStr));

// 2. OpenIddict Validation - introspection via Auth Service (for JWE/OpenIddict tokens)
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(builder.Configuration["OpenIddict:Issuer"]);
        options.UseIntrospection()
               .SetClientId(builder.Configuration["OpenIddict:ClientId"])
               .SetClientSecret(builder.Configuration["OpenIddict:ClientSecret"]);
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

// 3. Authentication: Policy Scheme - accepts both OpenIddict JWE and plain HS256 JWT
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
            "DomovoyClients"
        },
        IssuerSigningKey = jwtSecret != null
            ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            : null
    };
});

builder.Services.AddAuthorization();

// 3. MassTransit v8+ 
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TelemetryRuleEvaluator>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:User"]);
            h.Password(builder.Configuration["RabbitMQ:Pass"]);
        });

        cfg.ConfigureEndpoints(context);

        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
    });
});

// 4. HealthChecks
builder.Services.AddHealthChecks();

builder.Services.AddControllers();

// 5. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new OpenApiInfo { Title = "Domovoy RulesEngine Service", Version = "v1" });
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RulesEngineDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// HealthChecks 
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = _ => false });

// API 
app.MapControllers();

app.Run();
