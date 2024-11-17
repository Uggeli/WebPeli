// Define shader code globally for use by renderer
const BASIC_VERTEX_SHADER = `#version 300 es
precision mediump float;

uniform mat4 uProjection;
uniform mat4 uView;
uniform mat4 uModel;
uniform float uHeight;

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;

out vec3 vNormal;
out vec3 vPosition;

void main() {
    // Scale Y by height for different tile heights
    vec3 position = aPosition;
    position.y *= uHeight;
    
    gl_Position = uProjection * uView * uModel * vec4(position, 1.0);
    vNormal = (uModel * vec4(aNormal, 0.0)).xyz;
    vPosition = position;
}`;

const BASIC_FRAGMENT_SHADER = `#version 300 es
precision mediump float;

uniform vec3 uBaseColor;
uniform float uAmbient;

in vec3 vNormal;
in vec3 vPosition;

out vec4 fragColor;

void main() {
    vec3 lightDir = normalize(vec3(1.0, 2.0, 1.0));
    float diff = max(dot(normalize(vNormal), lightDir), 0.0);
    vec3 color = uBaseColor * (uAmbient + diff);
    fragColor = vec4(color, 1.0);
}`;