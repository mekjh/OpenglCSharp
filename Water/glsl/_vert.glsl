#version 420 core

layout(location = 0) in vec3 position;
layout(location = 1) in vec2 texCoord;

uniform mat4 model;
uniform mat4 view;
uniform mat4 proj;

uniform vec2 flowVector;
out vec2 TexCoords;

void main(void)
{
    gl_Position = proj * view * model * vec4(position, 1.0);
    TexCoords = texCoord + flowVector;
}