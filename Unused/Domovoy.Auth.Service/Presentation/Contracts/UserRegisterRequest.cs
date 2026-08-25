using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Domovoy.Auth.Service.Presentation.Contracts;

/// <summary>Р—Р°РїСЂРѕСЃ РЅР° СЂРµРіРёСЃС‚СЂР°С†РёСЋ РЅРѕРІРѕРіРѕ РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ</summary>
public class UserRegisterRequest
{
    /// <summary>РРјСЏ РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ (Р»РѕРіРёРЅ)</summary>
    [Required]
    [MinLength(3)]
    [DefaultValue("testuser")]
    public string Username { get; init; } = string.Empty;

    /// <summary>РђРґСЂРµСЃ СЌР»РµРєС‚СЂРѕРЅРЅРѕР№ РїРѕС‡С‚С‹</summary>
    [Required]
    [EmailAddress]
    [DefaultValue("user@example.com")]
    public string Email { get; init; } = string.Empty;

    /// <summary>РџР°СЂРѕР»СЊ (РјРёРЅРёРјСѓРј 6 СЃРёРјРІРѕР»РѕРІ)</summary>
    [Required]
    [MinLength(6)]
    [DefaultValue("Test1234")]
    public string Password { get; init; } = string.Empty;

    /// <summary>РРјСЏ</summary>
    [Required]
    [DefaultValue("РРІР°РЅ")]
    public string FirstName { get; init; } = string.Empty;

    /// <summary>Р¤Р°РјРёР»РёСЏ</summary>
    [Required]
    [DefaultValue("РРІР°РЅРѕРІ")]
    public string LastName { get; init; } = string.Empty;
}
