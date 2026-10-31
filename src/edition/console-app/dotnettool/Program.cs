using JuliHyro.DotNetTool.Shared.Security;
using JuliHyro.DotNetTool.ConsoleEdition.Shells;

namespace JuliHyro.DotNetTool.ConsoleEdition;

/// <summary>
/// Console-edition entry point. No shell is created before elevation succeeds.
/// </summary>
public static class Program
{
    public static async Task<int> Main()
    {
        if (!AdministratorGuard.EnsureElevated())
            return 1;

        try
        {
            AdministratorGuard.DemandAdministrator("console startup");
            using var shell = new MainShell();
            await shell.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL] {ex.Message}");
            return 1;
        }
    }
}
