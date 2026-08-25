using System;
using System.Threading.Tasks;
using Domovoy.Auth.Service.Application.Services;
using Domovoy.Auth.Service.Infrastructure.Persistence;
using Domovoy.Auth.Service.Presentation.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Domovoy.Auth.Service.Tests;

public class ValidationServiceTests
{
    private readonly AuthDbContext _db;
    private readonly Mock<ILogger<ValidationService>> _loggerMock = new();
    private readonly ValidationService _service;

    public ValidationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AuthDbContext(options);
        _service = new ValidationService(_db, _loggerMock.Object);
    }

    [Theory]
    [InlineData("short", "Password must be at least 8 characters")]
    [InlineData("lowercaseonly1!", "Password must contain at least one uppercase letter")]
    [InlineData("UPPERCASEONLY1!", "Password must contain at least one lowercase letter")]
    [InlineData("NoDigitsInPassword!", "Password must contain at least one digit")]
    [InlineData("NoSpecial1234", "Password must contain at least one special character")]
    public async Task ValidateUserRegistrationAsync_WeakPassword_ReturnsValidationError(string password, string expectedError)
    {
        // Arrange
        var request = new UserRegisterRequest
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = password
        };

        // Act
        var (isValid, errorMessage) = await _service.ValidateUserRegistrationAsync(request);

        // Assert
        Assert.False(isValid);
        Assert.Equal(expectedError, errorMessage);
    }

    [Fact]
    public async Task ValidateUserRegistrationAsync_StrongPassword_ReturnsValid()
    {
        // Arrange
        var request = new UserRegisterRequest
        {
            Username = "validuser",
            Email = "validuser@example.com",
            Password = "StrongPassword123!"
        };

        // Act
        var (isValid, errorMessage) = await _service.ValidateUserRegistrationAsync(request);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }
}
