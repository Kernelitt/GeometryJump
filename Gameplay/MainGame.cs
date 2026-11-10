using GeometryJump.Core;
using GeometryJump.Level;
using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Text.Json;

namespace GeometryJump.Gameplay;

internal class MainGame
{
    private readonly Player _player;
    private Vector2 _cameraPosition;
    private Level.Level _currentLevel;
    private bool _isLevelLoaded = false;

    private float _deathTimer = 0f;
    private bool _isDead = false;
    private bool _isLevelPassed = false;
    private bool _isFirstFrame = true;
    private Box2 _lastSpikeHitbox; // Для отладочной подсветки шипа, об который убились
    private Box2 _lastPlayerHitbox;

    private int _lastActivatedOrbBlockIndex = -1;
    public MainGame()
    {
        // Спавним игрока на высоте 100 пикселей от низа экрана
        _player = new Player(new Vector2(100f, 100f));
        _cameraPosition = Vector2.Zero;
    }

    public void LoadLevel(string levelPath)
    {
        try
        {
            var options = new JsonSerializerOptions { IncludeFields = true };
            string json = File.ReadAllText(levelPath);
            _currentLevel = JsonSerializer.Deserialize<Level.Level>(json, options);
            _isLevelLoaded = true;

            if (_currentLevel.SongId > 0)
            {
                AudioManager.PlayNewgroundsSong(_currentLevel.SongId);
            }

            // ИСПРАВЛЕНО: Включаем защиту первого кадра
            _isFirstFrame = true;
        }
        catch
        {
            _isLevelLoaded = false;
        }
    }


    public void Update(float deltaTime, KeyboardState keyboard)
    {
        if (_isLevelPassed) return;

        if (_isDead)
        {
            _deathTimer -= deltaTime;
            if (_deathTimer <= 0f)
            {
                ActualReset();
            }
            return;
        }

        if (_isFirstFrame)
        {
            _isFirstFrame = false;
            return;
        }

        _player.Update(deltaTime, keyboard);

        // Движение по X — жесткая привязка
        _cameraPosition.X = _player.Position.X - 200f;


        float targetCameraY = _player.Position.Y - 250f;

        if (targetCameraY < 0f) targetCameraY = 0f; 

        float cameraSpeedY = _player.CurrentMode == GameMode.Ship ? 3.5f : 2.0f;
        _cameraPosition.Y = MathHelper.Lerp(_cameraPosition.Y, targetCameraY, cameraSpeedY * deltaTime);

        if (_isLevelLoaded)
        {
            CheckCollisions(deltaTime);
        }
    }


