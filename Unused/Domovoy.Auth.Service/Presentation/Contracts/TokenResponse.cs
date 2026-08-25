namespace Domovoy.Auth.Service.Presentation.Contracts;

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType = "Bearer");
