using System.Diagnostics;
using JuliHyro.DotNetTool.Shared.Models;
using JuliHyro.DotNetTool.Shared.Security;

namespace JuliHyro.DotNetTool.Shared.Install;

/// <summary>
/// Downloads and launches Microsoft's official Windows x64 SDK installers.
/// Progress is reported through a callback so this service remains UI-independent.
/// </summary>
public sealed class SdkInstaller(HttpClient httpClient)
{
    /// <summary>
    /// Downloads and launches the official default Windows installer.
    /// </summary>
    public async Task<int> InstallDefaultAsync(
        SdkRelease release,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        AdministratorGuard.DemandAdministrator("before default installer download");
        var tempFile = Path.Combine(
            Path.GetTempPath(),
            $"dotnet-sdk-{release.Version}-win-x64.exe");

        try
        {
            progress?.Invoke("[DOWNLOAD] Downloading the official SDK installer...");
            await DownloadAsync(release.InstallerUri, tempFile, cancellationToken);
            AdministratorGuard.DemandAdministrator("before launching the default installer");

            using var process = Process.Start(
                new ProcessStartInfo(tempFile) { UseShellExecute = true })
                ?? throw new InvalidOperationException(
                    "The SDK installer process could not be started.");

            progress?.Invoke(
                $"[MONITOR] Installer started (PID {process.Id}). Waiting for it to finish...");

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch (IOException)
            {
                // Windows may still hold the installer briefly after process exit.
            }
            catch (UnauthorizedAccessException)
            {
                // Temporary-file cleanup must not hide the installer result.
            }
        }
    }

    /// <summary>
    /// Installs an SDK into a caller-selected directory using Microsoft's install script.
    /// </summary>
    public async Task InstallCustomAsync(
        SdkRelease release,
        string installDirectory,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        AdministratorGuard.DemandAdministrator("before custom SDK installation");
        var fullPath = Path.GetFullPath(installDirectory);
        Directory.CreateDirectory(fullPath);

        var script = Path.Combine(Path.GetTempPath(), "dotnet-install.ps1");
        try
        {
            progress?.Invoke("[DOWNLOAD] Downloading Microsoft's dotnet-install.ps1...");

            using (var response = await httpClient.GetAsync(
                "https://dot.net/v1/dotnet-install.ps1",
                cancellationToken))
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

            foreach (var argument in new[]
            {
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                script,
                "-Version",
                release.Version,
                "-Architecture",
                "x64",
                "-InstallDir",
                fullPath
            })
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException(
                    "Could not start the custom SDK installer.");

            progress?.Invoke(
                $"[MONITOR] Custom installer started (PID {process.Id}).");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            while (!process.HasExited)
            {
                AdministratorGuard.DemandAdministrator(
                    "while custom installer is running");
                await Task.Delay(500, cancellationToken);
            }

            var outputText = await stdoutTask;
            var errorText = await stderrTask;

            if (!string.IsNullOrWhiteSpace(outputText))
                progress?.Invoke(outputText.TrimEnd());

            if (!string.IsNullOrWhiteSpace(errorText))
                progress?.Invoke(errorText.TrimEnd());

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"dotnet-install.ps1 exited with code {process.ExitCode}.");
            }

            var markerPath = Path.Combine(fullPath, ".jh-dtm-install.json");
            var marker = System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    Product = "JH-DTM",
                    Version = release.Version,
                    InstalledAtUtc = DateTimeOffset.UtcNow
                },
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

            await File.WriteAllTextAsync(markerPath, marker, cancellationToken);
            progress?.Invoke("[INSTALL] JH-DTM ownership marker created.");
        }
        finally
        {
            try
            {
                if (File.Exists(script))
                    File.Delete(script);
            }
            catch (IOException)
            {
                // A temporary script cleanup failure does not replace the install result.
            }
            catch (UnauthorizedAccessException)
            {
                // A temporary script cleanup failure does not replace the install result.
            }
        }
    }

    private async Task DownloadAsync(
        Uri uri,
        string destination,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, cancellationToken);
    }
}
