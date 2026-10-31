namespace JuliHyro.DotNetTool.Shared.Models;

/// <summary>
/// A resolved SDK release and the official Windows x64 installer URL.
/// </summary>
public sealed record SdkRelease(string Version, Uri InstallerUri);
