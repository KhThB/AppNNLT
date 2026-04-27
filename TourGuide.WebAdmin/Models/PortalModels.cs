namespace TourGuide.WebAdmin.Models;

public sealed class SessionUser
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}

public sealed class AuthResult
{
    public string Token { get; set; } = string.Empty;
    public SessionUser User { get; set; } = new();
}
