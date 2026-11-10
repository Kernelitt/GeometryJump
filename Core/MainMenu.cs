using ImGuiNET;
using GeometryJump.Gameplay;
using System.Text.Json;
using OpenTK.Mathematics;

namespace GeometryJump.Core;

internal class MainMenu
{
    public bool IsOpen { get; set; } = true;
    private string[] _availableMaps = Array.Empty<string>();
    bool vsync = true;
    private string _selectedMapFile = "";
    private Level.Level? _selectedLevelData = null;
    private bool _musicExists = false;
    private string _downloadError = "";

    public void RefreshMapsList()
    {
        if (Directory.Exists(Environment.CurrentDirectory))
        {
            _availableMaps = Directory.GetFiles(Environment.CurrentDirectory, "*.gjmap")
                                      .Select(Path.GetFileName)
                                      .ToArray();
        }

        if (!string.IsNullOrEmpty(_selectedMapFile) && !_availableMaps.Contains(_selectedMapFile))
        {
            _selectedMapFile = "";
            _selectedLevelData = null;
        }
    }

    private void LoadSelectedLevelInfo(string fileName)
    {
        try
        {
            _selectedMapFile = fileName;
            string json = File.ReadAllText(fileName);
            var options = new JsonSerializerOptions { IncludeFields = true };
            _selectedLevelData = JsonSerializer.Deserialize<Level.Level>(json, options);

            CheckMusicStatus();
            _downloadError = "";
        }
        catch
        {
            _selectedLevelData = null;
        }
    }

    private void CheckMusicStatus()
    {
        if (_selectedLevelData == null) return;
        int songId = _selectedLevelData.Value.SongId;
        _musicExists = File.Exists($"Assets/Songs/{songId}.mp3") || File.Exists($"Assets/Songs/{songId}.wav");
    }

