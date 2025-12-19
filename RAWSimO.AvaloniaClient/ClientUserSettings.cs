using System;
using System.IO;
using System.Text.Json;

namespace RAWSimO.AvaloniaClient;

internal sealed class ClientUserSettings
{
    public string LastConfigDirectory { get; set; }
    public string LastStatisticsDirectory { get; set; }

    private static string SettingsFilePath
    {
        get
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(baseDir))
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var dir = string.IsNullOrWhiteSpace(baseDir)
                ? Path.Combine(".", ".config", "RAWSimO")
                : Path.Combine(baseDir, "RAWSimO");

            return Path.Combine(dir, "avalonia-client.json");
        }
    }

    public static ClientUserSettings Load()
    {
        try
        {
            var path = SettingsFilePath;
            if (!File.Exists(path))
                return new ClientUserSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ClientUserSettings>(json) ?? new ClientUserSettings();
        }
        catch
        {
            return new ClientUserSettings();
        }
    }

    public void Save()
    {
        try
        {
            var path = SettingsFilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
        }
        catch
        {
            // best-effort only
        }
    }
}
