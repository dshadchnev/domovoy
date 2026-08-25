using Domovoy.Auth.Service.Infrastructure.Persistence.Entities;
using Domovoy.Domain.Entities;

namespace Domovoy.Auth.Service.Application.Mappers;

public static class UserDomainMapper
{
    public static User ToDomainUser(this AuthUser entity)
    {
        if (entity == null) return null!;

        return new User
        {
            Id = entity.Id,
            Username = entity.UserName ?? entity.Email ?? entity.Id.ToString(),
            Email = entity.Email ?? string.Empty,
            FirstName = entity.FirstName ?? string.Empty,
            LastName = entity.LastName ?? string.Empty,
            CreatedAt = entity.CreatedAt,
            LastLoginAt = entity.LastLoginAt,
            IsActive = entity.IsActive
        };
    }

    public static void ApplyDomainChanges(this AuthUser entity, User domain)
    {
        if (entity == null || domain == null) return;

        entity.FirstName = domain.FirstName;
        entity.LastName = domain.LastName;
        entity.IsActive = domain.IsActive;
        entity.LastLoginAt = domain.LastLoginAt;
    }
}
