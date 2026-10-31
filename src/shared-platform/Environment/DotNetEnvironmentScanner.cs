using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;
using JuliHyro.DotNetTool.Shared.Models;

namespace JuliHyro.DotNetTool.Shared.Environment;

/// <summary>
/// Discovers .NET SDK installations using structured sources first and optional
/// recursive scanning of non-system drives only when explicitly requested.
/// This library contains no user-interface or console interaction.
/// </summary>
public sealed class DotNetEnvironmentScanner
{
    private static readonly Regex SdkVersionDirectory = new(
        @"^\d+\.\d+\.\d+(?:[-+].*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] UninstallRoots =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    /// <summary>
    /// Runs the quick discovery pipeline: Registry, environment variables,
    /// PATH and known installation directories. It never scans drives recursively.
    /// </summary>
    public ScanResult DiscoverStandard(
        Action<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ScanResult();
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pathEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Report(progress, "[1/4] Scanning Windows Registry...");
        cancellationToken.ThrowIfCancellationRequested();
        ScanDotNetRegistry(result, roots);
        ScanUninstallRegistry(result);

        Report(progress, $"Registry scan completed. Found {result.RegistryLocations.Count} .NET registry locations and {result.MsiProducts.Count} registered SDK products.");

        Report(progress, "[2/4] Scanning environment variables...");
        cancellationToken.ThrowIfCancellationRequested();
        ScanEnvironmentVariables(roots);
        Report(progress, $"Environment scan completed. Found {roots.Count} known installation roots.");

        Report(progress, "[3/4] Scanning PATH entries...");
        cancellationToken.ThrowIfCancellationRequested();
        ScanPathEntries(roots, pathEntries);
        Report(progress, $"PATH scan completed. Found {pathEntries.Count} relevant PATH entries.");

        Report(progress, "[4/4] Inspecting known installation directories...");
        cancellationToken.ThrowIfCancellationRequested();
        ScanKnownDirectories(roots);

        foreach (var root in roots.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            InspectDotNetRoot(root, result, pathEntries, "Known directory");
        }

        foreach (var installation in ScanStandard())
        {
            AddInstallation(result, installation);
            AddRoot(Path.GetDirectoryName(FindDotNetCommand() ?? string.Empty), roots);
        }

        result.DotNetRoots.AddRange(roots.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        result.PathEntries.AddRange(pathEntries.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        Report(progress, $"Standard discovery completed. Found {result.Installations.Count} SDK installation(s).");
        return result;
    }

    /// <summary>
    /// Compatibility helper that returns SDKs visible through the current PATH dotnet command.
    /// </summary>
    public IReadOnlyList<SdkInstallation> ScanStandard()
    {
        var command = FindDotNetCommand();
        if (command is null)
            return [];

        var root = Path.GetDirectoryName(command);
        if (string.IsNullOrWhiteSpace(root))
            return [];

        return ReadSdkVersions(command)
            .Select(version => new SdkInstallation(
                version,
                root,
                "dotnet CLI",
                true,
                false))
            .ToArray();
    }

    /// <summary>
    /// Performs structured discovery and then recursively scans either all available
    /// non-system drives or one explicitly selected non-system drive.
    /// The Windows system drive is never recursively scanned.
    /// </summary>
    public ScanResult ScanDeep(
        bool scanAllDrives = true,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default,
        string? selectedDrive = null)
    {
        var result = DiscoverStandard(progress, cancellationToken);

        if (!scanAllDrives)
        {
            Report(progress, "Deep drive scan disabled.");
            return result;
        }

        ScanNonSystemDrives(
            result,
            progress,
            cancellationToken,
            selectedDrive);

        Report(progress,
            $"Deep discovery completed. Found {result.Installations.Count} SDK installation(s).");

        return result;
    }

    private static void ScanEnvironmentVariables(HashSet<string> roots)
    {
        var names = new[] { "DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_X86" };

        foreach (var name in names)
        {
            AddRoot(System.Environment.GetEnvironmentVariable(name), roots);

            foreach (var target in new[]
            {
                EnvironmentVariableTarget.User,
                EnvironmentVariableTarget.Machine
            })
            {
                try
                {
                    AddRoot(System.Environment.GetEnvironmentVariable(name, target), roots);
                }
                catch (Exception ex) when (IsAccessOrIoException(ex))
                {
                    // An inaccessible environment scope must not stop discovery.
                }
            }
        }
    }

    private static void ScanPathEntries(
        HashSet<string> roots,
        HashSet<string> pathEntries)
    {
        var values = new List<string>
        {
            System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty
        };

        foreach (var target in new[]
        {
            EnvironmentVariableTarget.User,
            EnvironmentVariableTarget.Machine
        })
        {
            try
            {
                values.Add(System.Environment.GetEnvironmentVariable("PATH", target) ?? string.Empty);
            }
            catch (Exception ex) when (IsAccessOrIoException(ex))
            {
                // Continue with other PATH scopes.
            }
        }

        foreach (var value in values)
        {
            foreach (var rawEntry in value.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    var expanded = System.Environment.ExpandEnvironmentVariables(
                        rawEntry.Trim().Trim('"'));
                    var fullPath = NormalizePath(expanded);

                    if (fullPath is null || !Directory.Exists(fullPath))
                        continue;

                    if (!fullPath.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
                        continue;

                    pathEntries.Add(fullPath);

                    if (File.Exists(Path.Combine(fullPath, "dotnet.exe")))
                        AddRoot(fullPath, roots);
                }
                catch (Exception ex) when (IsAccessOrIoException(ex) || ex is ArgumentException or NotSupportedException)
                {
                    // Invalid PATH entries are ignored.
                }
            }
        }
    }

    private static void ScanKnownDirectories(HashSet<string> roots)
    {
        var programFiles = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.ProgramFilesX86);
        var userProfile = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.UserProfile);
        var localAppData = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.LocalApplicationData);

        // These two system-drive locations are deliberately inspected first.
        AddRoot(Path.Combine(programFiles, "dotnet"), roots);
        AddRoot(Path.Combine(programFilesX86, "dotnet"), roots);

        // User-scoped and common custom locations are checked without recursive traversal.
        AddRoot(Path.Combine(userProfile, ".dotnet"), roots);
        AddRoot(Path.Combine(userProfile, "dotnet"), roots);
        AddRoot(Path.Combine(localAppData, "Programs", "dotnet"), roots);
    }

