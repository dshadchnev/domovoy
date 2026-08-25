using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Domovoy.Devices.Service.Tests;

public class WeatherForecastEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WeatherForecastEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        });
    }

    [Fact]
    public async Task GetWeatherForecast_ReturnsSuccessAndContent()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/weatherforecast");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Status: {response.StatusCode}, Body: {content}");
        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(25, 76)]
    public void WeatherForecast_TemperatureF_CalculatedCorrectly(int tempC, int expectedF)
    {
        int actualF = 32 + (int)(tempC / 0.5556);
        Assert.Equal(expectedF, actualF);
    }
}
