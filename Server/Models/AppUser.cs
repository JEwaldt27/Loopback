namespace Server.Models;

public class AppUser
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User"; // "Admin" or "User"
}
