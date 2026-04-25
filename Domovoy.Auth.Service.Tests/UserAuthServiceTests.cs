using System;
using System.Threading.Tasks;
using Domovoy.Auth.Service.Contracts;
using Domovoy.Auth.Service.Data;
using Domovoy.Auth.Service.Data.Entities;
using Domovoy.Auth.Service.Services;
using Domovoy.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Domovoy.Auth.Service.Tests
{
    public class UserAuthServiceTests
    {
        private readonly Mock<IPublishEndpoint> _busMock = new();
        private readonly Mock<ILogger<UserAuthService>> _loggerMock = new();
        private readonly Mock<ITokenService> _tokenServiceMock = new();
        private readonly Mock<IAuditService> _auditServiceMock = new();
        private readonly Mock<IValidationService> _validationServiceMock = new();
        private readonly AuthDbContext _db;

        public UserAuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _db = new AuthDbContext(options);
        }

        [Fact]
        public async Task RegisterAsync_ShouldCreateUser_WhenRequestIsValid()
        {
            // Arrange
            var request = new UserRegisterRequest { Username = "testuser", Email = "test@example.com", Password = "Password123!", FirstName = "First", LastName = "Last" };
            _validationServiceMock.Setup(v => v.ValidateUserRegistrationAsync(request))
                .ReturnsAsync((true, string.Empty));

            var service = new UserAuthService(_db, _busMock.Object, _loggerMock.Object, _tokenServiceMock.Object, _auditServiceMock.Object, _validationServiceMock.Object);

            // Act
            var response = await service.RegisterAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(request.Username, response.Username);
            Assert.Equal(request.Email, response.Email);
            
            var userInDb = await _db.Users.FirstOrDefaultAsync(u => u.UserName == request.Username);
            Assert.NotNull(userInDb);
            Assert.Equal(request.Email, userInDb.Email);
            
            _busMock.Verify(b => b.Publish(It.IsAny<UserRegisteredEvent>(), default), Times.Once);
            _auditServiceMock.Verify(a => a.LogUserActionAsync(It.IsAny<Guid>(), "USER_REGISTER", "Success", It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrowException_WhenValidationFails()
        {
            // Arrange
            var request = new UserRegisterRequest { Username = "invalid", Email = "invalid@example.com", Password = "123", FirstName = "First", LastName = "Last" };
            _validationServiceMock.Setup(v => v.ValidateUserRegistrationAsync(request))
                .ReturnsAsync((false, "Invalid password"));

            var service = new UserAuthService(_db, _busMock.Object, _loggerMock.Object, _tokenServiceMock.Object, _auditServiceMock.Object, _validationServiceMock.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(request));
            Assert.Equal("Invalid password", exception.Message);
            
            _auditServiceMock.Verify(a => a.LogUserActionAsync(null, "USER_REGISTER", "Failure", It.IsAny<string>(), "Invalid password"), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnTokens_WhenCredentialsAreValid()
        {
            // Arrange
            var username = "loginuser";
            var password = "Password123!";
            var user = new AuthUser
            {
                Id = Guid.NewGuid(),
                UserName = username,
                Email = "login@example.com",
                IsActive = true
            };
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<AuthUser>();
            user.PasswordHash = hasher.HashPassword(user, password);
            
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var request = new UserLoginRequest { Username = username, Password = password };
            _validationServiceMock.Setup(v => v.ValidateUserLogin(request))
                .Returns((true, string.Empty));
            _tokenServiceMock.Setup(t => t.GenerateUserToken(user)).Returns("access-token");
            _tokenServiceMock.Setup(t => t.CreateRefreshTokenAsync(user.Id, null))
                .ReturnsAsync(new RefreshToken { Token = "refresh-token", UserId = user.Id });
            _tokenServiceMock.Setup(t => t.GetTokenConfig())
                .Returns(new TokenConfig(60, 7, "issuer", "audience"));

            var service = new UserAuthService(_db, _busMock.Object, _loggerMock.Object, _tokenServiceMock.Object, _auditServiceMock.Object, _validationServiceMock.Object);

            // Act
            var response = await service.LoginAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("access-token", response.AccessToken);
            Assert.Equal("refresh-token", response.RefreshToken);
            
            _auditServiceMock.Verify(a => a.LogUserActionAsync(user.Id, "USER_LOGIN", "Success", It.IsAny<string>(), null), Times.Once);
            _busMock.Verify(b => b.Publish(It.IsAny<UserLoggedInEvent>(), default), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrowUnauthorized_WhenUserNotFound()
        {
            // Arrange
            var request = new UserLoginRequest { Username = "nonexistent", Password = "password" };
            _validationServiceMock.Setup(v => v.ValidateUserLogin(request))
                .Returns((true, string.Empty));

            var service = new UserAuthService(_db, _busMock.Object, _loggerMock.Object, _tokenServiceMock.Object, _auditServiceMock.Object, _validationServiceMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoginAsync(request));
            _auditServiceMock.Verify(a => a.LogUserActionAsync(null, "USER_LOGIN", "Failure", It.IsAny<string>(), "Invalid credentials"), Times.Once);
        }

        [Fact]
        public async Task RefreshTokenAsync_ShouldReturnNewTokens_WhenTokenIsValid()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new AuthUser { Id = userId, IsActive = true, UserName = "test" };
            _db.Users.Add(user);
            
            var oldToken = new RefreshToken 
            { 
                Token = "old-token", 
                TokenHash = "old-hash",
                UserId = userId, 
                ExpiresAt = DateTime.UtcNow.AddDays(7) 
            };
            _db.RefreshTokens.Add(oldToken);
            await _db.SaveChangesAsync();

            var request = new RefreshTokenRequest("old-token");
            _validationServiceMock.Setup(v => v.ValidateRefreshToken(request))
                .Returns((true, string.Empty));
            
            var newAccessToken = "new-access";
            var newRefreshToken = new RefreshToken { Id = Guid.NewGuid(), Token = "new-refresh", UserId = userId };
            
            _tokenServiceMock.Setup(t => t.GenerateUserToken(user)).Returns(newAccessToken);
            _tokenServiceMock.Setup(t => t.CreateRefreshTokenAsync(userId, oldToken.Id)).ReturnsAsync(newRefreshToken);
            _tokenServiceMock.Setup(t => t.GetTokenConfig()).Returns(new TokenConfig(60, 7, "issuer", "audience"));

            var service = new UserAuthService(_db, _busMock.Object, _loggerMock.Object, _tokenServiceMock.Object, _auditServiceMock.Object, _validationServiceMock.Object);

            // Act
            var response = await service.RefreshTokenAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(newAccessToken, response.AccessToken);
            Assert.Equal(newRefreshToken.Token, response.RefreshToken);
            
            var updatedOldToken = await _db.RefreshTokens.FindAsync(oldToken.Id);
            Assert.NotNull(updatedOldToken);
            Assert.NotNull(updatedOldToken.RevokedAt);
            Assert.Equal(newRefreshToken.Id, updatedOldToken.ReplacedByTokenId);
        }
    }
}