    private static void ScanDotNetRegistry(
        ScanResult result,
        HashSet<string> roots)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var locations = new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\dotnet\Setup\InstalledVersions\x64"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\dotnet\Setup\InstalledVersions\x86"),
            (RegistryHive.CurrentUser, RegistryView.Registry64, @"SOFTWARE\dotnet\Setup\InstalledVersions\x64"),
            (RegistryHive.CurrentUser, RegistryView.Registry32, @"SOFTWARE\dotnet\Setup\InstalledVersions\x86")
        };

        foreach (var (hive, view, subKey) in locations)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(subKey);
                if (key is null)
                    continue;

                result.RegistryLocations.Add($"{hive}\\{view}\\{subKey}");
                AddRoot(key.GetValue("InstallLocation") as string, roots);
            }
            catch (Exception ex) when (IsAccessOrIoException(ex))
            {
                result.Warnings.Add($"Registry scan skipped {subKey}: {ex.Message}");
            }
        }
    }

    private static void ScanUninstallRegistry(ScanResult result)
    {
        if (!OperatingSystem.IsWindows())
            return;

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        foreach (var uninstallRoot in UninstallRoots)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var root = baseKey.OpenSubKey(uninstallRoot);
                if (root is null)
                    continue;

                foreach (var subName in root.GetSubKeyNames())
                {
                    try
                    {
                        using var product = root.OpenSubKey(subName);
                        if (product is null)
                            continue;

                        var displayName = product.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName) ||
                            !displayName.Contains(".NET SDK", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var uninstallString = product.GetValue("UninstallString") as string;
                        var productCode = Guid.TryParse(subName.Trim('{', '}'), out var guid)
                            ? guid.ToString("B").ToUpperInvariant()
                            : ExtractProductCode(uninstallString);

                        result.MsiProducts.Add(new MsiSdkProduct(
                            displayName,
                            product.GetValue("DisplayVersion") as string ?? "Unknown",
                            product.GetValue("Publisher") as string ?? "Unknown",
                            productCode,
                            uninstallString,
                            $"{hive}\\{uninstallRoot}\\{subName}",
                            view.ToString()));
                    }
                    catch (Exception ex) when (IsAccessOrIoException(ex))
                    {
                        result.Warnings.Add($"Could not inspect uninstall entry {subName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (IsAccessOrIoException(ex))
            {
                result.Warnings.Add($"Uninstall registry scan skipped {hive}\\{uninstallRoot}: {ex.Message}");
            }
        }

        var distinct = result.MsiProducts
            .GroupBy(
                product => (product.DisplayName, product.DisplayVersion, product.ProductCode),
                StringTupleComparer.Instance)
            .Select(group => group.First())
            .ToArray();

        result.MsiProducts.Clear();
        result.MsiProducts.AddRange(distinct);
    }

    private static void ScanNonSystemDrives(
        ScanResult result,
        Action<string>? progress,
        CancellationToken cancellationToken,
        string? selectedDrive)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var windowsDirectory = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.Windows);
        var systemDrive = Path.GetPathRoot(windowsDirectory);

        var availableDrives = new List<DriveInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady ||
                    drive.DriveType is not (DriveType.Fixed or DriveType.Removable) ||
                    PathsEqual(drive.Name, systemDrive))
                {
                    continue;
                }

                if (selectedDrive is not null &&
                    !PathsEqual(drive.Name, selectedDrive))
                {
                    continue;
                }

                availableDrives.Add(drive);
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore drives that cannot be inspected.
            }
            catch (System.Security.SecurityException)
            {
                // Ignore drives that cannot be inspected.
            }
            catch (IOException)
            {
                // Removable media may disappear while the drive list is inspected.
            }
        }

        var drives = availableDrives
            .OrderBy(drive => drive.DriveType == DriveType.Fixed ? 0 : 1)
            .ToArray();

        if (selectedDrive is not null && drives.Length == 0)
        {
            result.Warnings.Add(
                $"Selected drive '{selectedDrive}' is unavailable or is the Windows system drive. The system drive is excluded from recursive scanning.");
            Report(progress, "Selected drive was unavailable or excluded. No recursive scan was started.");
            return;
        }

        if (drives.Length == 0)
        {
            Report(progress, "No additional fixed or removable data drives are available. The Windows system drive was not recursively scanned.");
            return;
        }

        foreach (var drive in drives)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                result.ScannedDrives.Add(drive.Name);
                Report(progress, $"Scanning data drive {drive.Name}");

                TraverseDirectoryTree(
                    drive.RootDirectory.FullName,
                    result,
                    progress,
                    cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                // An inaccessible drive is skipped without aborting the scan.
            }
            catch (System.Security.SecurityException)
            {
                // Continue with the next drive.
            }
            catch (IOException ex)
            {
                result.Warnings.Add($"Could not scan drive {drive.Name}: {ex.Message}");
            }
        }
    }

    private static void TraverseDirectoryTree(
        string startPath,
        ScanResult result,
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push(startPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            var normalized = NormalizePath(current);

            if (normalized is null || !visited.Add(normalized))
                continue;

            result.DirectoriesVisited++;
            if (result.DirectoriesVisited % 5000 == 0)
                Report(progress, $"Visited {result.DirectoriesVisited:N0} directories...");

            try
            {
                var dotnetExe = Path.Combine(normalized, "dotnet.exe");
                var sdkDirectory = Path.Combine(normalized, "sdk");

                if (File.Exists(dotnetExe) && Directory.Exists(sdkDirectory))
                {
                    if (!result.DotNetRoots.Any(root => PathsEqual(root, normalized)))
                        result.DotNetRoots.Add(normalized);

                    InspectDotNetRoot(
                        normalized,
                        result,
                        new HashSet<string>(),
                        "Deep filesystem scan");
                }

                foreach (var directory in Directory.EnumerateDirectories(normalized))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(directory);

                    // Never descend into OS folders, even when another Windows installation
                    // happens to exist on a data drive.
                    if (name.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        var attributes = File.GetAttributes(directory);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            continue;

                        pending.Push(directory);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Access denied is expected on protected folders. Skip silently.
                    }
                    catch (System.Security.SecurityException)
                    {
                        // Skip this folder and continue with its siblings.
                    }
                    catch (IOException)
                    {
                        // The folder may be locked, unavailable or removed.
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip inaccessible directory without polluting the warning list.
            }
            catch (System.Security.SecurityException)
            {
                // Continue with the remaining directory queue.
            }
            catch (IOException)
            {
                // Continue with the remaining directory queue.
            }
        }
    }

    private static void InspectDotNetRoot(
        string root,
        ScanResult result,
        HashSet<string> pathEntries,
        string source)
    {
        var sdkDirectory = Path.Combine(root, "sdk");
        if (!Directory.Exists(sdkDirectory))
            return;

        var isRegistered = pathEntries.Any(path => PathsEqual(path, root)) ||
            PathsEqual(System.Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? string.Empty, root) ||
            PathsEqual(System.Environment.GetEnvironmentVariable("DOTNET_ROOT_X64") ?? string.Empty, root) ||
            PathsEqual(System.Environment.GetEnvironmentVariable("DOTNET_ROOT_X86") ?? string.Empty, root);

        var markerExists = File.Exists(Path.Combine(root, ".jh-dtm-install.json"));

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(sdkDirectory))
            {
                var version = Path.GetFileName(directory);
                if (!SdkVersionDirectory.IsMatch(version))
                    continue;

                AddInstallation(result, new SdkInstallation(
                    version,
                    NormalizePath(root) ?? root,
                    source,
                    isRegistered,
                    markerExists));
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore inaccessible SDK directory.
        }
        catch (System.Security.SecurityException)
        {
            // Ignore inaccessible SDK directory.
        }
        catch (IOException ex)
        {
            result.Warnings.Add($"Could not inspect SDK directory {sdkDirectory}: {ex.Message}");
        }
    }

    private static string? FindDotNetCommand()
    {
        var path = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var entry in path.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var expanded = System.Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"'));
                var candidate = Path.Combine(expanded, "dotnet.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (Exception ex) when (IsAccessOrIoException(ex) || ex is ArgumentException or NotSupportedException)
            {
                // Ignore malformed PATH entry.
            }
        }

        return null;
    }

    private static IEnumerable<string> ReadSdkVersions(string dotnetPath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = dotnetPath,
                ArgumentList = { "--list-sdks" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (process is null)
                return [];

            var outputTask = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                process.Kill(true);
                return [];
            }

            var output = outputTask.GetAwaiter().GetResult();
            return output.Split(
                    System.Environment.NewLine,
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .OfType<string>()
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static void AddRoot(string? path, HashSet<string> roots)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var normalized = NormalizePath(
            System.Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));

        if (normalized is not null && Directory.Exists(normalized))
            roots.Add(normalized);
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var pathRoot = Path.GetPathRoot(fullPath) ?? string.Empty;
            var trimmedPath = fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

            if (string.Equals(
                trimmedPath,
                pathRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                return pathRoot;
            }

            return trimmedPath;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        var normalizedLeft = NormalizePath(left);
        var normalizedRight = NormalizePath(right);

        return normalizedLeft is not null && normalizedRight is not null &&
            string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractProductCode(string? uninstallString)
    {
        if (string.IsNullOrWhiteSpace(uninstallString))
            return null;

        var match = Regex.Match(uninstallString, @"\{[0-9A-Fa-f-]{36}\}");
        return match.Success ? match.Value : null;
    }

    private static void AddInstallation(ScanResult result, SdkInstallation installation)
    {
        var exists = result.Installations.Any(existing =>
            existing.Version.Equals(installation.Version, StringComparison.OrdinalIgnoreCase) &&
            PathsEqual(existing.RootPath, installation.RootPath));

        if (!exists)
            result.Installations.Add(installation);
    }

    private static bool IsAccessOrIoException(Exception exception) =>
        exception is UnauthorizedAccessException or
        System.Security.SecurityException or
        IOException;

    private static void Report(Action<string>? progress, string message) =>
        progress?.Invoke(message);

    private sealed class StringTupleComparer :
        IEqualityComparer<(string DisplayName, string DisplayVersion, string? ProductCode)>
    {
        public static readonly StringTupleComparer Instance = new();

        public bool Equals(
            (string DisplayName, string DisplayVersion, string? ProductCode) x,
            (string DisplayName, string DisplayVersion, string? ProductCode) y) =>
            string.Equals(x.DisplayName, y.DisplayName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.DisplayVersion, y.DisplayVersion, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ProductCode, y.ProductCode, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string DisplayName, string DisplayVersion, string? ProductCode) value) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.DisplayName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.DisplayVersion),
                value.ProductCode is null
                    ? 0
                    : StringComparer.OrdinalIgnoreCase.GetHashCode(value.ProductCode));
    }
}
