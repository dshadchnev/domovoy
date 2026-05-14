using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Domovoy.Auth.Service.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace Domovoy.Auth.Service.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> opts) : IdentityDbContext<AuthUser, AuthRole, Guid>(opts)
{
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AuthUser>().Property(u => u.NormalizedUserName).HasMaxLength(100);
        builder.Entity<AuthUser>().Property(u => u.NormalizedEmail).HasMaxLength(200);
        builder.Entity<AuthUser>().HasIndex(u => u.UserName).IsUnique();
        builder.Entity<AuthUser>().HasIndex(u => u.Email).IsUnique();

        builder.Entity<DeviceCredential>().HasIndex(d => d.NetworkDeviceId).IsUnique();
        builder.Entity<DeviceCredential>().HasQueryFilter(d => !d.IsRevoked);

        builder.Entity<RefreshToken>().HasIndex(r => r.Token).IsUnique();
        builder.Entity<RefreshToken>().HasIndex(r => r.TokenHash).IsUnique();
        builder.Entity<RefreshToken>().HasIndex(r => r.UserId);
        builder.Entity<RefreshToken>().HasQueryFilter(r => r.RevokedAt == null);

        builder.Entity<AuditLog>().HasIndex(a => a.UserId);
        builder.Entity<AuditLog>().HasIndex(a => a.CreatedAt).IsDescending();

        // 🔑 OpenIddict с Guid-ключами
        builder.UseOpenIddict<Guid>();
    }
}