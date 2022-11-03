#version 460 core

out vec4 FragColor;
in vec3 WorldPos;

uniform samplerCube environmentMap;
uniform float gamma;

void main()
{		
    vec3 envColor = texture(environmentMap, WorldPos).rgb;


    // HDR tonemap and gamma correct
    envColor = envColor / (envColor + vec3(1.0));
    envColor = pow(envColor, vec3(1.0/gamma)); 
    
    FragColor = vec4(envColor, 1.0);

}