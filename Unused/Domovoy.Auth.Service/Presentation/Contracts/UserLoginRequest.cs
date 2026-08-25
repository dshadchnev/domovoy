using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Domovoy.Auth.Service.Presentation.Contracts;

/// <summary>Р—Р°РїСЂРѕСЃ РЅР° РІС…РѕРґ РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ</summary>
public class UserLoginRequest
{
    /// <summary>РРјСЏ РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ (Р»РѕРіРёРЅ)</summary>
    [Required]
    [MinLength(3)]
    [DefaultValue("testuser")]
    public string Username { get; init; } = string.Empty;

    /// <summary>РџР°СЂРѕР»СЊ</summary>
    [Required]
    [MinLength(6)]
    [DefaultValue("Test1234")]
    public string Password { get; init; } = string.Empty;
}
