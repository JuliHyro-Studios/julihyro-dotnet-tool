using System.Diagnostics;
using System.Security.Principal;

namespace JuliHyro.DotNetTool.Shared.Security;

/// <summary>
/// Enforces the product's mandatory elevated-process policy without depending on a UI.
/// </summary>
public static class AdministratorGuard
{
    /// <summary>
    /// Returns whether the current Windows process belongs to the Administrators group.
    /// </summary>
    public static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Attempts to relaunch the current application with UAC elevation.
    /// No console output or other presentation behavior is performed here.
    /// </summary>
    public static bool EnsureElevated()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        if (IsAdministrator())
            return true;

        try
        {
            var executable = System.Environment.ProcessPath;
            var arguments = System.Environment.GetCommandLineArgs();
            var entry = arguments.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(executable) ||
                string.IsNullOrWhiteSpace(entry))
                return false;

            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = System.Environment.CurrentDirectory
            };

            if (Path.GetFileNameWithoutExtension(executable).Equals(
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
            {
                start.ArgumentList.Add(entry);
            }

            foreach (var argument in arguments.Skip(1))
                start.ArgumentList.Add(argument);

            using var elevated = Process.Start(start);
            return elevated is not null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // UAC was denied or the elevation request could not be started.
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Revalidates the current process token immediately before a privileged operation.
    /// </summary>
    public static void DemandAdministrator(string checkpoint)
    {
        if (!IsAdministrator())
        {
            throw new UnauthorizedAccessException(
                $"Security checkpoint failed: {checkpoint}. Administrator privileges are required.");
        }
    }
}
