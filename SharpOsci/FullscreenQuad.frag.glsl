#version 450

// 输入：顶点着色器传递的UV坐标
layout(location = 0) in vec2 fragUV;

// 输出：片段颜色
layout(location = 0) out vec4 outColor;

// 绑定计算纹理和采样器（假设绑定到槽0）
layout(binding = 0) uniform sampler2D computeTexture;

void main() {
    // 采样计算纹理
    outColor = texture(computeTexture, fragUV);
}