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
            progress?.Invoke($"[DOWNLOAD] Source : {release.InstallerUri}");
            progress?.Invoke($"[DOWNLOAD] Target : {tempFile}");
            await DownloadAsync(
                release.InstallerUri,
                tempFile,
                progress,
                "Official SDK installer",
                cancellationToken);
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
            progress?.Invoke("[DOWNLOAD] Source : https://dot.net/v1/dotnet-install.ps1");
            progress?.Invoke($"[DOWNLOAD] Target : {script}");

            await DownloadAsync(
                new Uri("https://dot.net/v1/dotnet-install.ps1"),
                script,
                progress,
                "dotnet-install.ps1",
                cancellationToken);

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
        Action<string>? progress,
        string downloadLabel,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        var directory = Path.GetDirectoryName(destination);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var buffer = new byte[81920];
        long downloadedBytes = 0;
        long lastReportedBytes = 0;
        var stopwatch = Stopwatch.StartNew();
        var reportInterval = TimeSpan.FromMilliseconds(500);

        progress?.Invoke($"[DOWNLOAD] Stage: Connecting | {downloadLabel}");

        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
                break;

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            downloadedBytes += read;

            if (stopwatch.Elapsed >= reportInterval ||
                (totalBytes.HasValue && downloadedBytes >= totalBytes.Value))
            {
                var elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
                var bytesPerSecond = (downloadedBytes - lastReportedBytes) / elapsedSeconds;
                var speed = FormatDataSize(bytesPerSecond) + "/s";
                var downloaded = FormatDataSize(downloadedBytes);

                if (totalBytes is > 0)
                {
                    var percentage = Math.Clamp(
                        downloadedBytes * 100d / totalBytes.Value,
                        0d,
                        100d);

                    var total = FormatDataSize(totalBytes.Value);
                    var bar = BuildProgressBar(percentage, 28);

                    progress?.Invoke(
                        $"[DOWNLOAD] {downloadLabel} {bar} {percentage,6:F2}% | " +
                        $"{downloaded} / {total} | {speed}");
                }
                else
                {
                    progress?.Invoke(
                        $"[DOWNLOAD] {downloadLabel} {downloaded} downloaded | " +
                        $"{speed} | Total size: unknown");
                }

                lastReportedBytes = downloadedBytes;
                stopwatch.Restart();
            }
        }

        await target.FlushAsync(cancellationToken);

        if (downloadedBytes == 0)
            throw new InvalidOperationException(
                $"The download of {downloadLabel} completed with an empty file.");

        if (totalBytes.HasValue && downloadedBytes != totalBytes.Value)
        {
            throw new IOException(
                $"The download of {downloadLabel} was incomplete. " +
                $"Expected {totalBytes.Value} bytes but received {downloadedBytes} bytes.");
        }

        progress?.Invoke(
            $"[DOWNLOAD] Stage: Completed | {downloadLabel} | " +
            $"{FormatDataSize(downloadedBytes)} saved to {destination}");
    }

    private static string BuildProgressBar(double percentage, int width)
    {
        var filled = (int)Math.Round(percentage / 100d * width);
        filled = Math.Clamp(filled, 0, width);
        return "[" + new string('#', filled) + new string('-', width - filled) + "]";
    }

    private static string FormatDataSize(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = Math.Max(bytes, 0);
        var unitIndex = 0;

        while (size >= 1024d && unitIndex < units.Length - 1)
        {
            size /= 1024d;
            unitIndex++;
        }

        return $"{size:F2} {units[unitIndex]}";
    }
}
