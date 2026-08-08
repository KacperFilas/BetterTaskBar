using System.Text.Json;

namespace BetterTaskBar;

public sealed class AppSettings
{
    public double RevealSeconds { get; set; } = 5;
    public bool WinKeyReveals { get; set; } = true;
    public bool UseCustomHotkey { get; set; }
    public int CustomHotkeyCode { get; set; }
    public int CustomHotkeyModifiers { get; set; }
    public bool AutoStart { get; set; } = true;

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BetterTaskBar",
            "config.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static bool IsFirstRun() => !File.Exists(FilePath);
}