    public void Render(int windowWidth, int windowHeight)
    {
        Matrix4 view = Matrix4.CreateTranslation(-_cameraPosition.X, -_cameraPosition.Y, 0f);
        Matrix4 projection = Matrix4.CreateOrthographicOffCenter(0, windowWidth, 0, windowHeight, -1f, 1f);
        Matrix4 viewProjection = view * projection;

        BatchRenderer.Begin(viewProjection);

        // 1. Отрисовка пола
        DrawBackground(windowWidth, windowHeight);

        // 2. Отрисовка блоков уровня с текстурами
        if (_isLevelLoaded && _currentLevel.blocks != null)
        {
            int texId = AssetManager.TextureAtlasId;
            float amp = AudioManager.GetCurrentMusicAmplitude();

            foreach (var block in _currentLevel.blocks)
            {
                float blockX = block.X * 48f;
                float blockY = block.Y * 48f;
                Vector2 blockSize = new Vector2(48f, 48f);

                // Culling (отсечение невидимых блоков)
                if (blockX > _cameraPosition.X - 64f && blockX < _cameraPosition.X + windowWidth + 64f)
                {
                    if (!Level.ObjectRegistry.TryGet(block.Id, out var data)) continue;

                    Vector2 baseSize = new Vector2(data.Scale.X * 48f, data.Scale.Y * 48f);

                    if (data.Type == InteractionType.Solid || data.Type == InteractionType.Hazard)
                    {
                        blockSize = new Vector2(48f, 48f);
                    }
                    string texKey = data.TextureKey;

                    Vector4 uv = AssetManager.TextureRegions.TryGetValue(texKey, out var cachedUV)
                        ? cachedUV
                        : new Vector4(0, 0, 1, 1);

                    Vector4 color = texId == 0
                        ? (data.Type == InteractionType.Hazard ? new Vector4(0.9f, 0.3f, 0.3f, 1.0f) : new Vector4(0.3f, 0.5f, 0.9f, 1.0f))
                        : Vector4.One;

                    Vector2 objPos = new Vector2(block.X * 48f, block.Y * 48f);

                    if (data.VisualAudioScale > 0.001f)
                    {
                        float scaleMultiplier = 1.0f + (amp * data.VisualAudioScale * 0.25f);
                        Vector2 newSize = baseSize * scaleMultiplier;

                        objPos -= (newSize - baseSize) * 0.5f;
                        baseSize = newSize;
                    }

                    BatchRenderer.DrawQuad(objPos, baseSize, uv, color, texId, block.Rotation);
                }
            }

        }

        // 3. Отрисовка игрока
        _player.Render();
        if (_isDead)
        {
            // Отключаем наложение текстуры (id = 0), берем чистый UV Vector4(0,0,1,1)
            Vector4 pureUV = new Vector4(0, 0, 1, 1);

            // Отрисовка хитбокса игрока (красный)
            Vector2 pMin = _lastPlayerHitbox.Min;
            Vector2 pSize = _lastPlayerHitbox.Max - _lastPlayerHitbox.Min;
            BatchRenderer.DrawQuad(pMin, pSize, pureUV, new Vector4(1f, 0f, 0f, 0.5f), 0);

            // Отрисовка хитбокса препятствия (оранжевый)
            Vector2 oMin = _lastSpikeHitbox.Min;
            Vector2 oSize = _lastSpikeHitbox.Max - _lastSpikeHitbox.Min;
            BatchRenderer.DrawQuad(oMin, oSize, pureUV, new Vector4(1f, 0.6f, 0f, 0.6f), 0);
        }
        BatchRenderer.End();
    }

    public void RenderPassMenu(Action onReturnToMenu)
    {
        if (!_isLevelPassed) return;

        ImGui.SetNextWindowPos(new System.Numerics.Vector2(ImGui.GetMainViewport().Size.X / 2 - 120, ImGui.GetMainViewport().Size.Y / 2 - 60), ImGuiCond.Always);
        ImGui.Begin("Level Completed!", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove);

        ImGui.TextColored(new System.Numerics.Vector4(0.2f, 1f, 0.2f, 1f), "CONGRATULATIONS!");
        ImGui.Text($"Level: {_currentLevel.Name}");
        ImGui.Separator();

        if (ImGui.Button("Restart Level", new System.Numerics.Vector2(100, 30)))
        {
            _isLevelPassed = false;
            ActualReset();
        }
        ImGui.SameLine();
        if (ImGui.Button("Main Menu", new System.Numerics.Vector2(100, 30)))
        {
            _isLevelPassed = false;
            onReturnToMenu?.Invoke();
        }

        ImGui.End();
    }

    private void DrawBackground(int width, int height)
    {
        // Линия пола
        BatchRenderer.DrawQuad(new Vector2(_cameraPosition.X, 0f), new Vector2(width, 100f), new Vector4(0, 0, 1, 1), new Vector4(0.1f, 0.1f, 0.15f, 1.0f), 0);
    }

