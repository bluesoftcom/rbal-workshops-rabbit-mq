namespace Models;

/// <summary>
/// User model version 2 - Evolved schema with additional optional fields
/// </summary>
public class UserV2
{
    public int UserId { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }

    // New optional fields for evolution
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime? CreatedDate { get; set; }
}