using JuliHyro.DotNetTool.Shared.Environment;
using JuliHyro.DotNetTool.Shared.Install;
using JuliHyro.DotNetTool.Shared.Metadata;
using JuliHyro.DotNetTool.Shared.Models;
using JuliHyro.DotNetTool.Shared.Networking;
using JuliHyro.DotNetTool.Shared.Security;
using JuliHyro.DotNetTool.Shared.Uninstall;

namespace JuliHyro.DotNetTool.ConsoleEdition.Shells;

/// <summary>
/// Main menu and presentation workflows for the console edition.
/// Shared Platform supplies data and operations but never reads or writes the console.
/// </summary>
public sealed class MainShell : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly DotNetEnvironmentScanner _scanner = new();
    private readonly SdkUninstaller _uninstaller = new();
    private readonly NetworkChecker _network;
    private readonly SdkReleaseResolver _resolver;
    private readonly SdkInstaller _installer;

    public MainShell()
    {
        _network = new NetworkChecker(_http);
        _resolver = new SdkReleaseResolver(_http);
        _installer = new SdkInstaller(_http);
    }

    public async Task RunAsync()
    {
        while (true)
        {
            AdministratorGuard.DemandAdministrator("MainShell menu");
            PrintMenu();
            Console.Write($"[{Environment.UserName}@{Environment.MachineName}] MainShell> ");
            var choice = Console.ReadLine()?.Trim();

            if (choice == "4")
            {
                Console.WriteLine("JH-DTM closed.");
                return;
            }

            try
            {
                AdministratorGuard.DemandAdministrator($"before option {choice}");
                switch (choice)
                {
                    case "0":
                        await InstallAsync(custom: false);
                        break;
                    case "1":
                        await InstallAsync(custom: true);
                        break;
                    case "2":
                        ScanSdkPaths();
                        break;
                    case "3":
                        UninstallSdk();
                        break;
                    default:
                        Console.WriteLine("[INVALID] Choose 0, 1, 2, 3 or 4.");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[CANCELLED] The current operation was cancelled.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.Write("Press Enter to return to MainShell...");
            Console.ReadLine();
        }
    }

    private static void PrintMenu()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine($"===== {ProductMetadata.ShortName} v{ProductMetadata.Version} =====");
        Console.WriteLine("[0] Install New .NET SDK - Default Installer");
        Console.WriteLine("[1] Install New .NET SDK - Custom Directory");
        Console.WriteLine("[2] Find .NET SDK Paths (Smart Discovery + Optional Deep Scan)");
        Console.WriteLine("[3] Uninstall .NET SDK (Custom + MSI)");
        Console.WriteLine("[4] Exit");
        Console.WriteLine("================================");
        Console.WriteLine($"{ProductMetadata.Copyright} | {ProductMetadata.Author}");
        Console.ResetColor();
    }

    private async Task InstallAsync(bool custom)
    {
        AdministratorGuard.DemandAdministrator("installer workflow entry");
        Console.Write("SDK version (exact e.g. 10.0.403, or channel e.g. 10.0): ");
        var requested = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requested))
            throw new ArgumentException("A version is required.");

        string? targetDirectory = null;
        if (custom)
        {
            Console.Write("Custom install directory: ");
            targetDirectory = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(targetDirectory))
                throw new ArgumentException("A custom directory is required.");

            targetDirectory = Path.GetFullPath(targetDirectory);
            if (File.Exists(targetDirectory))
                throw new IOException("The selected target exists as a file.");

            if (Directory.Exists(targetDirectory) &&
                Directory.EnumerateFileSystemEntries(targetDirectory).Any())
            {
                throw new IOException(
                    "The custom target directory is not empty. Choose an empty directory to avoid overwriting another installation.");
            }

            Directory.CreateDirectory(targetDirectory);
            AdministratorGuard.DemandAdministrator("after custom directory validation");
        }

        Console.WriteLine("[NETWORK] Checking access to Microsoft's .NET release metadata...");
        if (!await _network.HasInternetAccessAsync())
            throw new InvalidOperationException("Network check failed. Installation was cancelled.");

        Console.WriteLine("[VERSION] Resolving requested SDK...");
        var release = await _resolver.ResolveAsync(requested);
        Console.WriteLine($"[VERSION] Resolved: {release.Version}");

        Console.WriteLine("[ENVIRONMENT] Checking existing SDK installations...");
        var existing = _scanner.DiscoverStandard().Installations;

        foreach (var item in existing)
        {
            Console.WriteLine(
                $"  [{(item.IsRegistered ? "REGISTERED" : "FOUND")}] {item.Version} | {item.RootPath}");
        }

        if (existing.Any(item => item.Version.Equals(
            release.Version,
            StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"SDK {release.Version} already exists in a discovered installation. No changes were made.");
        }

        AdministratorGuard.DemandAdministrator("immediately before installation");
        if (custom)
        {
            Console.WriteLine($"[INSTALL] Custom directory: {targetDirectory}");
            await _installer.InstallCustomAsync(
                release,
                targetDirectory!,
                message => ReportInstallProgress(message));
            EnvironmentVariableManager.RegisterUserRoot(targetDirectory!);
        }
        else
        {
            Console.WriteLine("[INSTALL] Launching the official default installer...");
            var exitCode = await _installer.InstallDefaultAsync(
                release,
                message => ReportInstallProgress(message));
            if (exitCode == 1602)
            {
                Console.WriteLine("[CANCELLED] The installer was cancelled by the user.");
                return;
            }

            if (exitCode == 3010)
            {
                Console.WriteLine("[SUCCESS] Installation completed. Windows requested a restart.");
            }
            else if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"The official installer returned exit code {exitCode}.");
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(
            $"[SUCCESS] .NET SDK {release.Version} installation process completed.");
        Console.ResetColor();
    }

    /// <summary>
    /// Runs standard discovery first. A recursive scan is offered only when no SDK is found.
    /// </summary>
    private void ScanSdkPaths()
    {
        AdministratorGuard.DemandAdministrator("before standard SDK discovery");

        Console.WriteLine("[SCAN] Starting standard SDK discovery...");
        var standard = _scanner.DiscoverStandard(
            message => Console.WriteLine($"[SCAN] {message}"));

        if (standard.Installations.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("[RESULT] Standard discovery found SDK installations.");
            PrintScanResult(standard);
            Console.WriteLine("[INFO] Deep Scan was skipped because an SDK was found.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("===== SDK DISCOVERY RESULT =====");
        Console.WriteLine("[RESULT] No .NET SDK installations were found.");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("What would you like to do?");
            Console.WriteLine("[1] Start Deep Scan (All Available Data Drives)");
            Console.WriteLine("[2] Retry Standard Discovery");
            Console.WriteLine("[3] Select Specific Data Drive");
            Console.WriteLine("[4] Cancel and Return");
            Console.WriteLine();
            Console.Write("[DiscoveryAction]> ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1":
                {
                    var deepResult = RunDeepScan(selectedDrive: null);
                    PrintScanResult(deepResult);
                    return;
                }
                case "2":
                {
                    AdministratorGuard.DemandAdministrator("before retrying standard discovery");
                    Console.WriteLine("[SCAN] Retrying standard discovery...");
                    standard = _scanner.DiscoverStandard(
                        message => Console.WriteLine($"[SCAN] {message}"));

                    if (standard.Installations.Count > 0)
                    {
                        Console.WriteLine("[RESULT] SDK installation(s) found.");
                        PrintScanResult(standard);
                        return;
                    }

                    Console.WriteLine("[RESULT] Still no SDK installations found.");
                    break;
                }
                case "3":
                {
                    var selectedDrive = SelectDataDrive();
                    if (selectedDrive is null)
                    {
                        Console.WriteLine("[INFO] No drive selected. Returning to discovery actions.");
                        break;
                    }

                    var deepResult = RunDeepScan(selectedDrive);
                    PrintScanResult(deepResult);
                    return;
                }
                case "4":
                    Console.WriteLine("[CANCELLED] Returning to MainShell.");
                    return;
                default:
                    Console.WriteLine("[ERROR] Invalid selection. Choose 1, 2, 3 or 4.");
                    break;
            }

            Console.WriteLine();
        }
    }

    private ScanResult RunDeepScan(string? selectedDrive)
    {
        AdministratorGuard.DemandAdministrator("immediately before deep scan");

        if (selectedDrive is null)
        {
            Console.WriteLine(
                "[SCAN] Deep Scan will inspect non-system data drives only. The Windows system drive will not be recursively scanned.");
        }
        else
        {
            Console.WriteLine(
                $"[SCAN] Deep Scan will inspect {selectedDrive}. The Windows system drive is excluded.");
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            return _scanner.ScanDeep(
                scanAllDrives: true,
                progress: message => Console.WriteLine($"[SCAN] {message}"),
                cancellationToken: cancellation.Token,
                selectedDrive: selectedDrive);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static string? SelectDataDrive()
    {
        var windowsDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.Windows);
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

                availableDrives.Add(drive);
            }
            catch (UnauthorizedAccessException)
            {
                // Do not offer inaccessible drives.
            }
            catch (System.Security.SecurityException)
            {
                // Do not offer inaccessible drives.
            }
            catch (IOException)
            {
                // Removable media may disappear during enumeration.
            }
        }

        var drives = availableDrives
            .OrderBy(drive => drive.DriveType == DriveType.Fixed ? 0 : 1)
            .ToArray();

        if (drives.Length == 0)
        {
            Console.WriteLine(
                "[INFO] No additional data drives are available. The Windows system drive is intentionally excluded.");
            return null;
        }

        Console.WriteLine();
        Console.WriteLine("===== AVAILABLE DATA DRIVES =====");
        foreach (var drive in drives)
        {
            var sizeGb = drive.TotalSize / (1024L * 1024 * 1024);
            Console.WriteLine($"[{drive.Name}] {drive.DriveType} | {sizeGb:N0} GB");
        }

        Console.WriteLine();
        Console.Write("Enter drive letter (example: D): ");
        var input = Console.ReadLine()?.Trim().TrimEnd('\\', ':');
        if (string.IsNullOrWhiteSpace(input) || input.Length != 1 || !char.IsLetter(input[0]))
            return null;

        var requested = $"{char.ToUpperInvariant(input[0])}:\\";
        return drives.FirstOrDefault(drive => PathsEqual(drive.Name, requested))?.Name;
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            var normalizedLeft = Path.GetFullPath(left);
            var normalizedRight = Path.GetFullPath(right);
            return string.Equals(
                normalizedLeft.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                normalizedRight.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void PrintScanResult(ScanResult scan)
    {
        Console.WriteLine();
        Console.WriteLine("===== SDK DISCOVERY RESULT =====");
        Console.WriteLine($"Directories visited: {scan.DirectoriesVisited:N0}");
        Console.WriteLine($"Drives scanned: {(scan.ScannedDrives.Count == 0 ? "None" : string.Join(", ", scan.ScannedDrives))}");

        Console.WriteLine("-- DOTNET ROOTS --");
        foreach (var root in scan.DotNetRoots)
            Console.WriteLine($"[ROOT] {root}");

        Console.WriteLine("-- SDK INSTALLATIONS --");
        foreach (var sdk in scan.Installations)
        {
            var label = sdk.IsRegistered ? "REGISTERED" : "DISCOVERED";
            Console.WriteLine($"[{label}] {sdk.Version} | {sdk.RootPath} | {sdk.Source}");
        }

        Console.WriteLine("-- PATH ENTRIES --");
        foreach (var path in scan.PathEntries)
            Console.WriteLine($"[PATH] {path}");

        Console.WriteLine("-- REGISTRY --");
        foreach (var key in scan.RegistryLocations)
            Console.WriteLine($"[REGISTRY] {key}");

        Console.WriteLine("-- REGISTERED SDK PRODUCTS --");
        foreach (var product in scan.MsiProducts)
        {
            Console.WriteLine(
                $"[PRODUCT] {product.DisplayName} | {product.DisplayVersion} | ProductCode: {product.ProductCode ?? "Unavailable"}");
        }

        foreach (var warning in scan.Warnings)
            Console.WriteLine($"[WARNING] {warning}");

        if (scan.Installations.Count == 0)
            Console.WriteLine("[INFO] No SDK installations were found.");
    }


    private void UninstallSdk()
    {
        AdministratorGuard.DemandAdministrator("before uninstall inventory discovery");

        Console.WriteLine("[INVENTORY] Checking registered and known .NET SDK locations...");
        var scan = _scanner.DiscoverStandard(
            message => Console.WriteLine($"[INVENTORY] {message}"));

        var portable = scan.Installations
            .Where(item => item.IsPortable)
            .ToList();

        // A full data-drive scan is optional and only used to locate
        // JH-DTM-managed custom installations outside known locations.
        if (portable.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("[INVENTORY] No JH-DTM-managed custom SDK was found in known locations.");
            Console.Write("Search non-system data drives for managed custom SDKs? [y/N]: ");

            var searchCustom = Console.ReadLine()?.Trim();

            if (string.Equals(searchCustom, "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(searchCustom, "yes", StringComparison.OrdinalIgnoreCase))
            {
                AdministratorGuard.DemandAdministrator(
                    "immediately before custom SDK inventory deep scan");

                scan = _scanner.ScanDeep(
                    scanAllDrives: true,
                    progress: message => Console.WriteLine($"[DEEP SCAN] {message}"));

                portable = scan.Installations
                    .Where(item => item.IsPortable)
                    .ToList();
            }
        }

        var msiProducts = scan.MsiProducts
            .Where(product => !string.IsNullOrWhiteSpace(product.ProductCode))
            .ToList();

        Console.WriteLine();
        Console.WriteLine("==============================================================");
        Console.WriteLine("                    .NET SDK UNINSTALL INVENTORY");
        Console.WriteLine("==============================================================");

        if (portable.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("JH-DTM MANAGED CUSTOM SDKs");
            Console.WriteLine(new string('-', 78));
            Console.WriteLine(
                string.Format("{0,-5} {1,-18} {2,-52}",
                    "ID", "SDK VERSION", "INSTALLATION DIRECTORY"));
            Console.WriteLine(new string('-', 78));

            for (var index = 0; index < portable.Count; index++)
            {
                var item = portable[index];
                Console.WriteLine(
                    string.Format("{0,-5} {1,-18} {2,-52}",
                        $"P{index + 1}",
                        FormatTableCell(item.Version, 18),
                        FormatTableCell(item.RootPath, 52)));
            }

            Console.WriteLine(new string('-', 78));
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("JH-DTM MANAGED CUSTOM SDKs");
            Console.WriteLine("  None found.");
        }

        if (msiProducts.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("DEFAULT INSTALLER / WINDOWS INSTALLER SDKs");
            Console.WriteLine(new string('-', 103));
            Console.WriteLine(
                string.Format("{0,-5} {1,-42} {2,-20} {3,-33}",
                    "ID", "PRODUCT", "VERSION", "PRODUCT CODE"));
            Console.WriteLine(new string('-', 103));

            for (var index = 0; index < msiProducts.Count; index++)
            {
                var product = msiProducts[index];
                Console.WriteLine(
                    string.Format("{0,-5} {1,-42} {2,-20} {3,-33}",
                        $"M{index + 1}",
                        FormatTableCell(product.DisplayName, 42),
                        FormatTableCell(product.DisplayVersion, 20),
                        FormatTableCell(product.ProductCode ?? "Unavailable", 33)));
            }

            Console.WriteLine(new string('-', 103));
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("DEFAULT INSTALLER / WINDOWS INSTALLER SDKs");
            Console.WriteLine("  None found.");
        }

        if (portable.Count == 0 && msiProducts.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("[RESULT] No removable .NET SDK products were found.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("SELECTION");
        Console.WriteLine("  P<number>  Remove a JH-DTM-managed custom SDK.");
        Console.WriteLine("  M<number>  Uninstall a registered Windows Installer product.");
        Console.WriteLine("  0          Return without making changes.");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Selection (");
            var validChoices = new List<string>();

            if (portable.Count > 0)
                validChoices.Add($"P1-P{portable.Count}");

            if (msiProducts.Count > 0)
                validChoices.Add($"M1-M{msiProducts.Count}");

            validChoices.Add("0");
            Console.Write(string.Join(", ", validChoices));
            Console.Write(")> ");

            var selection = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(selection) ||
                string.Equals(selection, "0", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[CANCELLED] No SDK was changed.");
                return;
            }

            if (selection.StartsWith('P') &&
                int.TryParse(selection[1..], out var portableIndex))
            {
                if (portableIndex < 1 || portableIndex > portable.Count)
                {
                    Console.WriteLine(
                        $"[INVALID] {selection} is unavailable. Valid custom SDK selections: " +
                        (portable.Count == 0 ? "none" : $"P1-P{portable.Count}") +
                        ". Enter 0 to cancel.");
                    continue;
                }

                var selected = portable[portableIndex - 1];

                Console.WriteLine();
                Console.WriteLine("CUSTOM SDK REMOVAL");
                Console.WriteLine($"  Version : {selected.Version}");
                Console.WriteLine($"  Location: {selected.RootPath}");
                Console.Write("Type REMOVE to confirm deletion of this SDK version: ");

                if (!string.Equals(
                    Console.ReadLine(),
                    "REMOVE",
                    StringComparison.Ordinal))
                {
                    Console.WriteLine("[CANCELLED] No files were removed.");
                    return;
                }

                AdministratorGuard.DemandAdministrator(
                    "immediately before custom SDK removal");

                _uninstaller.RemovePortableSdk(selected);
                Console.WriteLine("[SUCCESS] Custom SDK version removed.");
                return;
            }

            if (selection.StartsWith('M') &&
                int.TryParse(selection[1..], out var msiIndex))
            {
                if (msiIndex < 1 || msiIndex > msiProducts.Count)
                {
                    Console.WriteLine(
                        $"[INVALID] {selection} is unavailable. Valid Windows Installer selections: " +
                        (msiProducts.Count == 0 ? "none" : $"M1-M{msiProducts.Count}") +
                        ". Enter 0 to cancel.");
                    continue;
                }

                var selected = msiProducts[msiIndex - 1];

                Console.WriteLine();
                Console.WriteLine("WINDOWS INSTALLER REMOVAL");
                Console.WriteLine($"  Product     : {selected.DisplayName}");
                Console.WriteLine($"  Version     : {selected.DisplayVersion}");
                Console.WriteLine($"  Product Code: {selected.ProductCode}");
                Console.WriteLine(
                    "  Note: Windows Installer may remove the complete SDK product,");
                Console.WriteLine(
                    "        not just one SDK version folder.");
                Console.Write("Type UNINSTALL to launch Windows Installer: ");

                if (!string.Equals(
                    Console.ReadLine(),
                    "UNINSTALL",
                    StringComparison.Ordinal))
                {
                    Console.WriteLine("[CANCELLED] No uninstall was started.");
                    return;
                }

                AdministratorGuard.DemandAdministrator(
                    "immediately before MSI uninstall");

                var exitCode = _uninstaller
                    .UninstallMsiProductAsync(selected)
                    .GetAwaiter()
                    .GetResult();

                if (exitCode == 1602)
                {
                    Console.WriteLine("[CANCELLED] Windows Installer operation was cancelled. No success was reported.");
                    return;
                }

                if (exitCode is not (0 or 3010))
                {
                    throw new InvalidOperationException(
                        $"Windows Installer returned exit code {exitCode}.");
                }

                Console.WriteLine(exitCode == 3010
                    ? "[SUCCESS] Uninstall completed. Windows requested a restart."
                    : "[SUCCESS] Windows Installer completed the uninstall.");
                return;
            }

            Console.WriteLine(
                "[INVALID] Enter one of the listed IDs exactly (for example M1), or 0 to cancel.");
        }
    }

    private static string FormatTableCell(string? value, int width)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Length <= width)
            return value;

        if (width <= 3)
            return value[..width];

        return value[..(width - 3)] + "...";
    }

    private static void ReportInstallProgress(string message)
    {
        if (message.StartsWith("[DOWNLOAD]", StringComparison.Ordinal))
        {
            Console.Write("\\r" + message.PadRight(Math.Max(Console.WindowWidth - 1, message.Length)));
            if (message.Contains("Stage: Completed", StringComparison.Ordinal))
                Console.WriteLine();
            return;
        }

        Console.WriteLine(message);
    }

    public void Dispose() => _http.Dispose();
}