    private void CheckCollisions(float deltaTime)
    {
        if (!_isLevelLoaded || _isDead || _isLevelPassed) return;

        Box2 playerBlockBox = _player.BlockHitbox;
        Box2 playerFullBox = _player.FullHitbox;
        bool stoodOnObject = false;
        bool launchedThisFrame = false;

        // 1. Проверка прохождения уровня
        float maxBlockX = 0f;
        float maxBlockY = 0f;

        if (_currentLevel.blocks != null && _currentLevel.blocks.Length > 0)
        {
            maxBlockX = _currentLevel.blocks.Max(b => b.X * 48f);
            maxBlockY = _currentLevel.blocks.Max(b => b.Y * 48f);

            if (_player.Position.X > maxBlockX + (10 * 48f))
            {
                _isLevelPassed = true;
                AudioManager.StopMusic();
                return;
            }
        }

        float dynamicCeilingDeathY = (_currentLevel.blocks != null && _currentLevel.blocks.Length > 0)
            ? maxBlockY + 48f + (10 * 48f)
            : 1500f;

        if (_player.Position.Y > dynamicCeilingDeathY)
        {
            _lastPlayerHitbox = playerFullBox;
            _lastSpikeHitbox = new Box2(_player.Position.X - 50f, dynamicCeilingDeathY, _player.Position.X + 100f, dynamicCeilingDeathY + 10f);
            TriggerDeath(playerFullBox, _lastSpikeHitbox);
            return;
        }

        // Читаем ввод с клавиатуры для механики Buffer Click
        var keyboard = MainApp.App.KeyboardState;
        bool isInputHeld = keyboard.IsKeyDown(Keys.Space) || keyboard.IsKeyDown(Keys.Up);

        // Сбрасываем кэш орба, если игрок полностью отпустил кнопку прыжка
        if (!isInputHeld)
        {
            _lastActivatedOrbBlockIndex = -1;
        }

        // Локальный хелпер для генерации уникального идентификатора ячейки (чтобы отличать орбы)
        int GetBlockHashCode(int x, int y) => x * 73856093 ^ y * 19349663;

        foreach (var block in _currentLevel.blocks)
        {
            if (!Level.ObjectRegistry.TryGet(block.Id, out var data)) continue;
            if (data.Type == InteractionType.Decorative) continue;

            Vector2 objPos = new Vector2(block.X * 48f, block.Y * 48f);
            Vector2 hitOffset = data.HitboxOffset;
            Vector2 hitSize = data.HitboxSize;

            Box2 targetBox = (data.Type == InteractionType.Hazard || data.Type == InteractionType.Interactable || data.Type == InteractionType.Touchable) ? playerFullBox : playerBlockBox;

            bool hit = Box2Extensions.IntersectsRotated(targetBox, objPos, new Vector2(48f, 48f), block.Rotation, hitOffset, hitSize);

            if (hit)
            {
                int blockUID = GetBlockHashCode(block.X, block.Y);

                switch (data.Type)
                {
                    case InteractionType.Hazard:
                        _lastPlayerHitbox = playerFullBox;
                        _lastSpikeHitbox = Box2Extensions.GetAbsoluteOBBBounds(objPos, block.Rotation, hitOffset, hitSize);
                        TriggerDeath(playerFullBox, _lastSpikeHitbox);
                        return;

                    case InteractionType.Solid:
                        if (launchedThisFrame) break;

                        Box2 absoluteBounds = Box2Extensions.GetAbsoluteOBBBounds(objPos, block.Rotation, hitOffset, hitSize);
                        float blockTop = absoluteBounds.Max.Y;
                        float blockBottom = absoluteBounds.Min.Y;

                        if (_player.CurrentMode == GameMode.Ship || _player.CurrentMode == GameMode.Ball)
                        {
                            bool isAbove = (_player.Position.Y - _player.Velocity.Y * deltaTime) >= blockTop - 12f;
                            bool isBelow = (_player.Position.Y + _player.Size.Y - _player.Velocity.Y * deltaTime) <= blockBottom + 12f;

                            if (isAbove && _player.Velocity.Y <= 0.01f && _player.GravitySign > 0)
                            {
                                _player.Position.Y = blockTop;
                                _player.Velocity.Y = 0;
                                stoodOnObject = true;
                            }
                            else if (isBelow && _player.Velocity.Y >= -0.01f && _player.GravitySign < 0)
                            {
                                _player.Position.Y = blockBottom - _player.Size.Y;
                                _player.Velocity.Y = 0;
                                stoodOnObject = true;
                            }
                            else
                            {
                                bool movingIntoWall = (_player.GravitySign > 0 && !isAbove) || (_player.GravitySign < 0 && !isBelow);
                                if (movingIntoWall)
                                {
                                    _lastPlayerHitbox = playerFullBox;
                                    _lastSpikeHitbox = absoluteBounds;
                                    TriggerDeath(playerFullBox, _lastSpikeHitbox);
                                    return;
                                }
                            }
                        }
                        else // --- КЛАССИЧЕСКАЯ ФИЗИКА КУБА С БУФЕРИЗАЦИЕЙ НА ЗЕМЛЕ ---
                        {
                            if (_player.GravitySign > 0)
                            {
                                if (_player.Velocity.Y <= 0.001f && (_player.Position.Y - _player.Velocity.Y * 0.016f) >= blockTop - 8f)
                                {
                                    _player.Position.Y = blockTop;
                                    stoodOnObject = true;
                                }
                                else
                                {
                                    _lastPlayerHitbox = playerFullBox;
                                    _lastSpikeHitbox = absoluteBounds;
                                    TriggerDeath(playerFullBox, _lastSpikeHitbox);
                                    return;
                                }
                            }
                            else
                            {
                                if (_player.Velocity.Y >= -0.001f && (_player.Position.Y + _player.Size.Y - _player.Velocity.Y * 0.016f) <= blockBottom + 8f)
                                {
                                    _player.Position.Y = blockBottom - _player.Size.Y;
                                    stoodOnObject = true;
                                }
                                else
                                {
                                    _lastPlayerHitbox = playerFullBox;
                                    _lastSpikeHitbox = absoluteBounds;
                                    TriggerDeath(playerFullBox, _lastSpikeHitbox);
                                    return;
                                }
                            }
                        }
                        break;

                    case InteractionType.Touchable:
                        data.OnTrigger?.Invoke(_player, block.Rotation);
                        launchedThisFrame = true;
                        break;

                    case InteractionType.Interactable:
                        if (isInputHeld)
                        {
                            bool isFreshKeyPress = keyboard.IsKeyPressed(Keys.Space) || keyboard.IsKeyPressed(Keys.Up);

                            if (isFreshKeyPress || !_player.HasBufferFired)
                            {
                                _player.HasBufferFired = true;
                                _lastActivatedOrbBlockIndex = blockUID; 

                                data.OnTrigger?.Invoke(_player, block.Rotation);
                                launchedThisFrame = true;
                            }
                        }
                        break;

                }
            }
        }

        if (launchedThisFrame)
        {
            _player.SetGrounded(false);
            return;
        }

        float floorY = 100f;

        if (_player.GravitySign > 0 && _player.Position.Y <= floorY)
        {
            _player.Position.Y = floorY;
            if (_player.CurrentMode == GameMode.Ship || _player.CurrentMode == GameMode.Ball) _player.Velocity.Y = 0;
            stoodOnObject = true;
        }

        _player.SetGrounded(stoodOnObject);
    }


