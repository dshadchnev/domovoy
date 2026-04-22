namespace Domovoy.Auth.Service.Contracts;

public record UserRegisterRequest(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName);
