using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Users: người dùng (khách và nhân viên).</summary>
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime? BirthDate { get; set; }
    public string Role { get; set; } = string.Empty;
    public int? CinemaId { get; set; }
    public bool Active { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public string? MemberCode { get; set; }
    public int LoyaltyPoints { get; set; }
    public string Tier { get; set; } = LoyaltyTier.Member;
    public long WalletBalance { get; set; }
    public string? ManagerPinHash { get; set; }
    public int PinFailedCount { get; set; }
    public DateTime? PinLockedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}
