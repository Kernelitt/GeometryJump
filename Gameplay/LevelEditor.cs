using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Text.Json;
using GeometryJump.Level;

namespace GeometryJump.Core;

internal class LevelEditor
{
    private List<Block> _editorBlocks = new();
    private int _selectedBlockId = 1; 
    private float _selectedRotation = 0f;

    private string _levelName = "My Custom Level";
    private string _author = "Developer";
    private string _fileName = "level1.gjmap";
    private int _songId = 0;
    private Vector2 _cameraPos = Vector2.Zero;
    private const float GridSize = 48f;

    // Переменные для перетаскивания камеры колесиком (СКМ)
    private Vector2 _lastMousePos;
    private bool _isDraggingCamera = false;

    public bool IsActive { get; set; } = false;

    private bool _isMusicPlaying = false;
    private float _musicTime = 0f;
    private const float BaseXSpeed = 400f; // Должно совпадать со скоростью плеера
    private bool _isSelecting = false;
    private Vector2 _selectStartWorld;
    private Vector2 _selectEndWorld;
    private List<Block> _selectedBlocks = new();
    private List<Block> _clipboard = new();
    public void Update(float deltaTime, KeyboardState keyboard, MouseState mouse, int windowWidth, int windowHeight)
    {
        if (!IsActive) return;

        if (_isMusicPlaying)
        {
            _musicTime = AudioManager.GetMusicPlaybackPosition();
            _cameraPos.X = (_musicTime * BaseXSpeed) - windowWidth * 0.3f;
            if (_cameraPos.X < 0) _cameraPos.X = 0;
        }

        // Навигация (СКМ)
        if (mouse.IsButtonDown(MouseButton.Middle))
        {
            _isMusicPlaying = false;
            if (!_isDraggingCamera)
            {
                _isDraggingCamera = true;
                _lastMousePos = new Vector2(mouse.Position.X, mouse.Position.Y);
            }
            else
            {
                Vector2 currentMousePos = new Vector2(mouse.Position.X, mouse.Position.Y);
                Vector2 delta = currentMousePos - _lastMousePos;
                _cameraPos.X -= delta.X;
                _cameraPos.Y += delta.Y;
                if (_cameraPos.X < 0) _cameraPos.X = 0;
                _lastMousePos = currentMousePos;
            }
        }
        else { _isDraggingCamera = false; }

        if (ImGui.GetIO().WantCaptureMouse) return;

        float mouseWorldX = mouse.Position.X + _cameraPos.X;
        float mouseWorldY = (windowHeight - mouse.Position.Y) + _cameraPos.Y;
        int gridX = (int)MathF.Floor(mouseWorldX / GridSize);
        int gridY = (int)MathF.Floor(mouseWorldY / GridSize);
        if (gridY < 2) gridY = 2;

        // --- ЛОГИКА ВЫДЕЛЕНИЯ ГРУППЫ БЛОКОВ (SHIFT + ЛКМ) ---
        if (keyboard.IsKeyDown(Keys.LeftShift))
        {
            if (mouse.IsButtonDown(MouseButton.Left))
            {
                if (!_isSelecting)
                {
                    _isSelecting = true;
                    _selectStartWorld = new Vector2(mouseWorldX, mouseWorldY);
                }
                _selectEndWorld = new Vector2(mouseWorldX, mouseWorldY);
            }
            else if (_isSelecting)
            {
                _isSelecting = false;
                // Фиксируем блоки, попавшие в рамку
                float minX = MathF.Min(_selectStartWorld.X, _selectEndWorld.X);
                float maxX = MathF.Max(_selectStartWorld.X, _selectEndWorld.X);
                float minY = MathF.Min(_selectStartWorld.Y, _selectEndWorld.Y);
                float maxY = MathF.Max(_selectStartWorld.Y, _selectEndWorld.Y);

                _selectedBlocks = _editorBlocks.Where(b =>
                    (b.X * GridSize >= minX && b.X * GridSize <= maxX) &&
                    (b.Y * GridSize >= minY && b.Y * GridSize <= maxY)).ToList();
            }
            return; // Прерываем установку блоков, если работаем с выделением
        }
        // Сброс выделения при обычном клике без Shift в пустоту
        if (mouse.IsButtonPressed(MouseButton.Left) && !_editorBlocks.Any(b => b.X == gridX && b.Y == gridY))
        {
            _selectedBlocks.Clear();
        }

        // Обычная установка/удаление объектов
        if (mouse.IsButtonDown(MouseButton.Left) && !_isDraggingCamera)
        {
            bool alreadyExists = _editorBlocks.Any(b => b.X == gridX && b.Y == gridY && b.Id == _selectedBlockId && MathF.Abs(b.Rotation - _selectedRotation) < 0.1f);
            if (!alreadyExists)
            {
                _editorBlocks.RemoveAll(b => b.X == gridX && b.Y == gridY);
                _editorBlocks.Add(new Block { X = gridX, Y = gridY, Id = _selectedBlockId, Rotation = _selectedRotation, Layer = 0 });
            }
        }
        else if (mouse.IsButtonDown(MouseButton.Right))
        {
            _editorBlocks.RemoveAll(b => b.X == gridX && b.Y == gridY);
            _selectedBlocks.RemoveAll(b => b.X == gridX && b.Y == gridY);
        }
    }

