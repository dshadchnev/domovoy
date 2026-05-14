using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Domovoy.Auth.Service.Contracts;
using Domovoy.Auth.Service.Controllers;
using Domovoy.Auth.Service.Data.Entities;
using Domovoy.Auth.Service.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Domovoy.Auth.Service.Tests
{
    public class AuthControllerTests
    {
        private readonly Mock<IUserAuthService> _userAuthServiceMock = new();
        private readonly Mock<ILogger<AuthController>> _loggerMock = new();
        private readonly Mock<UserManager<AuthUser>> _userManagerMock;
        private readonly Mock<RoleManager<AuthRole>> _roleManagerMock;

        public AuthControllerTests()
        {
            _userManagerMock = MockUserManager<AuthUser>();
            _roleManagerMock = MockRoleManager<AuthRole>();
        }

        public static Mock<UserManager<TUser>> MockUserManager<TUser>() where TUser : class
        {
            var store = new Mock<IUserStore<TUser>>();
            var mgr = new Mock<UserManager<TUser>>(store.Object, null, null, null, null, null, null, null, null);
            mgr.Object.UserValidators.Add(new UserValidator<TUser>());
            mgr.Object.PasswordValidators.Add(new PasswordValidator<TUser>());
            return mgr;
        }

        public static Mock<RoleManager<TRole>> MockRoleManager<TRole>() where TRole : class
        {
            var store = new Mock<IRoleStore<TRole>>();
            var roles = new List<IRoleValidator<TRole>>();
            roles.Add(new RoleValidator<TRole>());
            return new Mock<RoleManager<TRole>>(store.Object, roles, null, null, null);
        }

        [Fact]
        public async Task Register_ShouldReturnCreated_WhenRegistrationSucceeds()
        {
            // Arrange
            var request = new UserRegisterRequest("test", "test@test.com", "pass", "First", "Last");
            var response = new UserResponse(Guid.NewGuid(), "test", "test@test.com", "First", "Last", true, DateTime.UtcNow);
            
            _userAuthServiceMock.Setup(s => s.RegisterAsync(request, It.IsAny<string>()))
                .ReturnsAsync(response);

            var controller = new AuthController(_userAuthServiceMock.Object, _loggerMock.Object, _userManagerMock.Object, _roleManagerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await controller.Register(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(response, createdResult.Value);
        }

        [Fact]
        public async Task Login_ShouldReturnOk_WhenLoginSucceeds()
        {
            // Arrange
            var request = new UserLoginRequest("test", "pass");
            var response = new TokenResponse("access", "refresh", 3600);
            
            _userAuthServiceMock.Setup(s => s.LoginAsync(request, It.IsAny<string>()))
                .ReturnsAsync(response);

            var controller = new AuthController(_userAuthServiceMock.Object, _loggerMock.Object, _userManagerMock.Object, _roleManagerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(response, okResult.Value);
        }

        [Fact]
        public async Task Login_ShouldReturnUnauthorized_WhenServiceThrowsUnauthorized()
        {
            // Arrange
            var request = new UserLoginRequest("test", "wrong");
            _userAuthServiceMock.Setup(s => s.LoginAsync(request, It.IsAny<string>()))
                .ThrowsAsync(new UnauthorizedAccessException("Invalid credentials"));

            var controller = new AuthController(_userAuthServiceMock.Object, _loggerMock.Object, _userManagerMock.Object, _roleManagerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await controller.Login(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}
