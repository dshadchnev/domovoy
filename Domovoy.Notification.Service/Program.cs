using System.Text;
using Domovoy.Notification.Service.Data;
using Domovoy.Notification.Service.Consumers;
using Domovoy.Notification.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MassTransit;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// 1. PostgreSQL
builder.Services.AddDbContextFactory<NotificationDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddDbContext<NotificationDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// 2. JWT Validation
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// 3. Telegram Bot
var telegramToken = builder.Configuration["Telegram:BotToken"];
if (!string.IsNullOrEmpty(telegramToken))
{
    builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramToken));
}

// 4. Notification Senders
builder.Services.AddSingleton<INotificationSender, TelegramSender>();
builder.Services.AddSingleton<INotificationSender, EmailSender>();


// 5. MassTransit
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

// 6. HealthChecks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>("database");

// 7. Controllers + Swagger
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