using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace GeometryJump.Core;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct Vertex
{
    public Vector2 Position;  // 8 байт
    public Vector2 TexCoords; // 8 байт
    public Vector4 Color;     // 16 байт

    public Vertex(Vector2 position, Vector2 texCoords, Vector4 color)
    {
        Position = position;
        TexCoords = texCoords;
        Color = color;
    }
}
