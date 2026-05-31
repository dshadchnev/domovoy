using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MassTransit;
using Domovoy.RulesEngine.Service.Data;
using Domovoy.RulesEngine.Service.Consumers;

var builder = WebApplication.CreateBuilder(args);

// 1. PostgreSQL
// AddDbContextFactory (Singleton) + AddDbContext (Scoped) — стандартная комбинация:
// Consumer (MassTransit Singleton) получает IDbContextFactory, Controller получает RulesEngineDbContext.
var connStr = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContextFactory<RulesEngineDbContext>(
    opts => opts.UseNpgsql(connStr));
builder.Services.AddDbContext<RulesEngineDbContext>(
    opts => opts.UseNpgsql(connStr));

// 2. JWT Validation (�� �� ������������, ��� � ������ ��������)
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

// 3. MassTransit v8+ (�������� ����� �����������)
builder.Services.AddMassTransit(x =>
{
    // ������������ ���������� ��� ����������
    x.AddConsumer<TelemetryRuleEvaluator>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:User"]);
            h.Password(builder.Configuration["RabbitMQ:Pass"]);
        });

        // ����������� �������� ����� ��� �����������
        cfg.ConfigureEndpoints(context);

        // Retry-�������� (�������� ����� �����)
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
    opts.SwaggerDoc("v1", new() { Title = "Domovoy RulesEngine Service", Version = "v1" });
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

// �������� ��� ������ (��� Dev)
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

// HealthChecks ���������
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = _ => false });

// API ���������
app.MapControllers();

app.Run();