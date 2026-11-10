using OpenTK.Mathematics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace GeometryJump.Core;

internal class SkinSet
{
    public int SelectedCubeId { get; set; } = 1;
    public int SelectedShipId { get; set; } = 1;
    public int SelectedBallId { get; set; } = 1;

    // ИСПРАВЛЕНО: Запрещаем сериализатору лезть во внутренности математических структур OpenTK
    [JsonIgnore]
    public Vector4 Color1 { get; set; } = new Vector4(0.0f, 1.0f, 0.4f, 1.0f);

    [JsonIgnore]
    public Vector4 Color2 { get; set; } = new Vector4(1.0f, 0.9f, 0.0f, 1.0f);

    // ДОБАВЛЕНО: Безопасные плоские массивы для сохранения цветов в JSON без рекурсии
    public float[] Color1Raw
    {
        get => new float[] { Color1.X, Color1.Y, Color1.Z, Color1.W };
        set { if (value != null && value.Length == 4) Color1 = new Vector4(value[0], value[1], value[2], value[3]); }
    }

    public float[] Color2Raw
    {
        get => new float[] { Color1.X, Color2.Y, Color2.Z, Color2.W };
        set { if (value != null && value.Length == 4) Color2 = new Vector4(value[0], value[1], value[2], value[3]); }
    }

    public static SkinSet Current { get; set; } = new SkinSet();

    private const string ConfigPath = "config.json";

    public static List<int> AvailableCubes { get; private set; } = new();
    public static List<int> AvailableShips { get; private set; } = new();
    public static List<int> AvailableBalls { get; private set; } = new();

    public static void DiscoverAvailableSkins()
    {
        AvailableCubes.Clear();
        AvailableShips.Clear();
        AvailableBalls.Clear();

        foreach (var key in AssetManager.TextureRegions.Keys)
        {
            if (key.StartsWith("icon_") && key.EndsWith("_1"))
            {
                string[] parts = key.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int id)) AvailableCubes.Add(id);
            }
            else if (key.StartsWith("ship_") && key.EndsWith("_1"))
            {
                string[] parts = key.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int id)) AvailableShips.Add(id);
            }
            else if (key.StartsWith("ball_") && key.EndsWith("_1"))
            {
                string[] parts = key.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int id)) AvailableBalls.Add(id);
            }
        }

        AvailableCubes.Sort();
        AvailableShips.Sort();
        AvailableBalls.Sort();

        if (AvailableCubes.Count == 0) AvailableCubes.Add(1);
        if (AvailableShips.Count == 0) AvailableShips.Add(1);
        if (AvailableBalls.Count == 0) AvailableBalls.Add(1);
    }

    public void SaveConfig()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true
        };
        string json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(ConfigPath, json);
    }

    public static void LoadConfig()
    {
        if (!File.Exists(ConfigPath)) return;
        try
        {
            string json = File.ReadAllText(ConfigPath);
            var options = new JsonSerializerOptions { IncludeFields = true };
            Current = JsonSerializer.Deserialize<SkinSet>(json, options) ?? new SkinSet();
        }
        catch
        {
            Current = new SkinSet();
        }
    }
}
