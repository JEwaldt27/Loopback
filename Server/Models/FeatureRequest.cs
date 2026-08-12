namespace Server.Models;

/// <summary>
/// A user-submitted feature request. Anyone signed in can raise one and see them all;
/// see FeatureRequestsController for who may change what.
/// </summary>
public class FeatureRequest
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string Status { get; set; } = Statuses.Received;
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public static class Statuses
    {
        public const string Received = "Received";
        public const string WIP = "WIP";
        public const string Done = "Done";
        public const string Declined = "Declined";

        public static readonly string[] All = { Received, WIP, Done, Declined };

        public static bool IsValid(string? s) =>
            s != null && All.Contains(s, StringComparer.OrdinalIgnoreCase);

        // Canonical casing, so "wip" from a hand-rolled request still stores as "WIP".
        public static string Normalize(string s) =>
            All.First(v => string.Equals(v, s, StringComparison.OrdinalIgnoreCase));
    }
}
