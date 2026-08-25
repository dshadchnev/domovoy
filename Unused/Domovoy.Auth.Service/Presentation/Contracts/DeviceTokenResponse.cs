namespace Domovoy.Auth.Service.Presentation.Contracts;

public record DeviceTokenResponse(
    string AccessToken,
    int ExpiresIn,
    string TokenType = "Bearer");
