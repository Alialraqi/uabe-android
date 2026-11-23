namespace UABEAvalonia.Controls.MeshPreviewer
{
    /// <summary>
    /// Mesh GLSL shader source used by the previewer. Adapted from MeshPreviewerShaders.cs of UABEANext4.
    /// Uses GLSL ES 3.00, matching what Avalonia provides OpenGL ES / OpenGL context.
    /// </summary>
    public static class MeshPreviewerShaders
    {
        /// <summary>
        /// Vertex shader: MVP transform + transform the normal into world space and pass it to the fragment shader.
        /// </summary>
        public const string VERTEX_SOURCE = @"#version 300 es
precision mediump float;
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
uniform mat4 uModel;
uniform mat4 uProjection;
uniform mat4 uView;
out vec3 FragNormal;
void main()
{
    gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0);
    FragNormal = mat3(transpose(inverse(uModel))) * aNormal;
}";

        /// <summary>
        /// Fragment shader: diffuse lighting + 0.3 ambient light, outputs the final color.
        /// </summary>
        public const string FRAGMENT_SORUCE = @"#version 300 es
precision mediump float;
in vec3 FragNormal;
out vec4 FragColor;
uniform vec3 uDirectionalLightDir;
uniform vec3 uDirectionalLightColor;
void main()
{
    vec3 normal = normalize(FragNormal);
    vec3 lightDirection = normalize(uDirectionalLightDir) * 0.8;

    float diff = max(dot(normal, lightDirection), 0.0);
    vec3 diffuse = diff * uDirectionalLightColor + 0.3;

    FragColor = vec4(diffuse, 1.0);
}";

        public const int POSITION_LOC = 0;
        public const int NORMAL_LOC = 1;
    }
}
