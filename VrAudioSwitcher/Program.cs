namespace VrAudioSwitcher;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Tray application context is wired up in a later step.
        Application.Run(new ApplicationContext());
    }
}