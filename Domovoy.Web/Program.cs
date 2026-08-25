using Domovoy.Web.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var gatewayUrl = builder.Configuration["GatewayUrl"] ?? "http://localhost:8085";

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(gatewayUrl)
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiService>();

builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseHttpMetrics();
app.MapMetrics();

app.MapHealthChecks("/health");
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
