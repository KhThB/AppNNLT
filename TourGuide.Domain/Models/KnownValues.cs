namespace TourGuide.Domain.Models;

public static class KnownRoles
{
    public const string Admin = "Admin";
    public const string Merchant = "Merchant";
    public const string User = "User";
}

public static class PoiWorkflowStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Archived = "Archived";
}

public static class ModerationStatuses
{
    public const string PendingManual = "PendingManual";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class TranslationStatuses
{
    public const string Pending = "Pending";
    public const string Ready = "Ready";
    public const string PendingManual = "PendingManual";
}

public static class SubscriptionPackages
{
    public const string Basic = "Basic";
    public const string Premium = "Premium";
    public const string Boost = "Boost";
}

public static class NarrationStatuses
{
    public const string Started = "Started";
    public const string Completed = "Completed";
    public const string Replay = "Replay";
    public const string Error = "Error";
}
