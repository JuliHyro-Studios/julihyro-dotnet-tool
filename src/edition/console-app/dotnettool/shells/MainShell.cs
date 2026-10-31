using JuliHyro.DotNetTool.Shared.Environment;
using JuliHyro.DotNetTool.Shared.Install;
using JuliHyro.DotNetTool.Shared.Metadata;
using JuliHyro.DotNetTool.Shared.Models;
using JuliHyro.DotNetTool.Shared.Networking;
using JuliHyro.DotNetTool.Shared.Security;
using JuliHyro.DotNetTool.Shared.Uninstall;

namespace JuliHyro.DotNetTool.ConsoleEdition.Shells;

/// <summary>
/// Main menu for the console edition. Business logic stays in shared-platform.
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
                // Treat every menu selection as untrusted until this checkpoint passes.
                AdministratorGuard.DemandAdministrator($"before option {choice}");
                switch (choice)
                {
                    case "0": await InstallAsync(custom: false); break;
                    case "1": await InstallAsync(custom: true); break;
                    case "2": ScanDeepEnvironment(); break;
                    case "3": UninstallSdk(); break;
                    default: Console.WriteLine("[INVALID] Choose 0, 1, 2, 3 or 4."); break;
                }
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
        Console.WriteLine("[2] Find .NET SDK Paths (Deep Scan)");
        Console.WriteLine("[3] Uninstall .NET SDK");
        Console.WriteLine("[4] Exit");
        Console.WriteLine("================================");
        Console.WriteLine($"{ProductMetadata.Copyright} | {ProductMetadata.Author}");
        Console.ResetColor();
    }

    private async Task InstallAsync(bool custom)
    {
        AdministratorGuard.DemandAdministrator("installer workflow entry");
        Console.Write("SDK version (exact e.g. 10.0.403, or channel e.g. 10.0): ");
        var requested = Console.ReadLine()?.Trim() ?? "";
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
            if (Directory.Exists(targetDirectory) && Directory.EnumerateFileSystemEntries(targetDirectory).Any())
                throw new IOException("The custom target directory is not empty. Choose an empty directory to avoid overwriting another installation.");
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
        var existing = custom
            ? _scanner.ScanDeep().Installations
            : _scanner.ScanStandard();

        foreach (var item in existing)
            Console.WriteLine($"  [{(item.IsRegistered ? "REGISTERED" : "FOUND")}] {item.Version} | {item.RootPath}");

        if (existing.Any(item => item.Version.Equals(release.Version, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"SDK {release.Version} already exists in a discovered installation. No changes were made.");

        AdministratorGuard.DemandAdministrator("immediately before installation");
        if (custom)
        {
            Console.WriteLine($"[INSTALL] Custom directory: {targetDirectory}");
            await _installer.InstallCustomAsync(release, targetDirectory!);
            EnvironmentVariableManager.RegisterUserRoot(targetDirectory!);
        }
        else
        {
            Console.WriteLine("[INSTALL] Launching the official default installer...");
            var exitCode = await _installer.InstallDefaultAsync(release);
            if (exitCode != 0)
                throw new InvalidOperationException($"The official installer returned exit code {exitCode}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] .NET SDK {release.Version} installation process completed.");
        Console.ResetColor();
    }

    private void ScanDeepEnvironment()
    {
        AdministratorGuard.DemandAdministrator("before deep environment scan");
        var scan = _scanner.ScanDeep();

        Console.WriteLine("===== DEEP ENVIRONMENT SCAN =====");
        Console.WriteLine("-- DOTNET ROOTS --");
        foreach (var root in scan.DotNetRoots) Console.WriteLine($"[ROOT] {root}");
        Console.WriteLine("-- SDK INSTALLATIONS --");
        foreach (var sdk in scan.Installations)
            Console.WriteLine($"[{(sdk.IsRegistered ? "REGISTERED" : "DISCOVERED")}] {sdk.Version} | {sdk.RootPath}");
        Console.WriteLine("-- PATH ENTRIES --");
        foreach (var path in scan.PathEntries) Console.WriteLine($"[PATH] {path}");
        Console.WriteLine("-- REGISTRY --");
        foreach (var key in scan.RegistryLocations) Console.WriteLine($"[REGISTRY] {key}");
        foreach (var warning in scan.Warnings) Console.WriteLine($"[WARNING] {warning}");
        if (scan.Installations.Count == 0) Console.WriteLine("[INFO] No SDK installations were found.");
    }

    private void UninstallSdk()
    {
        AdministratorGuard.DemandAdministrator("before SDK uninstall scan");
        var scan = _scanner.ScanDeep();
        var portable = scan.Installations.Where(x => x.IsPortable).ToList();
        if (portable.Count == 0)
        {
            Console.WriteLine("[INFO] No portable/custom SDK installations were found.");
            Console.WriteLine("Default installer SDKs are not removed by deleting files. Use Windows Installed Apps to uninstall those.");
            return;
        }

        for (var i = 0; i < portable.Count; i++)
            Console.WriteLine($"[{i + 1}] {portable[i].Version} | {portable[i].RootPath}");
        Console.Write("Select SDK number (0 to cancel): ");
        if (!int.TryParse(Console.ReadLine(), out var selection) || selection == 0)
            return;
        if (selection < 1 || selection > portable.Count)
            throw new ArgumentOutOfRangeException(nameof(selection), "Invalid SDK selection.");

        var selected = portable[selection - 1];
        Console.Write($"Remove SDK {selected.Version} from {selected.RootPath}? Type YES to confirm: ");
        if (!string.Equals(Console.ReadLine(), "YES", StringComparison.Ordinal))
        {
            Console.WriteLine("[CANCELLED] No files were removed.");
            return;
        }

        AdministratorGuard.DemandAdministrator("immediately before SDK removal");
        _uninstaller.RemovePortableSdk(selected);
        Console.WriteLine("[SUCCESS] Selected portable SDK version removed.");
    }

    public void Dispose() => _http.Dispose();
}
