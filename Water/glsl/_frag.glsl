#version 420 core

uniform sampler2D textureMap;

uniform float repeatTextureDetailMap;
uniform vec3 waterColor;

in vec2 TexCoords;
out vec4 out_Color;

void main(void)
{
	out_Color = texture(textureMap, repeatTextureDetailMap * TexCoords) * vec4(waterColor, 1.0);
}