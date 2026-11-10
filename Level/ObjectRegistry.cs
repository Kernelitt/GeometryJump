using GeometryJump.Gameplay;
using OpenTK.Mathematics;

namespace GeometryJump.Level;

public enum InteractionType
{
    Solid,
    Hazard,
    Touchable,
    Interactable,
    Decorative
}

internal struct ObjectData
{
    public string TextureKey;
    public InteractionType Type;
    public Vector4 DefaultColor;
    public Vector2 HitboxOffset;
    public Vector2 HitboxSize;
    public Vector2 Scale = Vector2.One;
    public float VisualAudioScale;
    public Action<Gameplay.Player, float> OnTrigger;

    public ObjectData()
    {
    }
}

internal static class ObjectRegistry
{
    private static readonly Dictionary<int, ObjectData> _registry = new();

    static ObjectRegistry()
    {
        // 1. Обычный блок
        Register(1, new ObjectData
        {
            TextureKey = "block",
            Type = InteractionType.Solid,
            DefaultColor = Vector4.One,
            HitboxOffset = Vector2.Zero,
            HitboxSize = new Vector2(48f, 48f)
        });

        // 2. Классический Шип
        Register(2, new ObjectData
        {
            TextureKey = "spike",
            Type = InteractionType.Hazard,
            DefaultColor = Vector4.One,
            HitboxOffset = new Vector2(16f, 0f),
            HitboxSize = new Vector2(16f, 28f)
        });

        // 3. Маленький шип
        Register(3, new ObjectData
        {
            TextureKey = "spike1_2",
            Type = InteractionType.Hazard,
            DefaultColor = Vector4.One,
            HitboxOffset = new Vector2(20f, 0f),
            HitboxSize = new Vector2(8f, 12f)
        });

        // Блоки декора и платформ (4-10)
        for (int id = 4; id <= 10; id++)
        {
            if (id == 9) continue; // block1_6 пустой декоративный без коллизии
            Register(id, new ObjectData
            {
                TextureKey = $"block1_{id - 3}",
                Type = InteractionType.Solid,
                DefaultColor = Vector4.One,
                HitboxOffset = Vector2.Zero,
                HitboxSize = new Vector2(48f, 48f)
            });
        }

        // 9. Декоративный блок block1_6
        Register(9, new ObjectData
        {
            TextureKey = "block1_6",
            Type = InteractionType.Decorative,
            DefaultColor = Vector4.One,
            HitboxOffset = Vector2.Zero,
            HitboxSize = Vector2.Zero
        });

        Register(11, new ObjectData
        {
            TextureKey = "slab1_1",
            Type = InteractionType.Solid,
            DefaultColor = Vector4.One,
            HitboxOffset = new Vector2(0f, 24f),
            HitboxSize = new Vector2(48f, 24f)
        });

        // yellow
        Register(12, new ObjectData
        {
            TextureKey = "pad1",
            Type = InteractionType.Touchable,
            DefaultColor = Vector4.One,
            HitboxOffset = new Vector2(4f, 0f),
            HitboxSize = new Vector2(40f, 12f),
            OnTrigger = (player, rotation) => {
                player.ApplyVerticalImpulse(1100f);
                player.SetGrounded(false);
            }
        });


        Register(13, new ObjectData
        {
            TextureKey = "orb1",
            Type = InteractionType.Interactable,
            DefaultColor = Vector4.One,
            HitboxOffset = Vector2.Zero,
            HitboxSize = new Vector2(48f, 48f),
            OnTrigger = (player, rotation) => {
                player.ApplyVerticalImpulse(850f);
                player.SetGrounded(false);
            }
        });

        // pink
        Register(14, new ObjectData
        {
            TextureKey = "pad2",
            Type = InteractionType.Touchable,
            DefaultColor = Vector4.One,
            HitboxOffset = new Vector2(4f, 0f),
            HitboxSize = new Vector2(40f, 12f),
            OnTrigger = (player, rotation) => {
                player.ApplyVerticalImpulse(600f);
                player.SetGrounded(false);
            }
        });
        Register(15, new ObjectData
        {
            TextureKey = "orb2",
            Type = InteractionType.Interactable,
            DefaultColor = Vector4.One,
            HitboxOffset = Vector2.Zero,
            HitboxSize = new Vector2(48f, 48f),
            OnTrigger = (player, rotation) => {
                player.ApplyVerticalImpulse(800f);
                player.SetGrounded(false);
            }
        });

        // red
        Register(16, new ObjectData
        {
            TextureKey = "pad4",
            Type = InteractionType.Touchable,
            DefaultColor = Vector4.One,
            HitboxOffset = new Vector2(4f, 0f),
            HitboxSize = new Vector2(40f, 12f),
            OnTrigger = (player, rotation) => {
                player.ApplyVerticalImpulse(1500f);
                player.SetGrounded(false);
            }
        });
        Register(17, new ObjectData
        {
            TextureKey = "orb4",
            Type = InteractionType.Interactable,
            DefaultColor = Vector4.One,
            HitboxOffset = Vector2.Zero,
            HitboxSize = new Vector2(48f, 48f),
            OnTrigger = (player, rotation) => {
                player.ApplyVerticalImpulse(1800f);
                player.SetGrounded(false);
            }
        });

        // blue
        Register(18, new ObjectData
        {
            TextureKey = "pad3",
            Type = InteractionType.Touchable,
            DefaultColor = Vector4.One,
            HitboxOffset = new Vector2(4f, 0f),
            HitboxSize = new Vector2(40f, 12f),
            OnTrigger = (player, rotation) => {
                int angle = (int)MathF.Round(rotation) % 360;
                if (angle < 0) angle += 360;

                if (angle == 0)
                {
                    if (player.GravitySign > 0)
                    {
                        player.FlipGravity();
                    }
                }
                else if (angle == 180)
                {
                    if (player.GravitySign < 0)
                    {
                        player.FlipGravity();
                    }
                }
            }
        });
        Register(19, new ObjectData
        {
            TextureKey = "orb3",
            Type = InteractionType.Interactable,
            DefaultColor = Vector4.One,
            HitboxOffset = Vector2.Zero,
            HitboxSize = new Vector2(48f, 48f),
            OnTrigger = (player, rotation) => {
                player.FlipGravity();
                player.SetGrounded(false);
            }
        });


        //green
        Register(20, new ObjectData
        {
            TextureKey = "orb5",
            Type = InteractionType.Interactable,
            DefaultColor = Vector4.One,
            HitboxOffset = Vector2.Zero,
            HitboxSize = new Vector2(48f, 48f),
            OnTrigger = (player, rotation) => {
                player.FlipGravity();
                player.ApplyVerticalImpulse(700f);
                player.SetGrounded(false);
            }
        });
        Register(21, new ObjectData
        {
            TextureKey = "portal_cube", 
            Type = InteractionType.Touchable,
            DefaultColor = Vector4.One,
            HitboxOffset = new Vector2(12f, 0f),
            HitboxSize = new Vector2(24f, 96f),
            Scale = new Vector2(1, 3),
            OnTrigger = (player, rotation) => {
                if (player.CurrentMode != GameMode.Cube)
                {
                    player.CurrentMode = GameMode.Cube;
                    player.ApplyVerticalImpulse(0f);
                }
            }
        });

        Register(22, new ObjectData
        {
            TextureKey = "portal_ship",
            Type = InteractionType.Touchable,
            DefaultColor = Vector4.One,
            HitboxOffset = new Vector2(12f, 0f),
            HitboxSize = new Vector2(24f, 96f),
            Scale = new Vector2(1, 3),
            OnTrigger = (player, rotation) => {
                player.CurrentMode = GameMode.Ship;
            }
        });
    }

    public static void Register(int id, ObjectData data) => _registry[id] = data;
    public static bool TryGet(int id, out ObjectData data) => _registry.TryGetValue(id, out data);
}
