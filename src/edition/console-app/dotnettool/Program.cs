using shared_platform;

public static class Program
{
    public static void Main()
    {
        List<ShellOption> options = new()
        {
            new ShellOption(
                "Start",
                () =>
                {
                    Console.WriteLine("Starting application...");
                }
            ),
            new ShellOption(
                "Settings",
                () =>
                {
                    Console.WriteLine("Opening settings...");
                }
            ),
            new ShellOption(
                "About",
                () =>
                {
                    Console.WriteLine("Application version 1.0");
                }
            ),
            new ShellOption(
                "Exit",
                () =>
                {
                    Console.WriteLine("Exiting...");
                }
            ),
        };

        ShellOption? result = LibAPI.BuildShellPrompt(
            title: "MY APPLICATION",
            style: "========================",
            desc: "Welcome to my shell application.",
            prompt: "root@system:~$ ",
            columns: 3,
            options: options,
            onReturn: selected =>
            {
                Console.WriteLine($"Selected option: {selected.Name}");
            }
        );

        if (result != null)
        {
            Console.WriteLine($"Returned: {result.Name}");
        }
    }
}
