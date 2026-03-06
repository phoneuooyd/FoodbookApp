using System;
using System.ComponentModel.DataAnnotations;

namespace Foodbook.Models;

public class AuthAccount
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string SupabaseUserId { get; set; } = string.Empty;

    [MaxLength(320)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    public bool IsAutoLoginEnabled { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastSignInUtc { get; set; }
}