    private void TriggerDeath(Box2 playerBox, Box2 obstacleBox)
    {
        AudioManager.StopMusic();
        _isDead = true;
        _deathTimer = 1.5f; // Заморозка на 1.5 секунды
        _lastPlayerHitbox = playerBox;
        _lastSpikeHitbox = obstacleBox;
    }

    private void ActualReset()
    {
        if (_currentLevel.SongId > 0)
        {
            AudioManager.PlayNewgroundsSong(_currentLevel.SongId);
        }
        _isDead = false;
        _isLevelPassed = false;

        _player.Reset(new Vector2(100f, 100f));
        _player.GravitySign = 1.0f;

        _cameraPosition = Vector2.Zero;
        _isFirstFrame = true;
    }

}
internal static class Box2Extensions
{
    // Проверка пересечения зауженного AABB игрока (или полного) с повернутым OBB препятствия
    public static bool IntersectsRotated(Box2 playerAABB, Vector2 objPos, Vector2 objSize, float rotationDegrees, Vector2 hitOffset, Vector2 hitSize)
    {
        // 1. Получаем 4 вершины игрока (AABB)
        Vector2[] pVertices = new Vector2[]
        {
            new Vector2(playerAABB.Min.X, playerAABB.Min.Y),
            new Vector2(playerAABB.Max.X, playerAABB.Min.Y),
            new Vector2(playerAABB.Max.X, playerAABB.Max.Y),
            new Vector2(playerAABB.Min.X, playerAABB.Max.Y)
        };

        // 2. Получаем 4 вершины объекта с учетом локального хитбокса и вращения вокруг ЦЕНТРА ОБЪЕКТА (48х48)
        Vector2 objCenter = objPos + new Vector2(48f, 48f) * 0.5f;
        Vector2 localMin = hitOffset;
        Vector2 localMax = hitOffset + hitSize;

        Vector2[] oVertices = new Vector2[]
        {
            new Vector2(localMin.X, localMin.Y),
            new Vector2(localMax.X, localMin.Y),
            new Vector2(localMax.X, localMax.Y),
            new Vector2(localMin.X, localMax.Y)
        };

        float rad = MathHelper.DegreesToRadians(rotationDegrees);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        for (int i = 0; i < 4; i++)
        {
            // Смещение относительно центра блока
            Vector2 rel = oVertices[i] - new Vector2(48f, 48f) * 0.5f;
            // Вращение и мировой сдвиг
            oVertices[i] = new Vector2(
                rel.X * cos - rel.Y * sin + objCenter.X,
                rel.X * sin + rel.Y * cos + objCenter.Y
            );
        }

        // 3. Проверка осей методом разделяющей оси (SAT)
        return QueryAxisIntersection(pVertices, oVertices);
    }

