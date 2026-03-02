using System.IO;
using System.Text.Json;

namespace whisperMeOff;

public class ServerProfile
{
    public string Name { get; set; } = "";
    public string ServerUrl { get; set; } = "";
    public string Model { get; set; } = "";
}

public class AppSettings
{
    public List<ServerProfile> Profiles { get; set; } = new()
    {
        new ServerProfile { Name = "Ollama", ServerUrl = "http://localhost:11434", Model = "" },
        new ServerProfile { Name = "LM Studio", ServerUrl = "http://localhost:1234", Model = "" },
        new ServerProfile { Name = "Jan AI", ServerUrl = "http://localhost:1337", Model = "" }
    };
    
    public int SelectedProfileIndex { get; set; } = 0;
    
    // Window position and size
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 950;
    public double WindowHeight { get; set; } = 650;
    public bool WindowMaximized { get; set; } = false;

    private static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "whisperMeOff",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // If loading fails, return defaults
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Silently fail if we can't save
        }
    }
}
