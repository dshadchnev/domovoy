using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Domovoy.Auth.Service.Contracts;

/// <summary>Запрос на регистрацию нового пользователя</summary>
public class UserRegisterRequest
{
    /// <summary>Имя пользователя (логин)</summary>
    [Required]
    [MinLength(3)]
    [DefaultValue("testuser")]
    public string Username { get; init; } = string.Empty;

    /// <summary>Адрес электронной почты</summary>
    [Required]
    [EmailAddress]
    [DefaultValue("user@example.com")]
    public string Email { get; init; } = string.Empty;

    /// <summary>Пароль (минимум 6 символов)</summary>
    [Required]
    [MinLength(6)]
    [DefaultValue("Test1234")]
    public string Password { get; init; } = string.Empty;

    /// <summary>Имя</summary>
    [Required]
    [DefaultValue("Иван")]
    public string FirstName { get; init; } = string.Empty;

    /// <summary>Фамилия</summary>
    [Required]
    [DefaultValue("Иванов")]
    public string LastName { get; init; } = string.Empty;
}
