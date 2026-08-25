using System.Text;
using Domovoy.Notification.Service.Data;
using Domovoy.Notification.Service.Consumers;
using Domovoy.Notification.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MassTransit;
using OpenIddict.Validation.AspNetCore;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// 1. PostgreSQL
builder.Services.AddDbContextFactory<NotificationDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddDbContext<NotificationDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// 2. OpenIddict Validation (Introspection)
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(builder.Configuration["OpenIddict:Issuer"] ?? "http://domovoy-auth:8080/");
        options.UseIntrospection()
            .SetClientId(builder.Configuration["OpenIddict:ClientId"] ?? "domovoy-notification")
            .SetClientSecret(builder.Configuration["OpenIddict:ClientSecret"] ?? "notification-secret");
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

// 3. Authentication: SmartBearer Policy Scheme
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

// 4. Telegram Bot
var telegramToken = builder.Configuration["Telegram:BotToken"];
if (!string.IsNullOrEmpty(telegramToken))
{
    builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramToken));
}

// 5. Notification Senders
builder.Services.AddSingleton<INotificationSender, TelegramSender>();
builder.Services.AddSingleton<INotificationSender, EmailSender>();

// 6. MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<RuleTriggeredConsumer>();
    x.AddConsumer<CommandFailedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:User"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Pass"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
    });
});

// 7. HealthChecks & Controllers
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>("database");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Notification Service API",
        Version = "v1"
    });
    opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    opts.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

app.MapControllers();

app.Run();