namespace VrAudioSwitcher;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--list-audio"))
        {
            DebugListAudio();
            return;
        }

        ApplicationConfiguration.Initialize();
        // Tray application context is wired up in a later step.
        Application.Run(new ApplicationContext());
    }

    // Hidden diagnostic: dump endpoints + current defaults to stdout.
    private static void DebugListAudio()
    {
        var audio = new Audio.AudioManager();
        Console.WriteLine("== Playback ==");
        foreach (var d in audio.ListPlaybackDevices())
            Console.WriteLine($"  {d.Name}\n    {d.Id}");
        Console.WriteLine("== Capture ==");
        foreach (var d in audio.ListCaptureDevices())
            Console.WriteLine($"  {d.Name}\n    {d.Id}");
        Console.WriteLine("== Current defaults ==");
        Console.WriteLine($"  Render/Console:        {audio.GetDefaultId(Audio.EDataFlow.Render, Audio.ERole.Console)}");
        Console.WriteLine($"  Render/Communications: {audio.GetDefaultId(Audio.EDataFlow.Render, Audio.ERole.Communications)}");
        Console.WriteLine($"  Capture/Console:       {audio.GetDefaultId(Audio.EDataFlow.Capture, Audio.ERole.Console)}");
    }
}