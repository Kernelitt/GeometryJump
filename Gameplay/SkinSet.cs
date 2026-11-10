using OpenTK.Mathematics;

namespace GeometryJump.Gameplay;

internal class SkinSet
{
    public int SelectedCubeId { get; set; } = 1;
    public int SelectedShipId { get; set; } = 1;

    public Vector4 Color1 { get; set; } = new Vector4(0.0f, 1.0f, 0.4f, 1.0f);
    public Vector4 Color2 { get; set; } = new Vector4(1.0f, 0.9f, 0.0f, 1.0f);

    public static SkinSet Current { get; set; } = new SkinSet();
}
