namespace SpireKit.Multiplayer;

/// <summary>This machine's role in the current game session.</summary>
public enum SessionRole
{
    /// <summary>Not in a lobby or run (main menu, settings).</summary>
    Offline,

    /// <summary>Single-player run. Behaves as the host of a party of one.</summary>
    Solo,

    /// <summary>Hosting a multiplayer lobby or run. Authoritative for shared settings.</summary>
    Host,

    /// <summary>Joined someone else's lobby or run. Never authoritative.</summary>
    Client,
}

/// <summary>Helpers for <see cref="SessionRole"/>.</summary>
public static class SessionRoleExtensions
{
    /// <summary>True when this machine's local settings are the source of truth.</summary>
    public static bool IsAuthority(this SessionRole role) => role is not SessionRole.Client;
}
