// Renders the marching-cubes triangle buffer produced by FluidMesh.compute
// via Graphics.DrawProceduralIndirect. Vertices are reconstructed from
// SV_VertexID: 3 verts per triangle, struct-of-vert layout matches the
// AppendStructuredBuffer<Tri> in the compute shader.

Shader "Custom/FluidMesh"
{
    Properties
    {
        _Color("Tint", Color) = (0.86, 0.96, 1, 0.78)
        _Smoothness("Smoothness", Range(0, 1)) = 0.96
        _RefractionStrength("Refraction Strength", Range(0, 0.15)) = 0.045
        _TransmissionStrength("Transmission", Range(0, 1)) = 0.86
        _SurfaceReflection("Reflection", Range(0, 1)) = 0.22
        _LightPenetration("Light Penetration", Range(0.1, 8)) = 6.0
        _UsePerVertexColor("Use Per-Vertex Color", Range(0, 1)) = 0.12

        _Metallic("Metallic", Range(0, 1)) = 0.0
        _FresnelPower("Fresnel Power", Range(0.1, 8)) = 2.2
        _FresnelBoost("Fresnel Alpha Boost", Range(0, 1)) = 0.42
        _VolumeLighting("Volume Lighting", Range(0, 1)) = 0.55
        _ViewMarchDistance("View March Distance", Range(0.01, 8)) = 0.9
        _ViewMarchSteps("View March Steps", Range(1, 48)) = 16
        _LightMarchDistance("Light March Distance", Range(0.01, 8)) = 2.8
        _LightMarchSteps("Light March Steps", Range(1, 32)) = 10
        _RayMarchJitter("Ray March Jitter", Range(0, 1)) = 0.45
        _DensitySoftness("Density Softness", Range(0.05, 4)) = 1.45
        _OpticalDepthScale("Optical Depth Scale", Range(0.1, 32)) = 8.0
        _ViewAbsorption("View Absorption", Range(0, 8)) = 0.42
        _LightAbsorption("Light Absorption", Range(0, 8)) = 0.06
        _ScatteringStrength("Scattering Strength", Range(0, 4)) = 0.85
        _DepthAlphaBoost("Depth Alpha Boost", Range(0, 1)) = 0.3
        _DeepPhaseStrength("Deep Phase Strength", Range(0, 1)) = 0.04
        _ScatterPhaseStrength("Scatter Phase Strength", Range(0, 1)) = 0.9
        _IndexOfRefraction("Index Of Refraction", Range(1, 1.6)) = 1.333
        _RefractionSteps("Refraction Steps", Range(1, 4)) = 4
        _ExitSearchDistance("Exit Search Distance", Range(0.01, 8)) = 1.6
        _ExitSearchSteps("Exit Search Steps", Range(4, 64)) = 24
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200

        // Built-in render pipeline refraction source. Captures opaque scene
        // color before this transparent fluid draws, then the forward pass
        // samples it with a normal-based offset.
        GrabPass { "_FluidMeshGrabTexture" }

        // Depth pre-pass: writes only depth, no color. Without this the
        // transparent pass draws triangles in Append-buffer order and back
        // faces / far blobs paint over near surfaces, producing "internal
        // hole" artifacts. Keep this two-sided because the marching-cubes
        // winding can be opposite Unity's back-face convention depending on
        // the cube case; the depth buffer still keeps only the nearest surface.
        Pass
        {
            Tags { "LightMode" = "Always" }
            Cull Off
            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma vertex   vertDepth
            #pragma fragment fragDepth
            #pragma target 5.0
            #include "UnityCG.cginc"

            struct VertD { float3 position; float3 normal; float3 color; };
            struct TriD  { VertD v0; VertD v1; VertD v2; };
            StructuredBuffer<TriD> _Triangles;

            float4 vertDepth(uint vid : SV_VertexID) : SV_POSITION
            {
                TriD t = _Triangles[vid / 3];
                VertD v;
                uint c = vid % 3;
                if (c == 0) v = t.v0;
                else if (c == 1) v = t.v1;
                else v = t.v2;
                return mul(UNITY_MATRIX_VP, float4(v.position, 1.0));
            }

            float4 fragDepth() : SV_Target { return 0; }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma target 5.0

            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"

            struct Vert
            {
                float3 position;
                float3 normal;
                float3 color;
            };
            struct Tri
            {
                Vert v0;
                Vert v1;
                Vert v2;
            };

            StructuredBuffer<Tri> _Triangles;
            StructuredBuffer<uint> _DensityVolume;
            sampler2D _FluidMeshGrabTexture;
            float4 _Color;
            float  _Smoothness;
            float  _Metallic;
            float  _RefractionStrength;
            float  _TransmissionStrength;
            float  _SurfaceReflection;
            float  _LightPenetration;
            float  _FresnelPower;
            float  _FresnelBoost;
            float  _UsePerVertexColor;
            float  _VolumeLighting;
            float  _ViewMarchDistance;
            float  _ViewMarchSteps;
            float  _LightMarchDistance;
            float  _LightMarchSteps;
            float  _RayMarchJitter;
            float  _DensitySoftness;
            float  _OpticalDepthScale;
            float  _ViewAbsorption;
            float  _LightAbsorption;
            float  _ScatteringStrength;
            float  _DepthAlphaBoost;
            float  _DeepPhaseStrength;
            float  _ScatterPhaseStrength;
            float  _IndexOfRefraction;
            float  _RefractionSteps;
            float  _ExitSearchDistance;
            float  _ExitSearchSteps;
            float3 _VolumeOrigin;
            float3 _VolumeSize;
            float3 _VoxelSize;
            float4 _Resolution;
            float  _DensityScale;
            float  _IsoLevel;

            uint3 VolumeResolution()
            {
                return uint3((uint)_Resolution.x, (uint)_Resolution.y, (uint)_Resolution.z);
            }

            uint VoxelIndex(uint3 c)
            {
                uint3 r = VolumeResolution();
                return c.x + c.y * r.x + c.z * r.x * r.y;
            }

            float SampleDensityVoxel(int3 c)
            {
                uint3 r = VolumeResolution();
                int3 maxC = int3(r) - 1;
                c = clamp(c, int3(0, 0, 0), maxC);
                return (float)_DensityVolume[VoxelIndex(uint3(c))] / max(_DensityScale, 1.0);
            }

            float SampleDensityWorld(float3 worldPos)
            {
                float3 grid = (worldPos - _VolumeOrigin) / _VoxelSize;
                float3 maxGrid = _Resolution.xyz - 1.0;
                if (any(grid < 0.0) || any(grid > maxGrid)) return 0.0;

                int3 c0 = int3(floor(grid));
                float3 f = saturate(grid - floor(grid));
                int3 c1 = c0 + 1;

                float d000 = SampleDensityVoxel(c0 + int3(0, 0, 0));
                float d100 = SampleDensityVoxel(int3(c1.x, c0.y, c0.z));
                float d010 = SampleDensityVoxel(int3(c0.x, c1.y, c0.z));
                float d110 = SampleDensityVoxel(int3(c1.x, c1.y, c0.z));
                float d001 = SampleDensityVoxel(int3(c0.x, c0.y, c1.z));
                float d101 = SampleDensityVoxel(int3(c1.x, c0.y, c1.z));
                float d011 = SampleDensityVoxel(int3(c0.x, c1.y, c1.z));
                float d111 = SampleDensityVoxel(c1);

                float d00 = lerp(d000, d100, f.x);
                float d10 = lerp(d010, d110, f.x);
                float d01 = lerp(d001, d101, f.x);
                float d11 = lerp(d011, d111, f.x);
                float d0 = lerp(d00, d10, f.y);
                float d1 = lerp(d01, d11, f.y);
                return lerp(d0, d1, f.z);
            }

            bool InsideVolume(float3 worldPos)
            {
                float3 p = (worldPos - _VolumeOrigin) / _VolumeSize;
                return all(p >= 0.0) && all(p <= 1.0);
            }

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float FluidOccupancy(float density)
            {
                float occupancy = saturate((density - _IsoLevel * 0.2) / max(_IsoLevel * _DensitySoftness, 1e-4));
                return occupancy * occupancy * (3.0 - 2.0 * occupancy);
            }

            float3 DensityGradient(float3 worldPos)
            {
                float3 e = max(_VoxelSize, float3(1e-4, 1e-4, 1e-4));
                return float3(
                    SampleDensityWorld(worldPos + float3(e.x, 0.0, 0.0)) - SampleDensityWorld(worldPos - float3(e.x, 0.0, 0.0)),
                    SampleDensityWorld(worldPos + float3(0.0, e.y, 0.0)) - SampleDensityWorld(worldPos - float3(0.0, e.y, 0.0)),
                    SampleDensityWorld(worldPos + float3(0.0, 0.0, e.z)) - SampleDensityWorld(worldPos - float3(0.0, 0.0, e.z)));
            }

            float3 DensitySurfaceNormal(float3 worldPos, float3 fallbackNormal)
            {
                float3 gradient = DensityGradient(worldPos);
                float lenSq = dot(gradient, gradient);
                float3 normal = lenSq > 1e-8 ? -normalize(gradient) : normalize(fallbackNormal);
                return dot(normal, fallbackNormal) < 0.0 ? -normal : normal;
            }

            float3 SafeRefract(float3 incident, float3 normal, float eta)
            {
                float3 refracted = refract(incident, normal, eta);
                return dot(refracted, refracted) > 1e-6
                    ? normalize(refracted)
                    : normalize(reflect(incident, normal));
            }

            float TraceFluidExit(
                float3 start,
                float3 dir,
                float distance,
                float stepCount,
                float jitter01,
                float3 fallbackNormal,
                out float3 exitPos,
                out float3 exitNormal,
                out float thickness)
            {
                int steps = (int)clamp(stepCount, 4.0, 64.0);
                int refineSteps = (int)clamp(_RefractionSteps, 1.0, 4.0);
                float stepLen = distance / max((float)steps, 1.0);
                float3 prevP = start;
                float3 p = start + dir * (stepLen * lerp(0.25, 0.75, jitter01));
                float prevOccupancy = FluidOccupancy(SampleDensityWorld(prevP));
                bool sawFluid = prevOccupancy > 0.04;
                thickness = prevOccupancy * stepLen;

                [loop]
                for (int s = 0; s < 64; s++)
                {
                    if (s >= steps) break;

                    if (!InsideVolume(p))
                    {
                        exitPos = prevP;
                        exitNormal = DensitySurfaceNormal(prevP, fallbackNormal);
                        return sawFluid ? 1.0 : 0.0;
                    }

                    float occupancy = FluidOccupancy(SampleDensityWorld(p));
                    sawFluid = sawFluid || occupancy > 0.04;
                    thickness += occupancy * stepLen;

                    if (sawFluid && prevOccupancy > 0.04 && occupancy <= 0.04)
                    {
                        float3 lo = prevP;
                        float3 hi = p;

                        [loop]
                        for (int r = 0; r < 4; r++)
                        {
                            if (r >= refineSteps) break;
                            float3 mid = (lo + hi) * 0.5;
                            float midOccupancy = FluidOccupancy(SampleDensityWorld(mid));
                            if (midOccupancy > 0.04) lo = mid;
                            else hi = mid;
                        }

                        exitPos = (lo + hi) * 0.5;
                        exitNormal = DensitySurfaceNormal(exitPos, fallbackNormal);
                        return 1.0;
                    }

                    prevP = p;
                    prevOccupancy = occupancy;
                    p += dir * stepLen;
                }

                exitPos = prevP;
                exitNormal = DensitySurfaceNormal(prevP, fallbackNormal);
                return sawFluid ? 1.0 : 0.0;
            }

            float4 EntryExitRefractGrabPos(
                float4 frontGrabPos,
                float3 frontWorldPos,
                float3 frontNormal,
                float3 viewRay,
                float maxVoxel,
                float viewTransmittance,
                float jitter01)
            {
                float ior = max(_IndexOfRefraction, 1.0001);
                int refractionSteps = (int)clamp(_RefractionSteps, 1.0, 4.0);
                float3 entryDir = SafeRefract(viewRay, frontNormal, 1.0 / ior);

                float3 exitPos;
                float3 exitNormal;
                float thickness;
                float hitExit = TraceFluidExit(
                    frontWorldPos + entryDir * maxVoxel,
                    entryDir,
                    _ExitSearchDistance,
                    _ExitSearchSteps,
                    jitter01,
                    entryDir,
                    exitPos,
                    exitNormal,
                    thickness);

                float2 frontUv = frontGrabPos.xy / max(frontGrabPos.w, 1e-4);
                float3 viewN = normalize(mul((float3x3)UNITY_MATRIX_V, frontNormal));
                float distortion = _RefractionStrength * (0.35 + 0.65 * (1.0 - viewTransmittance));
                float2 offset = viewN.xy * distortion;

                if (hitExit > 0.5)
                {
                    float3 exitInterfaceNormal = dot(exitNormal, entryDir) > 0.0 ? -exitNormal : exitNormal;
                    float3 exitDir = SafeRefract(entryDir, exitInterfaceNormal, ior);

                    [loop]
                    for (int step = 1; step < 4; step++)
                    {
                        if (step >= refractionSteps) break;
                        float3 probePos = exitPos + exitDir * (maxVoxel * (float)step);
                        float3 probeNormal = DensitySurfaceNormal(probePos, exitNormal);
                        float3 probeInterfaceNormal = dot(probeNormal, entryDir) > 0.0 ? -probeNormal : probeNormal;
                        float3 refinedDir = SafeRefract(entryDir, probeInterfaceNormal, ior);
                        exitDir = normalize(lerp(exitDir, refinedDir, 0.35));
                    }

                    float4 exitClip = mul(UNITY_MATRIX_VP, float4(exitPos, 1.0));
                    float4 exitGrab = ComputeGrabScreenPos(exitClip);
                    float2 exitUv = exitGrab.xy / max(exitGrab.w, 1e-4);
                    float3 viewExitDir = normalize(mul((float3x3)UNITY_MATRIX_V, exitDir));
                    float pathStrength = saturate(_RefractionStrength * 28.0);
                    float thickness01 = saturate(thickness / max(_ExitSearchDistance, 1e-4));

                    offset += (exitUv - frontUv) * pathStrength;
                    offset += viewExitDir.xy * _RefractionStrength * thickness01 * 0.75;
                }

                float4 refractPos = frontGrabPos;
                refractPos.xy = (frontUv + offset) * refractPos.w;
                return refractPos;
            }

            float IntegrateDensity(float3 start, float3 dir, float distance, float stepCount, float jitter01)
            {
                int steps = (int)clamp(stepCount, 1.0, 64.0);
                float stepLen = distance / max((float)steps, 1.0);
                float sampleOffset = lerp(0.5, jitter01, _RayMarchJitter);
                float3 p = start + dir * (stepLen * sampleOffset);
                float acc = 0.0;

                [loop]
                for (int s = 0; s < 64; s++)
                {
                    if (s >= steps) break;
                    if (!InsideVolume(p)) break;
                    // March optical thickness, not raw particle density.
                    // Raw density can be many times the iso value in packed
                    // regions and immediately makes transmittance black. This
                    // maps density around the iso-surface to a bounded 0..1
                    // occupancy so opacity follows how much fluid the ray
                    // crosses, not how many particles happened to splat into
                    // one voxel.
                    float occupancy = FluidOccupancy(SampleDensityWorld(p));
                    acc += occupancy * stepLen * _OpticalDepthScale;
                    p += dir * stepLen;
                }
                return acc;
            }

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldN   : TEXCOORD1;
                float3 vColor   : TEXCOORD2;
                float4 grabPos  : TEXCOORD4;
                SHADOW_COORDS(3)
            };

            v2f vert(uint vid : SV_VertexID)
            {
                Tri t = _Triangles[vid / 3];
                Vert v;
                uint corner = vid % 3;
                if (corner == 0) v = t.v0;
                else if (corner == 1) v = t.v1;
                else v = t.v2;

                v2f o;
                o.worldPos = v.position;
                o.worldN   = normalize(v.normal);
                o.vColor   = v.color;
                o.pos      = mul(UNITY_MATRIX_VP, float4(o.worldPos, 1.0));
                o.grabPos  = ComputeGrabScreenPos(o.pos);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i, float facing : VFACE) : SV_Target
            {
                // Marching cubes can emit either winding, so we flip the
                // shading normal to whichever side of the surface the camera
                // is looking at. This makes the iso-surface appear solid from
                // both inside and outside without producing a transparent /
                // back-face-only look.
                float3 N = normalize(i.worldN);
                if (facing < 0) N = -N;
                float3 L = _WorldSpaceLightPos0.w == 0.0
                    ? normalize(_WorldSpaceLightPos0.xyz)
                    : normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 H = normalize(L + V);

                float ndotl = saturate(dot(N, L));
                float ndoth = saturate(dot(N, H));

                // Cheap Blinn-Phong with the smoothness slider feeding exponent.
                float exponent = lerp(8.0, 256.0, _Smoothness);
                float spec = pow(ndoth, exponent);

                float shadow = SHADOW_ATTENUATION(i);

                // Per-vertex color comes from particle phase (averaged across
                // contributing particles in the splat). _Color tints it; set
                // _UsePerVertexColor = 0 to use _Color alone.
                float3 baseCol = lerp(_Color.rgb, i.vColor * _Color.rgb, _UsePerVertexColor);

                float3 diffuse = baseCol * (ndotl * shadow + 0.25);
                float3 specCol = lerp(float3(0.04, 0.04, 0.04), baseCol, _Metallic);
                float3 col = diffuse + specCol * spec * shadow;

                float maxVoxel = max(max(_VoxelSize.x, _VoxelSize.y), _VoxelSize.z);
                float3 viewRay = normalize(i.worldPos - _WorldSpaceCameraPos);
                float jitter = Hash12(i.pos.xy);
                float viewDensity = IntegrateDensity(i.worldPos + viewRay * maxVoxel, viewRay, _ViewMarchDistance, _ViewMarchSteps, jitter);
                float lightDensity = IntegrateDensity(i.worldPos + L * maxVoxel, L, _LightMarchDistance, _LightMarchSteps, frac(jitter + 0.37));

                float penetration = max(_LightPenetration, 0.1);
                float viewTransmittance = exp(-viewDensity * (_ViewAbsorption / sqrt(penetration)));
                float lightTransmittance = exp(-lightDensity * (_LightAbsorption / penetration));
                float phaseForward = pow(saturate(dot(viewRay, L)) * 0.5 + 0.5, 2.0);
                float scatter = (1.0 - viewTransmittance) * lightTransmittance * phaseForward * _ScatteringStrength * sqrt(penetration);

                // Absorption/scattering follow the phase/body color from the
                // splatted particle colors. The material tint (_Color) still
                // acts as a global multiplier, but deep and scattered light no
                // longer drift toward a separate fixed blue/white color.
                float effectiveDeepStrength = saturate(_DeepPhaseStrength / penetration);
                float3 deepPhaseCol = baseCol * lerp(1.0, 0.45, effectiveDeepStrength);
                float3 scatterPhaseCol = lerp(float3(1.0, 1.0, 1.0), baseCol, _ScatterPhaseStrength);

                float3 ambientTransmission = UNITY_LIGHTMODEL_AMBIENT.rgb + baseCol * (0.18 + 0.08 * saturate(penetration / 4.0));
                float3 absorbed = lerp(deepPhaseCol, baseCol, viewTransmittance);
                float3 propagated = absorbed * (0.35 + 0.65 * lightTransmittance)
                                  + ambientTransmission * (1.0 - lightTransmittance) * 0.35
                                  + _LightColor0.rgb * scatterPhaseCol * scatter;
                float3 volumeCol = lerp(col, propagated + specCol * spec * shadow, _VolumeLighting);

                // Fresnel: edges of the silhouette become more opaque, like
                // looking through a glass of water - the rim reads as solid
                // while the centre stays see-through.
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                float4 refractPos = EntryExitRefractGrabPos(i.grabPos, i.worldPos, N, viewRay, maxVoxel, viewTransmittance, jitter);
                float3 refractedScene = tex2Dproj(_FluidMeshGrabTexture, UNITY_PROJ_COORD(refractPos)).rgb;

                // Beer-Lambert style transmission: the scene remains visible,
                // but longer/thicker fluid paths bias it toward the phase color.
                float3 transmissionTint = lerp(baseCol, float3(1.0, 1.0, 1.0), viewTransmittance);
                float3 transmitted = refractedScene * transmissionTint;

                float3 reflectDir = reflect(-V, N);
                float4 envSample = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflectDir);
                float3 envReflection = DecodeHDR(envSample, unity_SpecCube0_HDR);
                float reflectionWeight = _SurfaceReflection * (0.15 + 0.85 * fres) * (0.35 + 0.65 * _Smoothness);

                float transmissionWeight = _TransmissionStrength * saturate(0.2 + 0.8 * viewTransmittance);
                col = lerp(volumeCol, transmitted, transmissionWeight)
                    + envReflection * reflectionWeight;

                // Transparency follows ray-marched optical depth, with the
                // material tint alpha acting as a small surface-visibility
                // floor. Clear water should transmit the scene, but it still
                // needs enough blended coverage for refraction, highlights,
                // and the silhouette to be readable.
                float opticalAlpha = 1.0 - viewTransmittance;
                float thicknessAlpha = opticalAlpha * (1.0 + _DepthAlphaBoost);
                float surfaceAlpha = saturate(_Color.a) * lerp(0.28, 0.62, fres);
                float fresnelAlpha = fres * _FresnelBoost * max(opticalAlpha, saturate(_Color.a));
                float alpha = saturate(max(thicknessAlpha, surfaceAlpha) + fresnelAlpha);

                return float4(col, alpha);
            }
            ENDCG
        }

        // Shadow caster for shadow map generation.
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off

            CGPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #pragma target 5.0

            #include "UnityCG.cginc"

            struct Vert { float3 position; float3 normal; float3 color; };
            struct Tri  { Vert v0; Vert v1; Vert v2; };
            StructuredBuffer<Tri> _Triangles;

            struct v2fShadow
            {
                V2F_SHADOW_CASTER;
            };

            v2fShadow vertShadow(uint vid : SV_VertexID)
            {
                Tri t = _Triangles[vid / 3];
                Vert v;
                uint corner = vid % 3;
                if (corner == 0) v = t.v0;
                else if (corner == 1) v = t.v1;
                else v = t.v2;

                v2fShadow o;
                float4 worldPos = float4(v.position, 1.0);
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                o.pos = UnityApplyLinearShadowBias(o.pos);
                return o;
            }

            float4 fragShadow(v2fShadow i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
    FallBack Off
}
