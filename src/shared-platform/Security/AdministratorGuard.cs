using System.Diagnostics;
using System.Security.Principal;

namespace JuliHyro.DotNetTool.Shared.Security;

/// <summary>
/// Enforces the product's mandatory elevated-process policy.
/// A denied UAC request is a hard stop; there is no unelevated fallback.
/// </summary>
public static class AdministratorGuard
{
    public static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Relaunches this entry assembly with UAC when needed.
    /// Returns false when elevation was denied or could not be started.
    /// </summary>
    public static bool EnsureElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("[FATAL] JH-DTM currently supports Windows only.");
            return false;
        }

        if (IsAdministrator())
            return true;

        try
        {
            var executable = Environment.ProcessPath;
            var entry = Environment.GetCommandLineArgs().FirstOrDefault();
            if (string.IsNullOrWhiteSpace(executable) || string.IsNullOrWhiteSpace(entry))
                return false;

            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Environment.CurrentDirectory
            };

            // For framework-dependent execution, the process path is dotnet.exe
            // and the entry assembly must be passed as its first argument.
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(entry);

            foreach (var argument in Environment.GetCommandLineArgs().Skip(1))
                start.ArgumentList.Add(argument);

            using var elevated = Process.Start(start);
            return elevated is not null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine("[ACCESS DENIED] Administrator approval is required. JH-DTM is closing.");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL] Could not request elevation: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Revalidates the current process token immediately before an operation.
    /// This is a checkpoint, not a claim that elevation makes arbitrary code safe.
    /// </summary>
    public static void DemandAdministrator(string checkpoint)
    {
        if (!IsAdministrator())
            throw new UnauthorizedAccessException($"Security checkpoint failed: {checkpoint}. Administrator privileges are required.");
    }
}
