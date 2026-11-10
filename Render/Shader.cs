using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GeometryJump.Core;

internal class Shader : IDisposable
{
    private readonly int _handle;

    public Shader(string vertexCode, string fragmentCode)
    {
        int vs = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vs, vertexCode);
        GL.CompileShader(vs);
        CheckShaderCompile(vs);

        int fs = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fs, fragmentCode);
        GL.CompileShader(fs);
        CheckShaderCompile(fs);

        _handle = GL.CreateProgram();
        GL.AttachShader(_handle, vs);
        GL.AttachShader(_handle, fs);
        GL.LinkProgram(_handle);
        CheckProgramLink(_handle);

        GL.DeleteShader(vs);
        GL.DeleteShader(fs);
    }

    public void Use() => GL.UseProgram(_handle);

    public void SetMatrix4(string name, ref Matrix4 matrix)
    {
        int location = GL.GetUniformLocation(_handle, name);
        GL.UniformMatrix4(location, false, ref matrix);
    }

    public void SetInt(string name, int value)
    {
        int location = GL.GetUniformLocation(_handle, name);
        GL.Uniform1(location, value);
    }

    private void CheckShaderCompile(int shader)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
        if (success == 0)
        {
            string infoLog = GL.GetShaderInfoLog(shader);
            throw new Exception($"Ошибка компиляции шейдера: {infoLog}");
        }
    }

    private void CheckProgramLink(int program)
    {
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
        if (success == 0)
        {
            string infoLog = GL.GetProgramInfoLog(program);
            throw new Exception($"Ошибка линковки шейдерной программы: {infoLog}");
        }
    }

    public void Dispose()
    {
        GL.DeleteProgram(_handle);
        GC.SuppressFinalize(this);
    }
}