    private static bool QueryAxisIntersection(Vector2[] poly1, Vector2[] poly2)
    {
        List<Vector2> axes = new List<Vector2>();

        // Собираем нормали граней полигона 1 (вертикаль/горизонталь для игрока)
        axes.Add(new Vector2(1, 0));
        axes.Add(new Vector2(0, 1));

        // Собираем нормали граней повернутого полигона 2
        for (int i = 0; i < 4; i++)
        {
            Vector2 p1 = poly2[i];
            Vector2 p2 = poly2[(i + 1) % 4];
            Vector2 edge = p2 - p1;
            Vector2 normal = new Vector2(-edge.Y, edge.X).Normalized();
            axes.Add(normal);
        }

        // Проверяем проекции на каждую ось
        foreach (var axis in axes)
        {
            float min1 = float.MaxValue, max1 = float.MinValue;
            float min2 = float.MaxValue, max2 = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                float proj1 = Vector2.Dot(poly1[i], axis);
                if (proj1 < min1) min1 = proj1;
                if (proj1 > max1) max1 = proj1;

                float proj2 = Vector2.Dot(poly2[i], axis);
                if (proj2 < min2) min2 = proj2;
                if (proj2 > max2) max2 = proj2;
            }

            // Если проекции не пересекаются, значит есть разделяющая ось -> коллизии нет
            if (max1 < min2 || max2 < min1)
                return false;
        }

        return true;
    }

    // Метод генерирует правильные мировые границы (AABB оболочку) для визуализации повернутого OBB хитбокса
    public static Box2 GetAbsoluteOBBBounds(Vector2 objPos, float rotationDegrees, Vector2 hitOffset, Vector2 hitSize)
    {
        Vector2 objCenter = objPos + new Vector2(48f, 48f) * 0.5f;
        Vector2 localMin = hitOffset;
        Vector2 localMax = hitOffset + hitSize;

        Vector2[] vertices = new Vector2[]
        {
        new Vector2(localMin.X, localMin.Y),
        new Vector2(localMax.X, localMin.Y),
        new Vector2(localMax.X, localMax.Y),
        new Vector2(localMin.X, localMax.Y)
        };

        float rad = MathHelper.DegreesToRadians(rotationDegrees);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            Vector2 rel = vertices[i] - new Vector2(48f, 48f) * 0.5f;
            Vector2 rotated = new Vector2(
                rel.X * cos - rel.Y * sin + objCenter.X,
                rel.X * sin + rel.Y * cos + objCenter.Y
            );

            if (rotated.X < minX) minX = rotated.X;
            if (rotated.X > maxX) maxX = rotated.X;
            if (rotated.Y < minY) minY = rotated.Y;
            if (rotated.Y > maxY) maxY = rotated.Y;
        }

        return new Box2(minX, minY, maxX, maxY);
    }

}