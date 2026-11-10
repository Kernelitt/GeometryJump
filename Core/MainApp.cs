using GeometryJump.Gameplay;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.ComponentModel;

namespace GeometryJump.Core;

internal class MainApp : GameWindow
{
    public static MainApp App { get; private set; }

    private DebugProfiler _profiler;
    private bool _showDebugMenu = false;

    private ImGuiController _imguiController;
    private MainGame _game;
    private LevelEditor _editor;
    private MainMenu _menu;
    private bool _isInGameplay = false;

    public MainApp(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings) { }

    protected override void OnLoad()
    {
        App = this;
        base.OnLoad();

        GL.ClearColor(0.3f, 0f, 0.8f, 1f);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        AudioManager.Initialize();
        _profiler = new DebugProfiler();

        _imguiController = new ImGuiController(ClientSize.X, ClientSize.Y);
        _imguiController.OnBuildUI += () =>
        {
            _profiler.RenderDebugOverlay(this);
        };
        SkinSet.LoadConfig();
        AssetManager.LoadTextureAtlas("Assets\\Textures\\");
        SkinSet.DiscoverAvailableSkins();

        BatchRenderer.Initialize();

        _game = new MainGame();
        _editor = new LevelEditor();
        _menu = new MainMenu();
        _menu.RefreshMapsList();

        _imguiController = new ImGuiController(ClientSize.X, ClientSize.Y);
        _imguiController.SetupInput(this);
        _imguiController.OnBuildUI += () =>
        {
            if (_showDebugMenu) _profiler.RenderDebugOverlay(this);

            if (_menu.IsOpen)
            {
                _menu.RenderUI(
                    onLaunchGame: (mapFile) => { _game = new MainGame(); _game.LoadLevel(mapFile); _isInGameplay = true; },
                    onEditGame: (mapFile) => { _editor.LoadLevelDirectly(mapFile); },
                    onOpenNewEditor: () => _editor.IsActive = true,
                    onVsyncToggle: (vsync) => Context.SwapInterval = vsync ? 1 : 0
                );
            }

            if (_editor.IsActive)
            {
                _editor.RenderUI(
                    onExitEditor: () => { _menu.IsOpen = true; _menu.RefreshMapsList(); },
                    onInstantPlaytest: (mapFile) => {
                        _game = new MainGame();
                        _game.LoadLevel(mapFile);
                        _isInGameplay = true; // Мгновенно переключаем движок в режим игры!
                    }
                );
            }

            // Вызов отрисовки меню победы в геймплее
            if (_isInGameplay && !_menu.IsOpen)
            {
                _game.RenderPassMenu(onReturnToMenu: () => {
                    _isInGameplay = false;
                    _menu.IsOpen = true;
                    _menu.RefreshMapsList();
                });
            }
        };


    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyPressed(Keys.F3)) _showDebugMenu = !_showDebugMenu;

        // Кнопка Escape возвращает в главное меню
        if (KeyboardState.IsKeyPressed(Keys.Escape))
        {
            AudioManager.StopMusic();
            _isInGameplay = false;
            _editor.IsActive = false;
            _menu.IsOpen = true;
            _menu.RefreshMapsList();
        }

        if (_isInGameplay && !_menu.IsOpen)
        {
            _game.Update((float)args.Time, KeyboardState);
        }
        else if (_editor.IsActive)
        {
            _editor.Update((float)args.Time, KeyboardState, MouseState, ClientSize.X, ClientSize.Y);
        }
    }


    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        if (_isInGameplay && !_menu.IsOpen)
        {
            _game.Render(ClientSize.X, ClientSize.Y);
        }
        else if (_editor.IsActive)
        {
            _editor.RenderWorld(ClientSize.X, ClientSize.Y);
        }


        _imguiController.Render();
        

        SwapBuffers();
    }


    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
        _imguiController?.WindowResized(e.Width, e.Height);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        AudioManager.Shutdown();
        BatchRenderer.Shutdown();
        base.OnClosing(e);
    }
}
