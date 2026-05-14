using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Domovoy.Auth.Service.Contracts;

/// <summary>Запрос на вход пользователя</summary>
public class UserLoginRequest
{
    /// <summary>Имя пользователя (логин)</summary>
    [Required]
    [MinLength(3)]
    [DefaultValue("testuser")]
    public string Username { get; init; } = string.Empty;

    /// <summary>Пароль</summary>
    [Required]
    [MinLength(6)]
    [DefaultValue("Test1234")]
    public string Password { get; init; } = string.Empty;
}
