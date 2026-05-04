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

public sealed class TranslationPreviewRequestModel
{
    public string SourceLanguage { get; set; } = "vi";
    public string SourceDescription { get; set; } = string.Empty;
    public IReadOnlyList<string> TargetLanguages { get; set; } = Array.Empty<string>();
}

public sealed class TranslationPreviewResponseModel
{
    public string SourceLanguage { get; set; } = "vi";
    public string SourceHash { get; set; } = string.Empty;
    public bool IsProviderConfigured { get; set; }
    public Dictionary<string, LocalizedContentModel> Contents { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class LocalizedContentModel
{
    public string Description { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
