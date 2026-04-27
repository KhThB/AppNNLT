namespace TourGuide.API.Infrastructure.Options;

public sealed class AdminBootstrapOptions
{
    public bool Enabled { get; set; } = true;
    public string FullName { get; set; } = "System Admin";
    public string Phone { get; set; } = "admin";
    public string Email { get; set; } = "admin@tourguide.local";
    public string Password { get; set; } = "Admin123!";
}
