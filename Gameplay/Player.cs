using GeometryJump.Core;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace GeometryJump.Gameplay;

public enum GameMode { Cube, Ship, Ball }

internal class Player
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 Size { get; } = new Vector2(48f, 48f);

    public float Rotation { get; private set; }
    public bool IsGrounded { get; private set; }
    public float GravitySign { get; set; } = 1.0f;
    public GameMode CurrentMode { get; set; } = GameMode.Cube; 

    // Физика куба
    private const float Gravity = -3000f;
    private const float JumpVelocity = 800f;
    private const float BaseXSpeed = 400f;

    // Физика корабля
    private const float ShipFlyForce = 2500f;    
    private const float ShipGravity = -1800f;   
    private const float ShipTerminalVelocity = 600f; 

    public Vector4 PrimaryColor { get; set; } = new Vector4(0.0f, 1.0f, 0.4f, 1.0f);
    public Vector4 SecondaryColor { get; set; } = new Vector4(1.0f, 0.9f, 0.0f, 1.0f);

    public Box2 BlockHitbox
    {
        get
        {
            float narrowWidth = Size.X / 3f;
            float offsetX = (Size.X - narrowWidth) / 2f;
            return new Box2(Position.X + offsetX, Position.Y, Position.X + offsetX + narrowWidth, Position.Y + Size.Y);
        }
    }

    public Box2 FullHitbox => new Box2(Position.X, Position.Y, Position.X + Size.X, Position.Y + Size.Y);
    public bool HasBufferFired { get; set; } = false;

    public Player(Vector2 startPosition)
    {
        Position = startPosition;
        Velocity = new Vector2(BaseXSpeed, 0);
    }

    public void SetGrounded(bool grounded)
    {
        IsGrounded = grounded;
    }

    public void ApplyVerticalImpulse(float impulse)
    {
        float targetVelocityY = impulse * GravitySign;
        if (GravitySign > 0)
        {
            if (Velocity.Y < targetVelocityY) Velocity.Y = targetVelocityY;
            Position.Y += 2f;
        }
        else
        {
            if (Velocity.Y > targetVelocityY) Velocity.Y = targetVelocityY;
            Position.Y -= 2f;
        }
        IsGrounded = false;
    }

    public void FlipGravity()
    {
        GravitySign = -GravitySign;

        if (GravitySign < 0)
        {
            Position.Y -= 6f;
        }
        else
        {
            Position.Y += 6f;
        }

        Velocity.Y = -Velocity.Y / 2;
        IsGrounded = false;
    }


    public void Update(float deltaTime, KeyboardState keyboard)
    {
        Velocity.X = BaseXSpeed;
        bool isInputHeld = keyboard.IsKeyDown(Keys.Space) || keyboard.IsKeyDown(Keys.Up);

        if (!isInputHeld)
        {
            HasBufferFired = false;
        }

        if (CurrentMode == GameMode.Cube)
        {
            if (!IsGrounded)
            {
                Velocity.Y += Gravity * GravitySign * deltaTime;
                if (GravitySign > 0 && Velocity.Y < -1200f) Velocity.Y = -1200f;
                if (GravitySign < 0 && Velocity.Y > 1200f) Velocity.Y = 1200f;
                Rotation -= 360f * GravitySign * deltaTime;
            }
            else
            {
                Velocity.Y = 0;
                float targetRot = MathF.Round(Rotation / 90f) * 90f;
                Rotation = MathHelper.Lerp(Rotation, targetRot, 0.3f);
            }
            if (IsGrounded && isInputHeld)
            {
                Velocity.Y = JumpVelocity * GravitySign;
                IsGrounded = false;
            }
        }
        else if (CurrentMode == GameMode.Ship)
        {
            Rotation = MathHelper.Lerp(Rotation, (Velocity.Y / ShipTerminalVelocity) * 30f, 0.2f);
            if (isInputHeld) Velocity.Y += ShipFlyForce * GravitySign * deltaTime;
            else Velocity.Y += ShipGravity * GravitySign * deltaTime;

            Velocity.Y = Math.Clamp(Velocity.Y, -ShipTerminalVelocity, ShipTerminalVelocity);
            IsGrounded = false;
        }
        else if (CurrentMode == GameMode.Ball)
        {
            Rotation -= 400f * GravitySign * deltaTime;

            if (!IsGrounded)
            {
                Velocity.Y += Gravity * GravitySign * deltaTime;
                if (GravitySign > 0 && Velocity.Y < -1200f) Velocity.Y = -1200f;
                if (GravitySign < 0 && Velocity.Y > 1200f) Velocity.Y = 1200f;
            }
            else
            {
                Velocity.Y = 0;
            }

            if (IsGrounded && isInputHeld && !HasBufferFired)
            {
                HasBufferFired = true;
                GravitySign = -GravitySign;
                Velocity.Y = 0f;
                if (GravitySign < 0)
                {
                    Position.Y += 1f;
                }
                else
                {
                    Position.Y -= 1f;
                }
                IsGrounded = false;
            }
        }

        Position += Velocity * deltaTime;

        float floorY = 100f;
        if (GravitySign > 0 && Position.Y <= floorY)
        {
            Position.Y = floorY;
            Velocity.Y = 0;
            IsGrounded = true;
        }
    }

    public void Render()
    {
        int texId = AssetManager.TextureAtlasId;
        if (texId == 0)
        {
            Core.BatchRenderer.DrawQuad(Position, Size, new Vector4(0, 0, 1, 1), SkinSet.Current.Color1, 0, Rotation);
            return;
        }

        Vector4 FlipUVIfInverted(Vector4 uv) =>
            GravitySign < 0 ? new Vector4(uv.X, uv.W, uv.Z, uv.Y) : uv;

        // Извлекаем ключи на основе выбранного ID в гараже скинов
        string cubeBaseKey = $"icon_{SkinSet.Current.SelectedCubeId}_1"; 
        string cubeExtraKey = $"icon_{SkinSet.Current.SelectedCubeId}_2"; 

        if (CurrentMode == GameMode.Cube)
        {
            Vector4 baseUV = AssetManager.TextureRegions.TryGetValue(cubeBaseKey, out var bUV) ? bUV : AssetManager.TextureRegions["icon_1_1"];
            Vector4 extraUV = AssetManager.TextureRegions.TryGetValue(cubeExtraKey, out var eUV) ? eUV : AssetManager.TextureRegions["icon_1_2"];

            Core.BatchRenderer.DrawQuad(Position, Size, FlipUVIfInverted(baseUV), SkinSet.Current.Color1, texId, Rotation);
            Core.BatchRenderer.DrawQuad(Position, Size, FlipUVIfInverted(extraUV), SkinSet.Current.Color2, texId, Rotation);
        }
        else if (CurrentMode == GameMode.Ship)
        {
            Vector2 cubeSize = Size * 0.65f;
            float offsetModY = GravitySign > 0 ? 0.6f : 0.4f;
            Vector2 cubeOffset = new Vector2((Size.X - cubeSize.X) * 0.5f, (Size.Y - cubeSize.Y) * offsetModY);

            Vector4 cubeBaseUV = AssetManager.TextureRegions.TryGetValue(cubeBaseKey, out var cbUV) ? cbUV : AssetManager.TextureRegions["icon_1_1"];
            Vector4 cubeTextUV = AssetManager.TextureRegions.TryGetValue(cubeExtraKey, out var ceUV) ? ceUV : AssetManager.TextureRegions["icon_1_2"];

            string shipBaseKey = $"ship_{SkinSet.Current.SelectedShipId}_1";
            string shipExtraKey = $"ship_{SkinSet.Current.SelectedShipId}_2";

            Vector4 shipBaseUV = AssetManager.TextureRegions.TryGetValue(shipBaseKey, out var sbUV) ? sbUV : AssetManager.TextureRegions["ship_1_1"];
            Vector4 shipExtraUV = AssetManager.TextureRegions.TryGetValue(shipExtraKey, out var seUV) ? seUV : AssetManager.TextureRegions["ship_1_2"];

            Core.BatchRenderer.DrawQuad(Position + cubeOffset, cubeSize, FlipUVIfInverted(cubeBaseUV), SkinSet.Current.Color1, texId, Rotation);
            Core.BatchRenderer.DrawQuad(Position + cubeOffset, cubeSize, FlipUVIfInverted(cubeTextUV), SkinSet.Current.Color2, texId, Rotation);

            Core.BatchRenderer.DrawQuad(Position, Size, FlipUVIfInverted(shipBaseUV), SkinSet.Current.Color1, texId, Rotation);
            Core.BatchRenderer.DrawQuad(Position, Size, FlipUVIfInverted(shipExtraUV), SkinSet.Current.Color2, texId, Rotation);
        }
        else if (CurrentMode == GameMode.Ball)
        {
            string ballBaseKey = $"ball_{SkinSet.Current.SelectedBallId}_1";
            string ballExtraKey = $"ball_{SkinSet.Current.SelectedBallId}_2";

            Vector4 ballBaseUV = AssetManager.TextureRegions.TryGetValue(ballBaseKey, out var bbUV) ? bbUV : AssetManager.TextureRegions["icon_1_1"];
            Vector4 ballExtraUV = AssetManager.TextureRegions.TryGetValue(ballExtraKey, out var beUV) ? beUV : AssetManager.TextureRegions["icon_1_2"];

            Core.BatchRenderer.DrawQuad(Position, Size, FlipUVIfInverted(ballBaseUV), SkinSet.Current.Color1, texId, Rotation);
            Core.BatchRenderer.DrawQuad(Position, Size, FlipUVIfInverted(ballExtraUV), SkinSet.Current.Color2, texId, Rotation);
        }
    }

    public void Reset(Vector2 startPosition)
    {
        AudioManager.SetMusicPlaybackPosition(0f);
        Position = startPosition;
        Velocity = new Vector2(BaseXSpeed, 0);
        Rotation = 0f;
        IsGrounded = false;
        HasBufferFired = false;
        GravitySign = 1.0f;
        CurrentMode = GameMode.Cube; 
    }
}
