using UnityEngine;

/// <summary>App metadata for UI display (version from Player Settings bundleVersion).</summary>
public static class AppInfo
{
    public static string Version => Application.version;

    public static string VersionDisplay => $"Ver {Application.version}";
}
