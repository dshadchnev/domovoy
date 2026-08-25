namespace Domovoy.Auth.Service.Presentation.Contracts;

public record UserResponse(
    Guid Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    DateTime CreatedAt);
