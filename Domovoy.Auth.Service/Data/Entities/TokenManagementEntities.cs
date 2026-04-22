using System.ComponentModel.DataAnnotations.Schema;

namespace Domovoy.Auth.Service.Data.Entities;

[Table("RefreshTokens")]
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Column("UserId")] 
    public Guid UserId { get; set; }
    
    [Column("Token")] 
    public string Token { get; set; } = default!;
    
    [Column("TokenHash")] 
    public string TokenHash { get; set; } = default!;
    
    [Column("ExpiresAt")] 
    public DateTime ExpiresAt { get; set; }
    
    [Column("RevokedAt")] 
    public DateTime? RevokedAt { get; set; }
    
    [Column("CreatedAt")] 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Column("ReplacedByTokenId")] 
    public Guid? ReplacedByTokenId { get; set; }
    
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => !IsRevoked && !IsExpired;
}

[Table("AuditLogs")]
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Column("UserId")] 
    public Guid? UserId { get; set; }
    
    [Column("DeviceId")] 
    public string? DeviceId { get; set; }
    
    [Column("Action")] 
    public string Action { get; set; } = default!;
    
    [Column("Resource")] 
    public string Resource { get; set; } = default!;
    
    [Column("Result")] 
    public string Result { get; set; } = default!; // "Success" or "Failure"
    
    [Column("IpAddress")] 
    public string? IpAddress { get; set; }
    
    [Column("FailureReason")] 
    public string? FailureReason { get; set; }
    
    [Column("CreatedAt")] 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
