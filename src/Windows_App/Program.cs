namespace WaterCoolingDevice;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(argument => string.Equals(
                argument, HardwareTemperatureAgentOptions.AgentArgument,
                StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = HardwareTemperatureAgent.Run();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
