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

        int previewIdx = Array.IndexOf(args, "--config-preview");
        if (previewIdx >= 0 && previewIdx + 1 < args.Length)
        {
            DebugConfigPreview(args[previewIdx + 1]);
            return;
        }

        Application.Run(new UI.TrayAppContext());
    }

    // Hidden diagnostic: render the config window to a PNG (sample data) for visual review.
    private static void DebugConfigPreview(string path)
    {
        var controller = new Core.AppController();
        controller.Store.Load();
        if (controller.Store.Config.Profiles.Count == 0)
        {
            controller.Store.Config.Profiles.Add(new Profiles.Profile
            {
                Name = "VR — Index", OutputName = "Headphones (Valve Index)",
                MicName = "Microphone (Valve Index)", HmdModel = "Index", AutoSwitchOnHmd = true,
            });
            controller.Store.Config.Profiles.Add(new Profiles.Profile
            {
                Name = "Desktop", OutputName = "Speakers (Realtek)", MicName = "Mic (GoXLR)",
            });
        }
        using var hotkeys = new Hotkeys.HotkeyManager();
        using var form = new UI.ConfigForm(controller, hotkeys);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(-3000, -3000);
        form.Show();
        Application.DoEvents();
        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        form.Close();
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