    public void RenderWorld(int width, int height)
    {
        if (!IsActive) return;

        Matrix4 view = Matrix4.CreateTranslation(-_cameraPos.X, -_cameraPos.Y, 0f);
        Matrix4 projection = Matrix4.CreateOrthographicOffCenter(0, width, 0, height, -1f, 1f);
        BatchRenderer.Begin(view * projection);

        // Отрисовка пола
        BatchRenderer.DrawQuad(new Vector2(_cameraPos.X, 0), new Vector2(width, 100f), new Vector4(0, 0, 1, 1), new Vector4(0.1f, 0.1f, 0.15f, 1.0f), 0);

        foreach (var b in _editorBlocks)
        {
            if (!Level.ObjectRegistry.TryGet(b.Id, out var data)) continue;
            int texId = AssetManager.TextureAtlasId;
            Vector4 uv = AssetManager.TextureRegions.TryGetValue(data.TextureKey, out var cachedUV) ? cachedUV : new Vector4(0, 0, 1, 1);

            bool isSelected = _selectedBlocks.Any(sb => sb.X == b.X && sb.Y == b.Y);
            Vector4 color = isSelected ? new Vector4(1f, 1f, 0.2f, 1f) : Vector4.One;

            BatchRenderer.DrawQuad(new Vector2(b.X * GridSize, b.Y * GridSize), new Vector2(GridSize) * data.Scale, uv, color, texId, b.Rotation);
        }

        if (_isSelecting)
        {
            Vector2 pos = new Vector2(MathF.Min(_selectStartWorld.X, _selectEndWorld.X), MathF.Min(_selectStartWorld.Y, _selectEndWorld.Y));
            Vector2 size = new Vector2(MathF.Abs(_selectStartWorld.X - _selectEndWorld.X), MathF.Abs(_selectStartWorld.Y - _selectEndWorld.Y));
            BatchRenderer.DrawQuad(pos, size, new Vector4(0, 0, 1, 1), new Vector4(0.2f, 0.5f, 1f, 0.3f), 0);
        }
        float lineX = _musicTime * BaseXSpeed;
        BatchRenderer.DrawQuad(new Vector2(lineX, 0), new Vector2(3f, height), new Vector4(0, 0, 1, 1), new Vector4(1f, 0f, 0f, 0.7f), 0);

        BatchRenderer.End();
    }


