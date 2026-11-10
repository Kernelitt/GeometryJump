using GeometryJump.Core;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;

namespace GeometryJump;

internal class Program
{
    static void Main(string[] args)
    {
        var app = new MainApp(
            new GameWindowSettings() { },
            new NativeWindowSettings()
            {
                API = ContextAPI.OpenGL,
                APIVersion = new Version(3,3),
                Profile = ContextProfile.Core,
                Title = "Geometry Jump",
                ClientSize = (1240,720),
                WindowBorder = WindowBorder.Resizable,
                StartVisible = true,
            }
            );
        app.Run();
    }
}