    public void RenderUI(Action<string> onLaunchGame, Action<string> onEditGame, Action onOpenNewEditor, Action<bool> onVsyncToggle)
    {
        if (!IsOpen) return;

        ImGui.SetNextWindowSize(new System.Numerics.Vector2(750, 450), ImGuiCond.FirstUseEver);
        ImGui.Begin("Geometry Jump - Dashboard", ImGuiWindowFlags.NoCollapse);

        ImGui.BeginChild("LeftPane", new System.Numerics.Vector2(280, 0));
        ImGui.Text("Select Level:");
        ImGui.Separator();

        if (_availableMaps.Length == 0)
        {
            ImGui.TextDisabled("(No .gjmap levels found)");
        }

        foreach (var map in _availableMaps)
        {
            bool isSelected = (_selectedMapFile == map);
            if (ImGui.Selectable(map, isSelected))
            {
                LoadSelectedLevelInfo(map);
            }
        }

        ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 45);
        if (ImGui.Button("Create New Level", new System.Numerics.Vector2(-1, 30)))
        {
            IsOpen = false;
            onOpenNewEditor?.Invoke();
        }
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("RightPane", new System.Numerics.Vector2(0, 0));
        if (_selectedLevelData != null)
        {
            var level = _selectedLevelData.Value;

            ImGui.Text($"Title: {level.Name}");
            ImGui.Text($"Author: {level.Author}");
            ImGui.TextDisabled($"Description: {level.Description}");
            ImGui.Text($"Total Objects: {level.blocks?.Length ?? 0}");
            ImGui.Separator();

            // Секция Аудио
            ImGui.Text($"Newgrounds Song ID: {level.SongId}");
            if (level.SongId > 0)
            {
                if (_musicExists)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.2f, 1f, 0.2f, 1f), "Music Status: Downloaded & Ready");
                }
                else if (AudioManager.IsDownloading)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0.8f, 0f, 1f), $"Downloading music... {AudioManager.DownloadProgress * 100f:F0}%");
                    ImGui.ProgressBar(AudioManager.DownloadProgress, new System.Numerics.Vector2(-1, 15), "");
                }
                else
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f), "Music Status: Missing locally");
                    if (ImGui.Button("Download Track from Newgrounds"))
                    {
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            bool success = await AudioManager.DownloadSongFromNewgroundsAsync(level.SongId);
                            _musicExists = success;
                            if (!success) _downloadError = "Download failed. Check connection/ID.";
                        });
                    }
                }
                if (!string.IsNullOrEmpty(_downloadError))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0f, 0f, 1f), _downloadError);
                }
            }
            else
            {
                ImGui.TextDisabled("Music Status: No Song assigned");
            }

            ImGui.Separator();
            if (ImGui.TreeNode("Icon Kit & Character Customization"))
            {
                IntPtr openGlTexId = (IntPtr)AssetManager.TextureAtlasId;

                if (ImGui.BeginTabBar("GarageTabs"))
                {
                    if (ImGui.BeginTabItem("Cubes"))
                    {
                        ImGui.Text("Select Cube Skin (Base + Detail Layer):");
                        ImGui.Separator();

                        for (int i = 1; i <= 2; i++)
                        {
                            string baseKey = $"icon_{i}_1";
                            string extraKey = $"icon_{i}_2";

                            // Получаем точные UV из атласа
                            Vector4 uv1 = AssetManager.TextureRegions.TryGetValue(baseKey, out var u1) ? u1 : Vector4.Zero;
                            Vector4 uv2 = AssetManager.TextureRegions.TryGetValue(extraKey, out var u2) ? u2 : Vector4.Zero;

                            ImGui.PushID($"cube_btn_{i}");

                            System.Numerics.Vector2 screenPos = ImGui.GetCursorScreenPos();
                            if (ImGui.Button("##blank", new System.Numerics.Vector2(48, 48)))
                            {
                                SkinSet.Current.SelectedCubeId = i;
                            }

                            var drawList = ImGui.GetWindowDrawList();
                            System.Numerics.Vector2 pMin = screenPos;
                            System.Numerics.Vector2 pMax = new(screenPos.X + 48, screenPos.Y + 48);

                            uint c1 = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(SkinSet.Current.Color1.X, SkinSet.Current.Color1.Y, SkinSet.Current.Color1.Z, SkinSet.Current.Color1.W));
                            uint c2 = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(SkinSet.Current.Color2.X, SkinSet.Current.Color2.Y, SkinSet.Current.Color2.Z, SkinSet.Current.Color2.W));

                            // Слой 1: Основа (Цвет 1)
                            drawList.AddImage(openGlTexId, pMin, pMax, new System.Numerics.Vector2(uv1.X, uv1.Y), new System.Numerics.Vector2(uv1.Z, uv1.W), c1);
                            // Слой 2: Узор (Цвет 2)
                            drawList.AddImage(openGlTexId, pMin, pMax, new System.Numerics.Vector2(uv2.X, uv2.Y), new System.Numerics.Vector2(uv2.Z, uv2.W), c2);

                            if (SkinSet.Current.SelectedCubeId == i)
                            {
                                drawList.AddRect(pMin, pMax, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.2f, 1f, 0.2f, 1f)), 0f, ImDrawFlags.None, 2f);
                            }

                            ImGui.PopID();
                            if (i % 5 != 0) ImGui.SameLine();
                        }
                        ImGui.NewLine();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Ships"))
                    {
                        ImGui.Text("Select Ship Skin:");
                        ImGui.Separator();

                        for (int i = 1; i <= 1; i++) 
                        {
                            string baseKey = $"ship_{i}_1";
                            string extraKey = $"ship_{i}_2";

                            Vector4 uv1 = AssetManager.TextureRegions.TryGetValue(baseKey, out var u1) ? u1 : Vector4.Zero;
                            Vector4 uv2 = AssetManager.TextureRegions.TryGetValue(extraKey, out var u2) ? u2 : Vector4.Zero;

                            ImGui.PushID($"ship_btn_{i}");

                            System.Numerics.Vector2 screenPos = ImGui.GetCursorScreenPos();
                            if (ImGui.Button("##blank_ship", new System.Numerics.Vector2(48, 48)))
                            {
                                SkinSet.Current.SelectedShipId = i;
                            }

                            var drawList = ImGui.GetWindowDrawList();
                            System.Numerics.Vector2 pMin = screenPos;
                            System.Numerics.Vector2 pMax = new(screenPos.X + 48, screenPos.Y + 48);

                            uint c1 = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(SkinSet.Current.Color1.X, SkinSet.Current.Color1.Y, SkinSet.Current.Color1.Z, SkinSet.Current.Color1.W));
                            uint c2 = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(SkinSet.Current.Color2.X, SkinSet.Current.Color2.Y, SkinSet.Current.Color2.Z, SkinSet.Current.Color2.W));

                            drawList.AddImage(openGlTexId, pMin, pMax, new System.Numerics.Vector2(uv1.X, uv1.Y), new System.Numerics.Vector2(uv1.Z, uv1.W), c1);
                            drawList.AddImage(openGlTexId, pMin, pMax, new System.Numerics.Vector2(uv2.X, uv2.Y), new System.Numerics.Vector2(uv2.Z, uv2.W), c2);

                            if (SkinSet.Current.SelectedShipId == i)
                            {
                                drawList.AddRect(pMin, pMax, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.2f, 1f, 0.2f, 1f)), 0f, ImDrawFlags.None, 2f);
                            }

                            ImGui.PopID();
                            if (i % 5 != 0) ImGui.SameLine();
                        }
                        ImGui.NewLine();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Colors"))
                    {
                        System.Numerics.Vector4 col1 = new(SkinSet.Current.Color1.X, SkinSet.Current.Color1.Y, SkinSet.Current.Color1.Z, SkinSet.Current.Color1.W);
                        System.Numerics.Vector4 col2 = new(SkinSet.Current.Color2.X, SkinSet.Current.Color2.Y, SkinSet.Current.Color2.Z, SkinSet.Current.Color2.W);

                        if (ImGui.ColorEdit4("Primary Color", ref col1))
                            SkinSet.Current.Color1 = new Vector4(col1.X, col1.Y, col1.Z, col1.W);

                        if (ImGui.ColorEdit4("Secondary Color", ref col2))
                            SkinSet.Current.Color2 = new Vector4(col2.X, col2.Y, col2.Z, col2.W);

                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
                ImGui.TreePop();
            }



            // Кнопки запуска
            if (ImGui.Button("PLAY LEVEL", new System.Numerics.Vector2(160, 40)))
            {
                IsOpen = false;
                onLaunchGame?.Invoke(_selectedMapFile);
            }
            ImGui.SameLine();
            if (ImGui.Button("EDIT IN EDITOR", new System.Numerics.Vector2(160, 40)))
            {
                IsOpen = false;
                onEditGame?.Invoke(_selectedMapFile);
            }
        }
        else
        {
            ImGui.TextDisabled("Select a level from the left panel to view info.");
        }

        // Общие настройки в самом низу правой панели
        ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 55);
        ImGui.Separator();
        
        if (ImGui.Checkbox("Enable VSync", ref vsync))
        {
            onVsyncToggle?.Invoke(vsync);
        }

        ImGui.EndChild();

        ImGui.End();
    }
}
