namespace JuliHyro.DotNetTool.Shared.Models;

/// <summary>
/// Collects every location and SDK product discovered by a deep scan.
/// </summary>
public sealed class ScanResult
{
    public List<string> DotNetRoots { get; } = [];
    public List<string> PathEntries { get; } = [];
    public List<string> RegistryLocations { get; } = [];
    public List<SdkInstallation> Installations { get; } = [];
    public List<MsiSdkProduct> MsiProducts { get; } = [];
    public List<string> ScannedDrives { get; } = [];
    public List<string> Warnings { get; } = [];
    public long DirectoriesVisited { get; internal set; }
}
