using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GeometryJump.Core;

internal static class BatchRenderer
{
    private const int MaxQuadCount = 1000;
    private const int MaxVertexCount = MaxQuadCount * 4;
    private const int MaxIndexCount = MaxQuadCount * 6;

    private static int _vao, _vbo, _ebo;
    private static Shader _shader = null!;
    private static Vertex[] _vertexArray = null!;
    private static int _quadCount = 0;
    private static int _currentTextureId = 0;

    private static int _whiteTextureId;

    public static void Initialize()
    {
        _vertexArray = new Vertex[MaxVertexCount];

        // --- Добавляем генерацию 1x1 белой текстуры ---
        _whiteTextureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _whiteTextureId);
        uint whitePixel = 0xFFFFFFFF;
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ref whitePixel);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        // ----------------------------------------------

        // Шейдеры (оставляем прежними)
        string vertexShaderSource = @"#version 330 core
    layout (location = 0) in vec2 aPos;
    layout (location = 1) in vec2 aTexCoords;
    layout (location = 2) in vec4 aColor;
    
    uniform mat4 uProjection;
    out vec2 FragTexCoords;
    out vec4 FragColor;

    void main() {
        FragTexCoords = aTexCoords;
        FragColor = aColor;
        gl_Position = uProjection * vec4(aPos, 0.0, 1.0);
    }";

        string fragmentShaderSource = @"#version 330 core
    in vec2 FragTexCoords;
    in vec4 FragColor;
    
    uniform sampler2D uTexture;
    out vec4 Color;

    void main() {
        Color = texture(uTexture, FragTexCoords) * FragColor;
    }";

        _shader = new Shader(vertexShaderSource, fragmentShaderSource);

        uint[] indices = new uint[MaxIndexCount];
        uint offset = 0;
        for (int i = 0; i < MaxIndexCount; i += 6)
        {
            indices[i + 0] = offset + 0;
            indices[i + 1] = offset + 1;
            indices[i + 2] = offset + 2;
            indices[i + 3] = offset + 2;
            indices[i + 4] = offset + 3;
            indices[i + 5] = offset + 0;
            offset += 4;
        }

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, MaxVertexCount * BlittableHelper.SizeOf<Vertex>(), IntPtr.Zero, BufferUsageHint.DynamicDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, MaxIndexCount * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        int stride = BlittableHelper.SizeOf<Vertex>();
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, MarshalSizeOf(typeof(Vector2)));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, MarshalSizeOf(typeof(Vector2)) * 2);
        GL.BindVertexArray(0);
    }

    public static void Begin(Matrix4 projectionMatrix)
    {
        _shader.Use();
        _shader.SetMatrix4("uProjection", ref projectionMatrix);
        _shader.SetInt("uTexture", 0);
        _quadCount = 0;
    }

    public static void DrawQuad(Vector2 position, Vector2 size, Vector4 uvRegion, Vector4 color, int textureId, float rotationDegrees = 0f)
    {
        int targetTexture = textureId == 0 ? _whiteTextureId : textureId;

        if (_quadCount >= MaxQuadCount || (_currentTextureId != 0 && _currentTextureId != targetTexture))
        {
            Flush();
        }

        _currentTextureId = targetTexture;
        int vertexOffset = _quadCount * 4;

        // Локальные вершины относительно левого нижнего угла (0, 0) до (size.X, size.Y)
        Vector2 v3 = new Vector2(0, 0);
        Vector2 v2 = new Vector2(size.X, 0);
        Vector2 v1 = new Vector2(size.X, size.Y);
        Vector2 v0 = new Vector2(0, size.Y);

        if (MathF.Abs(rotationDegrees) > 0.001f)
        {
            float rad = MathHelper.DegreesToRadians(rotationDegrees);
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);

            // Центр квада для вращения
            Vector2 center = size * 0.5f;

            Vector2 Rotate(Vector2 v)
            {
                Vector2 rel = v - center;
                return new Vector2(
                    rel.X * cos - rel.Y * sin + center.X,
                    rel.X * sin + rel.Y * cos + center.Y
                );
            }

            v0 = Rotate(v0);
            v1 = Rotate(v1);
            v2 = Rotate(v2);
            v3 = Rotate(v3);
        }

        // Смещаем на мировую позицию
        _vertexArray[vertexOffset + 0] = new Vertex(position + v0, new Vector2(uvRegion.X, uvRegion.Y), color);
        _vertexArray[vertexOffset + 1] = new Vertex(position + v1, new Vector2(uvRegion.Z, uvRegion.Y), color);
        _vertexArray[vertexOffset + 2] = new Vertex(position + v2, new Vector2(uvRegion.Z, uvRegion.W), color);
        _vertexArray[vertexOffset + 3] = new Vertex(position + v3, new Vector2(uvRegion.X, uvRegion.W), color);

        _quadCount++;
    }


    public static void End()
    {
        Flush();
    }

    private static void Flush()
    {
        if (_quadCount == 0) return;

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _currentTextureId);

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

        // Отправляем на GPU только ту часть массива, которую реально заполнили
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, _quadCount * 4 * BlittableHelper.SizeOf<Vertex>(), _vertexArray);

        GL.DrawElements(PrimitiveType.Triangles, _quadCount * 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

        GL.BindVertexArray(0);
        _quadCount = 0;
    }

    public static void Shutdown()
    {
        _shader.Dispose();
        GL.DeleteTexture(_whiteTextureId);
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ebo);
        GL.DeleteVertexArray(_vao);
    }

    private static int MarshalSizeOf(Type t) => System.Runtime.InteropServices.Marshal.SizeOf(t);
}

// Хелпер для быстрого подсчета размера структуры на чистом .NET без небезопасного кода
internal static class BlittableHelper
{
    public static int SizeOf<T>() where T : struct => System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
}
