#include "Math.cginc"

float Poly6(float distSqr, float h) {
    float coef = 315.0f / (64.0f * UNITY_PI * pow(h, 9));
    float hSqr = h * h;
    if (hSqr < distSqr) return 0.0f;
    return coef * pow(hSqr - distSqr, 3);
}

float3 GradientSpiky(float3 r, float h) {
    float coef = 45.0f / (UNITY_PI * pow(h, 6));
    float dist = length(r);
    if (h < dist) return float3(0.0f, 0.0f, 0.0f);
    return -coef * normalize(r) * pow(h - dist, 2);
}

float ViscosityLaplacian(float r, float h) {
    if (h < r) return 0.0f;
    float coef = 45.0f / (UNITY_PI * pow(h, 6));
    return coef * (h - r);
}
