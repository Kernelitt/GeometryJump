using System.Text.Json;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;
using GeometryJump.Level;
using OpenTK.Mathematics;

namespace GeometryJump.Core;

internal static class AssetManager
{
    private static readonly Dictionary<string, byte[]> _audioCache = new();
    private static readonly Dictionary<string, Level.Level> _levelCache = new();

    public static int TextureAtlasId { get; private set; }
    public static Dictionary<string, Vector4> TextureRegions { get; private set; } = new();

    public static void LoadTextureAtlas(string rootDirectoryPath)
    {
        if (!Directory.Exists(rootDirectoryPath))
        {
            System.Console.WriteLine($"[AssetManager] Ошибка: Директория {rootDirectoryPath} не существует!");
            return;
        }

        // Сканируем папку и все подпапки на наличие PNG файлов
        string[] texturePaths = Directory.GetFiles(rootDirectoryPath, "*.png", SearchOption.AllDirectories);

        int atlasWidth = 2048;
        int currentX = 0;
        int currentY = 0;
        int maxRowHeight = 0;

        List<(string Path, int Width, int Height)> imageInfos = new();

        foreach (var path in texturePaths)
        {
            using var stream = File.OpenRead(path);
            var info = ImageInfo.FromStream(stream);
            imageInfos.Add((path, info.Value.Width, info.Value.Height));
        }

        foreach (var img in imageInfos)
        {
            if (currentX + img.Width > atlasWidth)
            {
                currentX = 0;
                currentY += maxRowHeight;
                maxRowHeight = 0;
            }
            if (img.Height > maxRowHeight) maxRowHeight = img.Height;
            currentX += img.Width;
        }
        int atlasHeight = currentY + maxRowHeight;
        atlasHeight = MathHelper.NextPowerOfTwo(atlasHeight);

        byte[] atlasData = new byte[atlasWidth * atlasHeight * 4];

        currentX = 0;
        currentY = 0;
        maxRowHeight = 0;

        foreach (var img in imageInfos)
        {
            using var stream = File.OpenRead(img.Path);
            ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            if (currentX + img.Width > atlasWidth)
            {
                currentX = 0;
                currentY += maxRowHeight;
                maxRowHeight = 0;
            }

            if (img.Height > maxRowHeight) maxRowHeight = img.Height;

            for (int y = 0; y < img.Height; y++)
            {
                int srcOffset = y * img.Width * 4;
                int destOffset = ((currentY + y) * atlasWidth + currentX) * 4;
                Array.Copy(image.Data, srcOffset, atlasData, destOffset, img.Width * 4);
            }

            float uMin = (float)currentX / atlasWidth;
            float vMin = (float)currentY / atlasHeight;
            float uMax = (float)(currentX + img.Width) / atlasWidth;
            float vMax = (float)(currentY + img.Height) / atlasHeight;

            // ИСПРАВЛЕНО: Ключом становится ТОЛЬКО имя файла без путей и расширения
            string key = Path.GetFileNameWithoutExtension(img.Path);
            TextureRegions[key] = new Vector4(uMin, vMin, uMax, vMax);

            currentX += img.Width;
        }

        if (TextureAtlasId != 0) GL.DeleteTexture(TextureAtlasId);

        TextureAtlasId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, TextureAtlasId);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, atlasWidth, atlasHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, atlasData);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
    }

    public static byte[] GetAudioData(string path)
    {
        if (!_audioCache.TryGetValue(path, out var data))
        {
            data = File.ReadAllBytes(path);
            _audioCache[path] = data;
        }
        return data;
    }

    public static Level.Level LoadLevel(string path)
    {
        if (_levelCache.TryGetValue(path, out var level)) return level;

        string json = File.ReadAllText(path);

        var options = new JsonSerializerOptions { IncludeFields = true };
        var loadedLevel = JsonSerializer.Deserialize<Level.Level>(json, options);

        _levelCache[path] = loadedLevel;
        return loadedLevel;
    }

}