    public void RenderUI(Action onExitEditor, Action<string> onInstantPlaytest)
    {
        if (!IsActive) return;

        ImGui.Begin("Map Editor", ImGuiWindowFlags.AlwaysAutoResize);

        ImGui.InputText("Level Name", ref _levelName, 32);
        ImGui.InputText("Author", ref _author, 32);
        ImGui.InputText("File Name", ref _fileName, 32);
        ImGui.InputInt("Newgrounds Song ID", ref _songId); 

        ImGui.Separator();
        ImGui.Text("Object Selection Grid (10x3):");

        int itemsPerRow = 10;
        int maxItems = 30;

        for (int i = 1; i <= maxItems; i++)
        {
            bool hasObj = ObjectRegistry.TryGet(i, out var data);

            Vector4 uv = (hasObj && AssetManager.TextureRegions.TryGetValue(data.TextureKey, out var cacheUV))
                ? cacheUV
                : new Vector4(0, 0, 1, 1);

            ImGui.PushID(i);

            if (_selectedBlockId == i)
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.6f, 1f, 0.6f));
            else
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.1f, 0.1f, 0.1f, 0.4f));

            IntPtr texPtr = AssetManager.TextureAtlasId;
            if (ImGui.ImageButton("id_btn", texPtr, new System.Numerics.Vector2(32, 32), new System.Numerics.Vector2(uv.X, uv.Y), new System.Numerics.Vector2(uv.Z, uv.W)))
            {
                if (hasObj) _selectedBlockId = i;
            }

            ImGui.PopStyleColor();
            ImGui.PopID();

            if (i % itemsPerRow != 0) ImGui.SameLine();
        }

        ImGui.Separator();
        if (ObjectRegistry.TryGet(_selectedBlockId, out var activeData))
            ImGui.Text($"Selected: ID {_selectedBlockId} [{activeData.TextureKey}]");
        else
            ImGui.TextDisabled($"Selected empty slot (ID {_selectedBlockId})");

        ImGui.SliderFloat("Object Rotation", ref _selectedRotation, 0, 270, "%.0f");
        if (ImGui.Button("Round to 90°")) _selectedRotation = MathF.Round(_selectedRotation / 90f) * 90f;

        ImGui.Separator();
        ImGui.TextWrapped("Control: LMB to Draw | RMB to Erase | Hold MMB to Move Camera");

        ImGui.Separator();
        ImGui.Text("Music Timeline / Playtest Tools:");

        if (ImGui.Button(_isMusicPlaying ? "Pause Audio" : "Play Audio Track"))
        {
            _isMusicPlaying = !_isMusicPlaying;
            if (_isMusicPlaying)
            {
                AudioManager.PlayNewgroundsSong(_songId);
                AudioManager.SetMusicPlaybackPosition(_musicTime);
            }
            else
            {
                AudioManager.StopMusic();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Stop & Reset Line"))
        {
            _isMusicPlaying = false;
            _musicTime = 0f;
            AudioManager.PlayNewgroundsSong(0);
        }
        if (_selectedBlocks.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextColored(new System.Numerics.Vector4(1f, 1f, 0f, 1f), $"Selected Group: {_selectedBlocks.Count} objects");

            if (ImGui.Button("Copy Selection"))
            {
                _clipboard = _selectedBlocks.Select(b => new Block { X = b.X, Y = b.Y, Id = b.Id, Rotation = b.Rotation }).ToList();
            }
            ImGui.SameLine();
            if (ImGui.Button("Paste") && _clipboard.Count > 0)
            {
                _selectedBlocks.Clear();
                foreach (var cb in _clipboard)
                {
                    var pasted = new Block { X = cb.X + 2, Y = cb.Y, Id = cb.Id, Rotation = cb.Rotation };
                    _editorBlocks.Add(pasted);
                    _selectedBlocks.Add(pasted);
                }
            }

            if (ImGui.Button("Rotate Structure +90°"))
            {
                if (_selectedBlocks.Count > 0)
                {
                    // 1. Находим центр масс выделенной структуры
                    float sumX = 0;
                    float sumY = 0;
                    foreach (var b in _selectedBlocks)
                    {
                        sumX += b.X;
                        sumY += b.Y;
                    }
                    // Округляем центр до целых ячеек сетки
                    int centerX = (int)MathF.Round(sumX / _selectedBlocks.Count);
                    int centerY = (int)MathF.Round(sumY / _selectedBlocks.Count);

                    // 2. Трансформируем каждый блок вокруг общего центра
                    for (int i = 0; i < _selectedBlocks.Count; i++)
                    {
                        var b = _selectedBlocks[i];
                        _editorBlocks.Remove(b); // Удаляем старую копию из мира

                        // Локальные координаты относительно общего центра
                        int localX = b.X - centerX;
                        int localY = b.Y - centerY;

                        // Матрица поворота на +90 градусов по часовой стрелке
                        b.X = -localY + centerX;
                        b.Y = localX + centerY;

                        // Поворачиваем и сам спрайт внутри ячейки
                        b.Rotation = (b.Rotation + 90f) % 360f;

                        _editorBlocks.Add(b); // Возвращаем обновленный блок в мир
                        _selectedBlocks[i] = b; // Обновляем ссылку в списке выделения
                    }
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Move Up")) MoveGroup(0, 1);
            ImGui.SameLine();
            if (ImGui.Button("Move Down")) MoveGroup(0, -1);
            ImGui.SameLine();
            if (ImGui.Button("Move Left")) MoveGroup(-1, 0);
            ImGui.SameLine();
            if (ImGui.Button("Move Right")) MoveGroup(1, 0);
        }
        ImGui.SliderFloat("Timeline (sec)", ref _musicTime, 0f, 300f, "%.2f s");
        if (!_isMusicPlaying && ImGui.IsItemEdited())
        {
            AudioManager.SetMusicPlaybackPosition(_musicTime);
        }

        ImGui.Separator();
        if (ImGui.Button("PLAYTEST", new System.Numerics.Vector2(-1, 35)))
        {
            _isMusicPlaying = false;
            ExportLevel(); // Автосохранение перед тестом
            IsActive = false;

            // Передаем действие на запуск геймплея этой карты наружу
            onInstantPlaytest?.Invoke(_fileName);
        }

        ImGui.Separator();
        if (ImGui.Button("Save Level (.gjmap)")) ExportLevel();
        ImGui.SameLine();
        if (ImGui.Button("Load Level")) ImportLevel();

        ImGui.Separator();
        if (ImGui.Button("Close Editor & Return"))
        {
            IsActive = false;
            onExitEditor?.Invoke();
        }

        ImGui.End();
    }

    private void MoveGroup(int deltaX, int deltaY)
    {
        for (int i = 0; i < _selectedBlocks.Count; i++)
        {
            var b = _selectedBlocks[i];
            _editorBlocks.Remove(b);
            b.X += deltaX;
            b.Y += deltaY;
            _editorBlocks.Add(b);
            _selectedBlocks[i] = b;
        }
    }

    private void ExportLevel()
    {
        var levelData = new Level.Level
        {
            Name = _levelName,
            Author = _author,
            Description = "Created in integrated editor",
            SongId = _songId,
            blocks = _editorBlocks.ToArray()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true
        };

        string json = JsonSerializer.Serialize(levelData, options);
        File.WriteAllText(_fileName, json);
    }

    private void ImportLevel()
    {
        if (!File.Exists(_fileName)) return;
        string json = File.ReadAllText(_fileName);

        // ДОБАВЛЕНО: Включаем поддержку полей для чтения
        var options = new JsonSerializerOptions
        {
            IncludeFields = true
        };

        var loaded = JsonSerializer.Deserialize<Level.Level>(json, options);

        _levelName = loaded.Name;
        _author = loaded.Author;
        _songId = loaded.SongId;
        _editorBlocks = new List<Block>(loaded.blocks);
    }
    public void LoadLevelDirectly(string filePath)
    {
        if (!File.Exists(filePath)) return;
        _fileName = filePath;
        ImportLevel(); // Загружаем данные блоков в редактор
        IsActive = true;
    }

}
