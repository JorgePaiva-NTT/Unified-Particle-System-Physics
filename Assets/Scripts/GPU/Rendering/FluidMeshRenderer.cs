using System;
using GPU;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace GPU.Rendering
{
    /// <summary>
    /// Extracts a triangle mesh from the fluid particle cloud each frame using
    /// marching cubes on a GPU density volume, and renders it via
    /// Graphics.DrawProceduralIndirect.
    ///
    /// Pipeline (per LateUpdate):
    ///   1. ClearDensity   - zero the uint volume
    ///   2. SplatParticles - one thread per fluid particle, InterlockedAdd into volume
    ///   3. (CPU)          - SetCounterValue(0) on the AppendStructuredBuffer
    ///   4. ResetCounter   - prime the indirect-args buffer
    ///   5. MarchingCubes  - one thread per voxel, append triangles
    ///   6. (CPU)          - CopyCount(append, triCount, 0)
    ///   7. BuildArgs      - vertex count = triCount * 3
    ///   8. DrawProceduralIndirect
    /// </summary>
    [DisallowMultipleComponent]
    public class FluidMeshRenderer : MonoBehaviour
    {
        [Header("Sources")]
        public GPUParticleSystem ParticleSystem;
        [Tooltip("Optional. If set, the cube's renderer bounds drive the volume. " +
                 "Otherwise VolumeOrigin/VolumeSize fields below are used.")]
        public GameObject BoundsCube;
        public Vector3 VolumeOrigin = new Vector3(-5, -5, -5);
        public Vector3 VolumeSize   = new Vector3(10, 10, 10);

        [Header("Resolution")]
        [Tooltip("Voxel grid resolution per axis. Memory cost is N^3 uints.")]
        [Range(8, 256)]
        public int Resolution = 96;
        [Tooltip("Use water-like extraction defaults: modest smoothing, less syrupy blobs, strong transmission.")]
        public bool UseWaterSurfacePreset = true;
        [Tooltip("Maximum radius, in voxels, that one particle may splat into. " +
             "Raise this when increasing Resolution so splats are not clipped.")]
        [Range(1, 16)] public int MaxSplatVoxels = 8;

        [Header("Smoothing")]
        [Tooltip("GPU blur passes applied to the density/color volume before marching cubes. " +
             "Use this to merge particle splats into continuous fluid.")]
        [Range(0, 4)] public int DensitySmoothIterations = 1;
        [Tooltip("Smoothing neighborhood radius in voxels. Radius 1 is cheap; radius 2 is much heavier.")]
        [Range(0, 2)] public int DensitySmoothRadius = 1;
        [Tooltip("How strongly each blur pass replaces the original density. 0 = none, 1 = full blur.")]
        [Range(0f, 1f)] public float DensitySmoothStrength = 0.55f;

        [Header("Iso surface")]
        [Tooltip("World-space radius of each particle's density splat.")]
        public float SplatRadius = 0.18f;
        [Tooltip("Higher values make each particle contribution tighter, reducing swollen metaball boundaries.")]
        [Range(0.5f, 10f)] public float SplatFalloffPower = 5.0f;
        [Tooltip("Density multiplier for each particle splat. Raise this when isolated particles fail to create a visible surface.")]
        [Range(0.1f, 8f)] public float SplatDensityBoost = 2.0f;
        [Tooltip("Density threshold for the extracted iso-surface (post-splat).")]
        public float IsoLevel = 0.5f;
        [Tooltip("Multiplier converting float density into the uint accumulator. " +
                 "Higher = finer precision but watch for overflow with many particles.")]
        public float DensityScale = 1024f;

        [Header("Phase filter")]
        [Tooltip("Inclusive range of phase IDs treated as fluid. Cloth/static are 0..2.")]
        public int FluidPhaseMin = 3;
        public int FluidPhaseMax = 64;
        [Tooltip("0 = average colors where phases overlap, 1 = choose the strongest phase color per voxel. " +
             "Use high values for non-mixing fluids with crisp material boundaries.")]
        [Range(0f, 1f)] public float PhaseBoundarySharpness = 1f;

        [Header("Render")]
        public Material FluidMaterial;
        public ComputeShader FluidMeshShader;
        [Tooltip("Apply the live material controls below every frame. Disable this to edit the FluidMaterial asset directly.")]
        [FormerlySerializedAs("UseWaterMaterialPreset")]
        public bool DriveFluidMaterial = true;
        [Header("Live Fluid Material")]
        public Color FluidTint = new Color(0.86f, 0.96f, 1.0f, 0.78f);
        [Range(0f, 1f)] public float Smoothness = 0.96f;
        [Range(0f, 1f)] public float Metallic = 0.0f;
        [Range(0f, 0.15f)] public float RefractionStrength = 0.045f;
        [Range(0f, 1f)] public float TransmissionStrength = 0.86f;
        [Range(0f, 1f)] public float SurfaceReflection = 0.22f;
        [Range(0.1f, 8f)] public float LightPenetration = 6.0f;
        [Range(0f, 1f)] public float UsePerVertexColor = 0.12f;
        [Range(0.1f, 8f)] public float FresnelPower = 2.2f;
        [Range(0f, 1f)] public float FresnelBoost = 0.42f;
        [Range(0f, 1f)] public float VolumeLighting = 0.55f;
        [Range(0.01f, 8f)] public float ViewMarchDistance = 0.9f;
        [Range(1, 48)] public int ViewMarchSteps = 16;
        [Range(0.01f, 8f)] public float LightMarchDistance = 2.8f;
        [Range(1, 32)] public int LightMarchSteps = 10;
        [Range(0f, 1f)] public float RayMarchJitter = 0.45f;
        [Range(0.05f, 4f)] public float DensitySoftness = 1.45f;
        [Tooltip("Scales ray-marched optical depth in the material. Higher values make one-particle-thick blobs more visible.")]
        [Range(0.1f, 32f)] public float OpticalDepthScale = 8.0f;
        [Range(0f, 8f)] public float ViewAbsorption = 0.42f;
        [Range(0f, 8f)] public float LightAbsorption = 0.06f;
        [Range(0f, 4f)] public float ScatteringStrength = 0.85f;
        [Range(0f, 1f)] public float DepthAlphaBoost = 0.3f;
        [Range(0f, 1f)] public float DeepPhaseStrength = 0.04f;
        [Range(0f, 1f)] public float ScatterPhaseStrength = 0.9f;
        [Header("Water refraction")]
        [Tooltip("Water is roughly 1.333. Higher values bend the background more strongly.")]
        [Range(1.0f, 1.6f)] public float IndexOfRefraction = 1.333f;
        [Tooltip("Refinement steps used when estimating the back/exit surface. Four is the highest quality path in this shader.")]
        [Range(1, 4)] public int RefractionSteps = 4;
        [Tooltip("Maximum world distance marched through the density volume while searching for the exit surface.")]
        [Range(0.01f, 8f)] public float ExitSearchDistance = 1.6f;
        [Tooltip("Density samples used for the entry-to-exit ray search.")]
        [Range(4, 64)] public int ExitSearchSteps = 24;
        public ShadowCastingMode ShadowCasting = ShadowCastingMode.On;
        public bool ReceiveShadows = true;

        // ---- internal ----
        private ComputeBuffer densityBuffer;
        private ComputeBuffer colorAccumBuffer;
        private ComputeBuffer dominantWeightBuffer;
        private ComputeBuffer dominantColorBuffer;
        private ComputeBuffer densityScratchBuffer;
        private ComputeBuffer colorAccumScratchBuffer;
        private ComputeBuffer triangleBuffer;
        private ComputeBuffer triCountBuffer;
        private ComputeBuffer argsBuffer;
        private ComputeBuffer triTableBuffer;

        private int kClear, kSplat, kSmooth, kReset, kMC, kBuild;
        private int allocatedRes = -1;
        private int allocatedScratchRes = -1;

        private static readonly int sIDDensity            = Shader.PropertyToID("Density");
        private static readonly int sIDColorAccum         = Shader.PropertyToID("ColorAccum");
        private static readonly int sIDDominantWeight     = Shader.PropertyToID("DominantWeight");
        private static readonly int sIDDominantColor      = Shader.PropertyToID("DominantColor");
        private static readonly int sIDDensityRead        = Shader.PropertyToID("DensityREAD");
        private static readonly int sIDColorAccumRead     = Shader.PropertyToID("ColorAccumREAD");
        private static readonly int sIDDensityWrite       = Shader.PropertyToID("DensityWRITE");
        private static readonly int sIDColorAccumWrite    = Shader.PropertyToID("ColorAccumWRITE");
        private static readonly int sIDTriangles          = Shader.PropertyToID("Triangles");
        private static readonly int sIDDrawArgs           = Shader.PropertyToID("DrawArgs");
        private static readonly int sIDTriCount           = Shader.PropertyToID("TriCount");
        private static readonly int sIDTriTable           = Shader.PropertyToID("TriTable");
        private static readonly int sIDPositions          = Shader.PropertyToID("ParticlePositions");
        private static readonly int sIDPhase              = Shader.PropertyToID("ParticlePhase");
        private static readonly int sIDColors             = Shader.PropertyToID("ParticleColors");
        private static readonly int sIDVolumeOrigin       = Shader.PropertyToID("VolumeOrigin");
        private static readonly int sIDVolumeSize         = Shader.PropertyToID("VolumeSize");
        private static readonly int sIDVoxelSize          = Shader.PropertyToID("VoxelSize");
        private static readonly int sIDResolution         = Shader.PropertyToID("Resolution");
        private static readonly int sIDSplatRadius        = Shader.PropertyToID("SplatRadius");
        private static readonly int sIDSplatFalloffPower  = Shader.PropertyToID("SplatFalloffPower");
        private static readonly int sIDSplatDensityBoost  = Shader.PropertyToID("SplatDensityBoost");
        private static readonly int sIDDensityScale       = Shader.PropertyToID("DensityScale");
        private static readonly int sIDIsoLevel           = Shader.PropertyToID("IsoLevel");
        private static readonly int sIDMaxSplatVoxels     = Shader.PropertyToID("MaxSplatVoxels");
        private static readonly int sIDSmoothingRadius    = Shader.PropertyToID("SmoothingRadiusVoxels");
        private static readonly int sIDSmoothingStrength  = Shader.PropertyToID("SmoothingStrength");
        private static readonly int sIDPhaseBoundarySharpness = Shader.PropertyToID("PhaseBoundarySharpness");
        private static readonly int sIDFluidNumParticles  = Shader.PropertyToID("FluidNumParticles");
        private static readonly int sIDFluidPhaseMin      = Shader.PropertyToID("FluidPhaseMin");
        private static readonly int sIDFluidPhaseMax      = Shader.PropertyToID("FluidPhaseMax");
        private static readonly int sIDMatTriangles       = Shader.PropertyToID("_Triangles");
        private static readonly int sIDMatDensityVolume   = Shader.PropertyToID("_DensityVolume");
        private static readonly int sIDMatColor           = Shader.PropertyToID("_Color");
        private static readonly int sIDMatUsePerVertexColor = Shader.PropertyToID("_UsePerVertexColor");
        private static readonly int sIDMatVolumeOrigin    = Shader.PropertyToID("_VolumeOrigin");
        private static readonly int sIDMatVolumeSize      = Shader.PropertyToID("_VolumeSize");
        private static readonly int sIDMatVoxelSize       = Shader.PropertyToID("_VoxelSize");
        private static readonly int sIDMatResolution      = Shader.PropertyToID("_Resolution");
        private static readonly int sIDMatDensityScale    = Shader.PropertyToID("_DensityScale");
        private static readonly int sIDMatIsoLevel        = Shader.PropertyToID("_IsoLevel");
        private static readonly int sIDMatOpticalDepthScale = Shader.PropertyToID("_OpticalDepthScale");
        private static readonly int sIDMatSmoothness      = Shader.PropertyToID("_Smoothness");
        private static readonly int sIDMatMetallic        = Shader.PropertyToID("_Metallic");
        private static readonly int sIDMatRefraction      = Shader.PropertyToID("_RefractionStrength");
        private static readonly int sIDMatTransmission    = Shader.PropertyToID("_TransmissionStrength");
        private static readonly int sIDMatReflection      = Shader.PropertyToID("_SurfaceReflection");
        private static readonly int sIDMatLightPenetration = Shader.PropertyToID("_LightPenetration");
        private static readonly int sIDMatFresnelPower    = Shader.PropertyToID("_FresnelPower");
        private static readonly int sIDMatFresnelBoost    = Shader.PropertyToID("_FresnelBoost");
        private static readonly int sIDMatVolumeLighting  = Shader.PropertyToID("_VolumeLighting");
        private static readonly int sIDMatViewDistance    = Shader.PropertyToID("_ViewMarchDistance");
        private static readonly int sIDMatViewSteps       = Shader.PropertyToID("_ViewMarchSteps");
        private static readonly int sIDMatLightDistance   = Shader.PropertyToID("_LightMarchDistance");
        private static readonly int sIDMatLightSteps      = Shader.PropertyToID("_LightMarchSteps");
        private static readonly int sIDMatRayMarchJitter  = Shader.PropertyToID("_RayMarchJitter");
        private static readonly int sIDMatDensitySoftness = Shader.PropertyToID("_DensitySoftness");
        private static readonly int sIDMatViewAbsorption  = Shader.PropertyToID("_ViewAbsorption");
        private static readonly int sIDMatLightAbsorption = Shader.PropertyToID("_LightAbsorption");
        private static readonly int sIDMatScatterStrength = Shader.PropertyToID("_ScatteringStrength");
        private static readonly int sIDMatDepthAlphaBoost = Shader.PropertyToID("_DepthAlphaBoost");
        private static readonly int sIDMatDeepPhase       = Shader.PropertyToID("_DeepPhaseStrength");
        private static readonly int sIDMatScatterPhase    = Shader.PropertyToID("_ScatterPhaseStrength");
        private static readonly int sIDMatIndexOfRefraction = Shader.PropertyToID("_IndexOfRefraction");
        private static readonly int sIDMatRefractionSteps = Shader.PropertyToID("_RefractionSteps");
        private static readonly int sIDMatExitSearchDistance = Shader.PropertyToID("_ExitSearchDistance");
        private static readonly int sIDMatExitSearchSteps = Shader.PropertyToID("_ExitSearchSteps");

        // Each Tri = 3 verts * (3 pos + 3 normal + 3 color) floats = 27 floats.
        private const int TriStrideFloats = 27;
        private const int TriStrideBytes  = TriStrideFloats * sizeof(float);

        void OnEnable()
        {
            if (FluidMeshShader == null)
            {
                FluidMeshShader = Resources.Load<ComputeShader>("FluidMesh");
            }
            if (FluidMeshShader == null)
            {
                Debug.LogError("[FluidMeshRenderer] FluidMesh.compute not found in Resources. " +
                               "Place Assets/Shader/Resources/FluidMesh.compute or assign it manually.");
                enabled = false;
                return;
            }

            kClear = FluidMeshShader.FindKernel("ClearDensity");
            kSplat = FluidMeshShader.FindKernel("SplatParticles");
            kSmooth = FluidMeshShader.FindKernel("SmoothDensity");
            kReset = FluidMeshShader.FindKernel("ResetCounter");
            kMC    = FluidMeshShader.FindKernel("MarchingCubes");
            kBuild = FluidMeshShader.FindKernel("BuildArgs");

            // TriTable is uploaded once and reused.
            triTableBuffer = new ComputeBuffer(MarchingCubesTables.TriTable.Length, sizeof(int));
            triTableBuffer.SetData(MarchingCubesTables.TriTable);

            triCountBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
            argsBuffer     = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        void OnDisable()
        {
            ReleaseAll();
        }

        void OnDestroy()
        {
            ReleaseAll();
        }

        void OnValidate()
        {
            if (DriveFluidMaterial && FluidMaterial != null)
            {
                float opticalDepthScale = UseWaterSurfacePreset ? Mathf.Max(OpticalDepthScale, 8.0f) : OpticalDepthScale;
                ApplyLiveMaterialProperties(FluidMaterial, opticalDepthScale);
            }
        }

        private void ReleaseAll()
        {
            ReleaseBuffer(ref densityBuffer);
            ReleaseBuffer(ref colorAccumBuffer);
            ReleaseBuffer(ref dominantWeightBuffer);
            ReleaseBuffer(ref dominantColorBuffer);
            ReleaseBuffer(ref densityScratchBuffer);
            ReleaseBuffer(ref colorAccumScratchBuffer);
            ReleaseBuffer(ref triangleBuffer);
            ReleaseBuffer(ref triCountBuffer);
            ReleaseBuffer(ref argsBuffer);
            ReleaseBuffer(ref triTableBuffer);
            allocatedScratchRes = -1;
        }

        private static void ReleaseBuffer(ref ComputeBuffer b)
        {
            if (b != null) { b.Release(); b = null; }
        }

        private void EnsureResolution(int res)
        {
            if (allocatedRes == res && densityBuffer != null && triangleBuffer != null && colorAccumBuffer != null &&
                dominantWeightBuffer != null && dominantColorBuffer != null) return;

            ReleaseBuffer(ref densityBuffer);
            ReleaseBuffer(ref colorAccumBuffer);
            ReleaseBuffer(ref dominantWeightBuffer);
            ReleaseBuffer(ref dominantColorBuffer);
            ReleaseBuffer(ref densityScratchBuffer);
            ReleaseBuffer(ref colorAccumScratchBuffer);
            ReleaseBuffer(ref triangleBuffer);
            allocatedScratchRes = -1;

            int voxels = res * res * res;
            densityBuffer    = new ComputeBuffer(voxels, sizeof(uint));
            // 3 uints per voxel - one per RGB channel.
            colorAccumBuffer = new ComputeBuffer(voxels * 3, sizeof(uint));
            dominantWeightBuffer = new ComputeBuffer(voxels, sizeof(uint));
            dominantColorBuffer = new ComputeBuffer(voxels, sizeof(uint));

            // Worst case: 5 triangles per cube, but typical scenes hit far less.
            // Cap to avoid silly allocations at high resolution.
            int maxTris = Mathf.Min(voxels * 5, 4_000_000);
            triangleBuffer = new ComputeBuffer(maxTris, TriStrideBytes, ComputeBufferType.Append);
            triangleBuffer.SetCounterValue(0);

            allocatedRes = res;
        }

        private void EnsureScratchBuffers(int res)
        {
            if (allocatedScratchRes == res && densityScratchBuffer != null && colorAccumScratchBuffer != null) return;

            ReleaseBuffer(ref densityScratchBuffer);
            ReleaseBuffer(ref colorAccumScratchBuffer);

            int voxels = res * res * res;
            densityScratchBuffer = new ComputeBuffer(voxels, sizeof(uint));
            colorAccumScratchBuffer = new ComputeBuffer(voxels * 3, sizeof(uint));
            allocatedScratchRes = res;
        }

        void LateUpdate()
        {
            if (ParticleSystem == null) return;
            ComputeBuffer positions = ParticleSystem.FluidPositionsBuffer;
            ComputeBuffer phase     = ParticleSystem.FluidPhaseBuffer;
            ComputeBuffer colors    = ParticleSystem.FluidColorsBuffer;
            int numParticles        = ParticleSystem.FluidParticleCount;
            if (positions == null || phase == null || colors == null || numParticles <= 0) return;
            if (FluidMaterial == null) return;

            int res = Mathf.Clamp(Resolution, 8, 256);
            EnsureResolution(res);

            // Resolve volume from BoundsCube if assigned.
            Vector3 origin = VolumeOrigin;
            Vector3 size   = VolumeSize;
            if (BoundsCube != null)
            {
                var rend = BoundsCube.GetComponent<Renderer>();
                if (rend != null)
                {
                    var b = rend.bounds;
                    origin = b.min;
                    size   = b.size;
                }
                else
                {
                    var t = BoundsCube.transform;
                    size   = t.lossyScale;
                    origin = t.position - size * 0.5f;
                }
            }

            Vector3 voxelSize = new Vector3(size.x / res, size.y / res, size.z / res);

            float splatFalloffPower = UseWaterSurfacePreset ? 4.0f : SplatFalloffPower;
            float splatDensityBoost = UseWaterSurfacePreset ? Mathf.Max(SplatDensityBoost, 2.0f) : SplatDensityBoost;
            int smoothIterationsSetting = UseWaterSurfacePreset ? 1 : DensitySmoothIterations;
            int smoothRadiusSetting = UseWaterSurfacePreset ? 1 : DensitySmoothRadius;
            float smoothStrengthSetting = UseWaterSurfacePreset ? Mathf.Min(DensitySmoothStrength, 0.22f) : DensitySmoothStrength;
            float opticalDepthScale = UseWaterSurfacePreset ? Mathf.Max(OpticalDepthScale, 8.0f) : OpticalDepthScale;

            // Common uniforms
            FluidMeshShader.SetVector(sIDVolumeOrigin, origin);
            FluidMeshShader.SetVector(sIDVolumeSize,   size);
            FluidMeshShader.SetVector(sIDVoxelSize,    voxelSize);
            FluidMeshShader.SetInts  (sIDResolution,   res, res, res);
            FluidMeshShader.SetFloat (sIDSplatRadius,  SplatRadius);
            FluidMeshShader.SetFloat (sIDSplatFalloffPower, Mathf.Max(splatFalloffPower, 0.25f));
            FluidMeshShader.SetFloat (sIDSplatDensityBoost, Mathf.Max(splatDensityBoost, 0f));
            FluidMeshShader.SetFloat (sIDDensityScale, DensityScale);
            FluidMeshShader.SetFloat (sIDIsoLevel,     IsoLevel * DensityScale / DensityScale); // documented: in float units
            FluidMeshShader.SetInt   (sIDMaxSplatVoxels, Mathf.Clamp(MaxSplatVoxels, 1, 16));
            FluidMeshShader.SetInt   (sIDSmoothingRadius, Mathf.Clamp(smoothRadiusSetting, 0, 2));
            FluidMeshShader.SetFloat (sIDSmoothingStrength, Mathf.Clamp01(smoothStrengthSetting));
            FluidMeshShader.SetFloat (sIDPhaseBoundarySharpness, Mathf.Clamp01(PhaseBoundarySharpness));
            FluidMeshShader.SetInt   (sIDFluidNumParticles, numParticles);
            FluidMeshShader.SetInt   (sIDFluidPhaseMin, FluidPhaseMin);
            FluidMeshShader.SetInt   (sIDFluidPhaseMax, FluidPhaseMax);

            // 1. Clear
            FluidMeshShader.SetBuffer(kClear, sIDDensity,    densityBuffer);
            FluidMeshShader.SetBuffer(kClear, sIDColorAccum, colorAccumBuffer);
            FluidMeshShader.SetBuffer(kClear, sIDDominantWeight, dominantWeightBuffer);
            FluidMeshShader.SetBuffer(kClear, sIDDominantColor, dominantColorBuffer);
            int g3 = Mathf.CeilToInt(res / 8f);
            FluidMeshShader.Dispatch(kClear, g3, g3, g3);

            // 2. Splat
            FluidMeshShader.SetBuffer(kSplat, sIDDensity,    densityBuffer);
            FluidMeshShader.SetBuffer(kSplat, sIDColorAccum, colorAccumBuffer);
            FluidMeshShader.SetBuffer(kSplat, sIDDominantWeight, dominantWeightBuffer);
            FluidMeshShader.SetBuffer(kSplat, sIDDominantColor, dominantColorBuffer);
            FluidMeshShader.SetBuffer(kSplat, sIDPositions,  positions);
            FluidMeshShader.SetBuffer(kSplat, sIDPhase,      phase);
            FluidMeshShader.SetBuffer(kSplat, sIDColors,     colors);
            int g1 = Mathf.CeilToInt(numParticles / 256f);
            FluidMeshShader.Dispatch(kSplat, g1, 1, 1);

            ComputeBuffer activeDensity = densityBuffer;
            ComputeBuffer activeColor = colorAccumBuffer;
            int smoothIterations = Mathf.Clamp(smoothIterationsSetting, 0, 4);
            if (smoothIterations > 0 && smoothRadiusSetting > 0 && smoothStrengthSetting > 0f)
            {
                EnsureScratchBuffers(res);
                for (int i = 0; i < smoothIterations; i++)
                {
                    ComputeBuffer writeDensity = ReferenceEquals(activeDensity, densityBuffer) ? densityScratchBuffer : densityBuffer;
                    ComputeBuffer writeColor = ReferenceEquals(activeColor, colorAccumBuffer) ? colorAccumScratchBuffer : colorAccumBuffer;

                    FluidMeshShader.SetBuffer(kSmooth, sIDDensityRead, activeDensity);
                    FluidMeshShader.SetBuffer(kSmooth, sIDColorAccumRead, activeColor);
                    FluidMeshShader.SetBuffer(kSmooth, sIDDensityWrite, writeDensity);
                    FluidMeshShader.SetBuffer(kSmooth, sIDColorAccumWrite, writeColor);
                    FluidMeshShader.Dispatch(kSmooth, g3, g3, g3);

                    activeDensity = writeDensity;
                    activeColor = writeColor;
                }
            }

            // 3. Reset triangle append counter (CPU side).
            triangleBuffer.SetCounterValue(0);

            // 4. Reset args (sets instance count etc.).
            FluidMeshShader.SetBuffer(kReset, sIDDrawArgs, argsBuffer);
            FluidMeshShader.Dispatch(kReset, 1, 1, 1);

            // 5. Marching cubes.
            FluidMeshShader.SetBuffer(kMC, sIDDensity,    activeDensity);
            FluidMeshShader.SetBuffer(kMC, sIDColorAccum, activeColor);
            FluidMeshShader.SetBuffer(kMC, sIDDominantColor, dominantColorBuffer);
            FluidMeshShader.SetBuffer(kMC, sIDTriTable,   triTableBuffer);
            FluidMeshShader.SetBuffer(kMC, sIDTriangles,  triangleBuffer);
            FluidMeshShader.Dispatch(kMC, g3, g3, g3);

            // 6. Pull triangle count into a buffer the BuildArgs kernel can read.
            ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);

            // 7. Build indirect args.
            FluidMeshShader.SetBuffer(kBuild, sIDTriCount, triCountBuffer);
            FluidMeshShader.SetBuffer(kBuild, sIDDrawArgs, argsBuffer);
            FluidMeshShader.Dispatch(kBuild, 1, 1, 1);

            // 8. Bind triangle buffer to the material and queue the draw.
            FluidMaterial.SetBuffer(sIDMatTriangles, triangleBuffer);
            FluidMaterial.SetBuffer(sIDMatDensityVolume, activeDensity);
            FluidMaterial.SetVector(sIDMatVolumeOrigin, origin);
            FluidMaterial.SetVector(sIDMatVolumeSize, size);
            FluidMaterial.SetVector(sIDMatVoxelSize, voxelSize);
            FluidMaterial.SetVector(sIDMatResolution, new Vector4(res, res, res, 0));
            FluidMaterial.SetFloat(sIDMatDensityScale, DensityScale);
            FluidMaterial.SetFloat(sIDMatIsoLevel, IsoLevel);
            if (DriveFluidMaterial)
            {
                ApplyLiveMaterialProperties(FluidMaterial, opticalDepthScale);
            }

            Bounds drawBounds = new Bounds(origin + size * 0.5f, size * 1.5f);
            Graphics.DrawProceduralIndirect(
                material:        FluidMaterial,
                bounds:          drawBounds,
                topology:        MeshTopology.Triangles,
                bufferWithArgs:  argsBuffer,
                argsOffset:      0,
                camera:          null,
                properties:      null,
                castShadows:     ShadowCasting,
                receiveShadows:  ReceiveShadows,
                layer:           gameObject.layer);
        }

        private void ApplyLiveMaterialProperties(Material material, float opticalDepthScale)
        {
            if (material.HasProperty(sIDMatColor))
            {
                material.SetColor(sIDMatColor, FluidTint);
            }

            material.SetFloat(sIDMatSmoothness, Smoothness);
            material.SetFloat(sIDMatMetallic, Metallic);
            material.SetFloat(sIDMatRefraction, RefractionStrength);
            material.SetFloat(sIDMatTransmission, TransmissionStrength);
            material.SetFloat(sIDMatReflection, SurfaceReflection);
            material.SetFloat(sIDMatLightPenetration, LightPenetration);
            material.SetFloat(sIDMatUsePerVertexColor, UsePerVertexColor);

            material.SetFloat(sIDMatFresnelPower, FresnelPower);
            material.SetFloat(sIDMatFresnelBoost, FresnelBoost);
            material.SetFloat(sIDMatVolumeLighting, VolumeLighting);
            material.SetFloat(sIDMatViewDistance, ViewMarchDistance);
            material.SetFloat(sIDMatViewSteps, Mathf.Clamp(ViewMarchSteps, 1, 48));
            material.SetFloat(sIDMatLightDistance, LightMarchDistance);
            material.SetFloat(sIDMatLightSteps, Mathf.Clamp(LightMarchSteps, 1, 32));
            material.SetFloat(sIDMatRayMarchJitter, RayMarchJitter);
            material.SetFloat(sIDMatDensitySoftness, DensitySoftness);
            material.SetFloat(sIDMatOpticalDepthScale, opticalDepthScale);
            material.SetFloat(sIDMatViewAbsorption, ViewAbsorption);
            material.SetFloat(sIDMatLightAbsorption, LightAbsorption);
            material.SetFloat(sIDMatScatterStrength, ScatteringStrength);
            material.SetFloat(sIDMatDepthAlphaBoost, DepthAlphaBoost);
            material.SetFloat(sIDMatDeepPhase, DeepPhaseStrength);
            material.SetFloat(sIDMatScatterPhase, ScatterPhaseStrength);
            material.SetFloat(sIDMatIndexOfRefraction, Mathf.Max(IndexOfRefraction, 1.0001f));
            material.SetFloat(sIDMatRefractionSteps, Mathf.Clamp(RefractionSteps, 1, 4));
            material.SetFloat(sIDMatExitSearchDistance, Mathf.Max(ExitSearchDistance, 0.01f));
            material.SetFloat(sIDMatExitSearchSteps, Mathf.Clamp(ExitSearchSteps, 4, 64));
        }
    }
}
