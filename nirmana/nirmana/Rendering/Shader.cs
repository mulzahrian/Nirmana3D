using System;
using OpenTK;
using OpenTK.Graphics.OpenGL4;

namespace nirmana.Rendering
{
    /// <summary>
    /// Wrapper untuk compile & link shader program, plus helper set uniform.
    /// </summary>
    public class Shader : IDisposable
    {
        public int Handle { get; private set; }

        public Shader(string vertexSource, string fragmentSource)
        {
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexSource);
            CompileAndCheck(vertexShader, "VERTEX");

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentSource);
            CompileAndCheck(fragmentShader, "FRAGMENT");

            Handle = GL.CreateProgram();
            GL.AttachShader(Handle, vertexShader);
            GL.AttachShader(Handle, fragmentShader);
            GL.LinkProgram(Handle);

            GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(Handle);
                throw new Exception("Shader link error: " + infoLog);
            }

            GL.DetachShader(Handle, vertexShader);
            GL.DetachShader(Handle, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
        }

        private static void CompileAndCheck(int shader, string name)
        {
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                throw new Exception(name + " shader compile error: " + infoLog);
            }
        }

        public void Use() => GL.UseProgram(Handle);

        public void SetMatrix4(string name, Matrix4 matrix)
        {
            int location = GL.GetUniformLocation(Handle, name);
            GL.UniformMatrix4(location, false, ref matrix);
        }

        public void SetVector3(string name, Vector3 vector)
        {
            int location = GL.GetUniformLocation(Handle, name);
            GL.Uniform3(location, vector);
        }

        public void SetInt(string name, int value)
        {
            int location = GL.GetUniformLocation(Handle, name);
            GL.Uniform1(location, value);
        }

        public void Dispose()
        {
            GL.DeleteProgram(Handle);
        }
    }

    /// <summary>
    /// Kumpulan source GLSL bawaan supaya tidak perlu mengurus file .vert/.frag
    /// terpisah di awal (menghindari masalah "copy to output directory").
    /// </summary>
    public static class ShaderSource
    {
        public const string BasicVertex = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vNormal;
out vec3 vFragPos;
out vec2 vTexCoord;

void main()
{
    vFragPos = vec3(uModel * vec4(aPosition, 1.0));
    vNormal = mat3(transpose(inverse(uModel))) * aNormal;
    vTexCoord = aTexCoord;
    gl_Position = uProjection * uView * vec4(vFragPos, 1.0);
}
";

        public const string BasicFragment = @"
#version 330 core
in vec3 vNormal;
in vec3 vFragPos;
in vec2 vTexCoord;

uniform vec3 uLightDir;
uniform vec3 uObjectColor;
uniform vec3 uViewPos;

out vec4 FragColor;

void main()
{
    vec3 normal = normalize(vNormal);
    vec3 lightDir = normalize(-uLightDir);

    float ambientStrength = 0.25;
    vec3 ambient = ambientStrength * vec3(1.0);

    float diff = max(dot(normal, lightDir), 0.0);
    vec3 diffuse = diff * vec3(1.0);

    vec3 viewDir = normalize(uViewPos - vFragPos);
    vec3 reflectDir = reflect(-lightDir, normal);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32.0);
    vec3 specular = spec * 0.2 * vec3(1.0);

    vec3 result = (ambient + diffuse + specular) * uObjectColor;
    FragColor = vec4(result, 1.0);
}
";

        public const string LineVertex = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aColor;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vColor;

void main()
{
    vColor = aColor;
    gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
}
";

        public const string LineFragment = @"
#version 330 core
in vec3 vColor;
out vec4 FragColor;

void main()
{
    FragColor = vec4(vColor, 1.0);
}
";
    }
}