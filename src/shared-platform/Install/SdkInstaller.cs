using System.Diagnostics;
using JuliHyro.DotNetTool.Shared.Models;
using JuliHyro.DotNetTool.Shared.Security;

namespace JuliHyro.DotNetTool.Shared.Install;

/// <summary>
/// Downloads and launches Microsoft's official Windows x64 SDK installer.
/// </summary>
public sealed class SdkInstaller(HttpClient httpClient)
{
    public async Task<int> InstallDefaultAsync(SdkRelease release, CancellationToken cancellationToken = default)
    {
        AdministratorGuard.DemandAdministrator("before default installer download");
        var tempFile = Path.Combine(Path.GetTempPath(), $"dotnet-sdk-{release.Version}-win-x64.exe");
        try
        {
            await DownloadAsync(release.InstallerUri, tempFile, cancellationToken);
            AdministratorGuard.DemandAdministrator("before launching the default installer");

            using var process = Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true })
                ?? throw new InvalidOperationException("The SDK installer process could not be started.");

            Console.WriteLine($"[MONITOR] Installer started (PID {process.Id}). Waiting for it to finish...");
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { /* Windows may still hold the installer briefly. */ }
        }
    }

    public async Task InstallCustomAsync(SdkRelease release, string installDirectory, CancellationToken cancellationToken = default)
    {
        AdministratorGuard.DemandAdministrator("before custom SDK installation");
        var fullPath = Path.GetFullPath(installDirectory);
        Directory.CreateDirectory(fullPath);

        var script = Path.Combine(Path.GetTempPath(), "dotnet-install.ps1");
        using (var response = await httpClient.GetAsync("https://dot.net/v1/dotnet-install.ps1", cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(script);
            await stream.CopyToAsync(output, cancellationToken);
        }

        AdministratorGuard.DemandAdministrator("before executing dotnet-install.ps1");
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
                     "-Version", release.Version, "-Architecture", "x64", "-InstallDir", fullPath })
            start.ArgumentList.Add(arg);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the custom installer.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        while (!process.HasExited)
        {
            AdministratorGuard.DemandAdministrator("while custom installer is running");
            await Task.Delay(500, cancellationToken);
        }

        Console.Write(await stdout);
        var errorText = await stderr;
        if (!string.IsNullOrWhiteSpace(errorText))
            Console.Error.WriteLine(errorText);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"dotnet-install.ps1 exited with code {process.ExitCode}.");

        // This marker lets the scanner recognize custom installs managed by JH-DTM.
        var markerPath = Path.Combine(fullPath, ".jh-dtm-install.json");
        var marker = System.Text.Json.JsonSerializer.Serialize(new
        {
            Product = "JH-DTM",
            Version = release.Version,
            InstalledAtUtc = DateTimeOffset.UtcNow
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(markerPath, marker, cancellationToken);
    }

    private async Task DownloadAsync(Uri uri, string destination, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, cancellationToken);
    }
}
