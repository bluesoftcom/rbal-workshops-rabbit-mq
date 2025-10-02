namespace Models;

/// <summary>
/// User model version 1 - Original schema
/// </summary>
public class UserV1
{
    public int UserId { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
}