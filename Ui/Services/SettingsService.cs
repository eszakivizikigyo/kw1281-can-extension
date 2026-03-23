using System;
using System.IO;
using System.Text.Json;

namespace BitFab.KW1281Test.Ui.Services;

public class AppSettings
{
    public string? LastPort { get; set; }
    public int BaudRate { get; set; } = 10400;
    public string Mode { get; set; } = "KLine";
    public byte ControllerAddress { get; set; } = 0x17;
    public string ThemeVariant { get; set; } = "Default";
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 700;
}

public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "kw1281test", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Ignore corrupt settings
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save failures
        }
    }
}
