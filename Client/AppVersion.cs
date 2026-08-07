namespace Client;

/// <summary>
/// Single source of truth for the displayed app version. Bump this with each deploy so you
/// can confirm at a glance (toolbar + PDF header) that the server is running the new build.
/// </summary>
public static class AppVersion
{
    public const string Version = "1.5.23";
}
