#version 450

// ���룺������ɫ�����ݵ�UV����
layout(location = 0) in vec2 fragUV;

// �����Ƭ����ɫ
layout(location = 0) out vec4 outColor;

// �󶨼�������Ͳ�����������󶨵���0��
layout(binding = 0) uniform sampler2D computeTexture;

void main() {
    // ������������
    outColor = texture(computeTexture, fragUV);
}