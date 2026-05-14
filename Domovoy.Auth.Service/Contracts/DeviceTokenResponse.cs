namespace Domovoy.Auth.Service.Contracts;

public record DeviceTokenResponse(
    string AccessToken,
    int ExpiresIn,
    string TokenType = "Bearer");
