using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Assets.Scripts.GPU.Fluids;
using GPU;
using GPU.Fluids;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class GPUParticleSystem : MonoBehaviour
{

    static bool meshChanged = true;

    ComputeBuffer argsBuffer;

    private uint[] phaseArray;

    private const int MaxParticlesPerCell = 4;

    private MaterialPropertyBlock props;

    private int kernel = 0;
    private int collisionsKernel = 0;
    private int gridKernel = 0;
    private int csSolveConstraintsKernel = 0;
    private int nodeUpdateComputeShaderHandle = 0;

    [HideInInspector] public uint[] Args = new uint[5] { 0, 0, 0, 0, 0 };

    //GPU Info
    private const int PPerWarp = 32;
    private int mWarpCount;
    private int mWarpCountY;
    private Vector4[] positions;
    private Vector4[] velocities;

    [SerializeField] Material mat;
    [SerializeField] Material BoundsParticleMat;
    [SerializeField] Mesh mesh;

    public GameObject ParticleGo;
    public GameObject BoundaryGo;

    public Mesh m
    {
        get { return mesh; }
        set
        {
            mesh = value;
            meshChanged = true;
        }
    }

    [SerializeField] float Gravity;
    [SerializeField] Vector3 Boundaries;
    [SerializeField] int NumParticles = 1000;

    [SerializeField] float particleRadius = 1.0f;

    private float ParticleDiameter
    {
        get { return particleRadius * 2.0f; }
    }

    [SerializeField] float particlesMass = 1.0f;

    [SerializeField] float collideSpring = 0.5f;
    [SerializeField] float collideDamping = 0.01f;
    [SerializeField] float collideShear = 0.01f;
    [SerializeField] float collideAttraction = 0.01f;
    [SerializeField] float globalDamping = 0.01f;
    [SerializeField] float boundaryDamping = 0.8f;

    Vector2 invClothSize;
    Vector2 step;
    private int width, height;

    [SerializeField] Color minColor = Color.blue;
    [SerializeField] Color maxColor = Color.white;

    [SerializeField] float unityTimeStep;
    [SerializeField] float customTimeStep = 0.01f;
    [SerializeField] bool synchronizeTime = false;

    [SerializeField]
    public float GroundLevel = -1.583f;

    [Tooltip("Cube GameObject whose world-space AABB is used as the hard simulation boundary. " +
             "Particles are clamped inside this volume each frame.")]
    public GameObject BoundaryCube;

    [Tooltip("Optional cube/empty Transforms whose world-space AABB carves a hole " +
             "out of the boundary particle shell. Use to create openings (drains, " +
             "spouts) on any face of the pool. The hole is purely visual/collision: " +
             "BoundaryCube still constrains particles unless removed too.")]
    public Transform[] BoundaryHoles;

    [Header("Respawn")]
    [Tooltip("If set, fluid particles are teleported back to this transform's " +
             "position when their per-particle timer hits RespawnInterval. " +
             "Velocities are zeroed.")]
    public Transform RespawnTarget;

    [Tooltip("Per-particle respawn interval in seconds. Each fluid particle " +
             "ticks its own timer (initialised to a random value in [0,interval) " +
             "so they respawn in a continuous stream rather than all at once). " +
             "Set <= 0 to disable.")]
    public float RespawnInterval = 0f;

    [Header("Rendering")]
    [Tooltip("Draw the fluid/cloth particles as instanced spheres. Disable when " +
             "rendering the fluid as a mesh via FluidMeshRenderer.")]
    public bool RenderFluidParticles = true;
    [Tooltip("Draw the static boundary particles as instanced spheres.")]
    public bool RenderBoundaryParticles = true;

    // GPU buffers driving the per-particle respawn (sized to bodyController.NumParticles).
    private ComputeBuffer agesBuffer;
    private ComputeBuffer initialPositionsBuffer;
    // CPU snapshot of initial fluid positions + centroid (used to compute the
    // offset pushed to the shader each frame, and for the manual context-menu
    // "reset all now" path).
    private Vector4[] initialFluidPositions;
    private int fluidParticleCount;
    private Vector3 initialFluidCentroid;

    private readonly Vector3 mWorldSize = new Vector3(2, 2, 2);
    private readonly Vector3 mGridSize = new Vector3(64, 64, 64);
    private Vector3 mWorldOrigin;
    private int totalCells;
    private float mCellSize;

    [SerializeField] private Transform sphere;
    private Vector4 rigidBodyPosition;
    private Vector4 rigidBodyVelocity;
    private float radius;

    private const int texWidth = 512, texHeight = 512;


    private void Start()
    {
        camera1 = Camera.main;

        StartFluid();
    }

    private void Update()
    {
        unityTimeStep = Time.deltaTime;
        if (synchronizeTime)
            customTimeStep = Time.deltaTime;

        //var transform1 = sphere.transform;
        //rigidBodyPosition = transform1.position;
        //rigidBodyVelocity = rigidbody2.velocity;
        //radius = transform1.localScale.x / 2;

        Integrate();

        //rigidbody1.velocity = rigidBodyVelocity;
    }


    private void OnDestroy()
    {
        argsBuffer?.Dispose();

        if (boundaryColorsBuffer != null) { boundaryColorsBuffer.Release(); boundaryColorsBuffer = null; }
        if (agesBuffer != null) { agesBuffer.Release(); agesBuffer = null; }
        if (initialPositionsBuffer != null) { initialPositionsBuffer.Release(); initialPositionsBuffer = null; }

        fusion_FluidBoundary?.Dispose();
        foreach (var item in fusion_FluidBody)
        {
            item?.Dispose();
        }
        fusion_FluidSolver?.Dispose();

        clothSolver?.Dispose();

        bodyController?.Dispose();
    }

    void Integrate()
    {

        //Reset Grid to be Updated
        //gridCells.SetData(new uint[totalCells * MaxParticlesPerCell]);
        //gridCounters.SetData(new uint[totalCells]);

        SetComputeProperties();

        DispatchBuffers();

        //mat.SetBuffer("positions", oldPositionBuffer);
        //mat.SetFloat("Diameter", ParticleDiameter);

        //var castShadow = ShadowCastingMode.On;
        //const bool receiveShadow = true;

        //Graphics.DrawMeshInstancedIndirect(
        //    mesh, 0, mat,
        //    new Bounds(this.transform.position, new Vector3(0,0, 0)), 
        //    argsBuffer, 0, props, castShadow, receiveShadow);
    }

    private void DispatchBuffers()
    {

        UpdateFluid();

        //Set the debug materials textures so they are rendered in the screen
        //positionsDebugMaterial.SetTexture(MainTex, oldPositionTexture);
        //velocitiesDebugMaterial.SetTexture(MainTex, oldVelocityTexture);

        /*
        computeShader.SetBuffer(kernel, "oldPositionBuffer", oldPositionBuffer);
        computeShader.SetBuffer(kernel, "oldVelocityBuffer", oldVelocityBuffer);
        computeShader.SetBuffer(kernel, "newPositionBuffer", newPositionBuffer);
        computeShader.SetBuffer(kernel, "newVelocityBuffer", newVelocityBuffer);

        computeShader.Dispatch(kernel, mWarpCount, mWarpCountY, 1);

        STDUtils.Swap(ref oldPositionBuffer, ref newPositionBuffer);
        STDUtils.Swap(ref oldVelocityBuffer, ref newVelocityBuffer);


        fluidSolver.StepPhysics(customTimeStep, Gravity, gridCounters, gridCells, computeShader, gridKernel,
            oldPositionBuffer, mWarpCount);
        */

        /*
        //Update cloth
        clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "phaseBuffer", phaseBuffer);
        clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "clothData", Objects);
        
        
        clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "newPositionBuffer", newPositionBuffer);
        clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "oldPositionBuffer", oldPositionBuffer);
        clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "newVelocityBuffer", newVelocityBuffer);
        clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "oldVelocityBuffer", oldVelocityBuffer);
        
#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlaying) return;
#endif
        for (var i = 0; i < looper; i++)
        {
            clothUpdateShader.Dispatch(nodeUpdateComputeShaderHandle, NUCSdispatchDimX, NUCSdispatchDimY,
                NUCSdispatchDimZ);
        }
        STDUtils.Swap(ref oldVelocityBuffer, ref newVelocityBuffer);
        STDUtils.Swap(ref oldPositionBuffer, ref newPositionBuffer);
        */


        /*
        if (useTexture) {
            computeShader.SetTexture(gridKernel, "oldPositionTexture", oldPositionTexture);
        }
        else {
            computeShader.SetBuffer(gridKernel, "oldPositionBuffer", oldPositionBuffer);
        }
        computeShader.SetBuffer(gridKernel, "gridCounters", gridCounters);
        computeShader.SetBuffer(gridKernel, "gridCells", gridCells);

        computeShader.Dispatch(gridKernel, mWarpCount, 1, 1);

        if (!ShowGrid) 
            return;
        
        degubGridCounters = new uint[totalCells];
        gridCounters.GetData(degubGridCounters);
        */

        /*
        if (useTexture) {
            computeShader.SetTexture(collisionsKernel, "oldPositionTexture", oldPositionTexture);
            computeShader.SetTexture(collisionsKernel, "oldVelocityTexture", oldVelocityTexture);
            computeShader.SetTexture(collisionsKernel, "newPositionTexture", newPositionTexture);
            computeShader.SetTexture(collisionsKernel, "newVelocityTexture", newVelocityTexture);
        }
        else {
            computeShader.SetBuffer(collisionsKernel, "oldPositionBuffer", oldPositionBuffer);
            computeShader.SetBuffer(collisionsKernel, "oldVelocityBuffer", oldVelocityBuffer);
            computeShader.SetBuffer(collisionsKernel, "newPositionBuffer", newPositionBuffer);
            computeShader.SetBuffer(collisionsKernel, "newVelocityBuffer", newVelocityBuffer);
        }
        computeShader.SetBuffer(collisionsKernel, "gridCounters", gridCounters);
        computeShader.SetBuffer(collisionsKernel, "gridCells", gridCells);
        
        computeShader.Dispatch(collisionsKernel, mWarpCount, mWarpCountY, 1);
        if (useTexture){
            //STDUtils.SwapTextures(ref oldVelocityTexture, ref newVelocityTexture);
            var vPair = STDUtils.SwapTextures(oldVelocityTexture, newVelocityTexture);
            oldVelocityTexture = vPair.Key as Texture2D;
            newVelocityTexture = vPair.Value as RenderTexture;
        }    
        else STDUtils.Swap(ref oldVelocityBuffer, ref newVelocityBuffer);    
        */
    }

    void SetComputeProperties()
    {
        //computeShader.SetFloat("deltaTime", customTimeStep);

        //computeShader.SetInt("NumParticles", NumParticles);
        //computeShader.SetFloat("particleRadius", particleRadius);
    }

    //the fluid body object
    private FluidBody[] fusion_FluidBody = new FluidBody[10];
    private ClothBody[] clothBody = new ClothBody[10];

    //the fluid boundary object
    private FluidBoundary fusion_FluidBoundary;

    //the PBD fluid solver object
    private FluidSolverN fusion_FluidSolver;
    private ClothSolver clothSolver;

    //the boundaries of the container
    //Bounds fusion_FluidBodySource, fusion_outerSource, fusion_innerSource;
    //bool used for checking system errors
    private bool wasError;

    //resize the cube by pressing key
    private Vector3 containerScale;
    private Vector3[] containerClothScale = new Vector3[10];

    //the container's transformation
    public Transform[] containerTransform = new Transform[10];
    public Transform[] containerClothTransform = new Transform[10];

    //vector3 for the position of the container's center
    private Vector3 containerPos;
    private Vector3[] containerClothPos = new Vector3[10];

    //the two public vector3 for the ratio of fluid body(deprecated solution)
    //public Vector3 FluidBodyRatioStart;
    //public Vector3 FluidBodyRatioEnd;
    //the transform that defines the original size of the fluid chuck
    public Transform[] FluidChunkTransform = new Transform[10];
    public Renderer FluidChunkRenderer;

    //the initial velocity of the fluid particles
    public Vector3 FluidInitialVelocity;

    //the number of iterations
    public int DensityComputeIterations = 2;

    public int ConstraintComputeIterations = 2;

    [Tooltip("How strongly particles from other fluid phases behave like a moving boundary. " +
             "Raise this to keep different-density fluids separated; lower it if contact jitters.")]
    [Range(0f, 3f)] public float InterphaseBoundaryStrength = 1.0f;

    [Header("PBF Stability")]
    [Tooltip("Lower values make density constraints stiffer. If particles overlap or collapse, lower this; if the fluid jitters, raise it.")]
    [Range(1f, 150f)] public float PressureRelaxation = 35.0f;
    [Tooltip("Minimum constraint iterations used while the water preset is enabled.")]
    [Range(1, 8)] public int WaterConstraintIterations = 5;
    [Tooltip("Desired same-phase spacing, as a multiplier of the visual particle diameter.")]
    [Range(0.5f, 2.0f)] public float ParticleRestDistanceMultiplier = 1.15f;
    [Tooltip("Extra short-range separation applied when same-phase particles overlap.")]
    [Range(0f, 1f)] public float ParticleContactStiffness = 0.35f;
    [Tooltip("Gentle same-phase attraction that helps sheets and surfaces close without adding much viscosity.")]
    [Range(0f, 0.1f)] public float ParticleCohesion = 0.025f;
    [Tooltip("Cohesion reach, as a multiplier of ParticleRestDistance.")]
    [Range(1f, 4f)] public float ParticleCohesionRadiusMultiplier = 2.2f;

    [Header("Water Physics")]
    [Tooltip("Override per-chunk damping/viscosity with low values suited for water-like motion.")]
    public bool UseWaterPhysicsPreset = true;
    [Range(0f, 0.002f)] public float WaterViscosityCoeff = 0.00005f;
    [Range(0f, 0.02f)] public float WaterDampingCoeff = 0.001f;

    //the thickness of fluid boundary
    public float BoundaryThickness = 1.0f;

    public ComputeShader bitonicSort;
    public ComputeShader gridHash;
    public ComputeShader createBoundaryShader;
    public ComputeShader clothUpdateShader;
    public ComputeShader fluidSolver;

    private Camera camera1;
    private BodyController bodyController;

    // Exposed for external renderers (e.g. FluidMeshRenderer) that need to
    // read the live particle pool. Returns null until StartFluid has run.
    public ComputeBuffer FluidPositionsBuffer => bodyController?.PositionsBuffer;
    public ComputeBuffer FluidPhaseBuffer    => bodyController?.phaseBuffer;
    public ComputeBuffer FluidColorsBuffer   => bodyController?.ParticleColors;
    public int           FluidParticleCount  => fluidParticleCount;

    void InitializerCloth(int numBodies)
    {
        if (numBodies > 10) return;
        for (int i = 0; i < numBodies; i++)
        {
            containerClothPos[i] = containerClothTransform[i].position;
            containerClothScale[i] = containerClothTransform[i].localScale;

            var sourceCloth = CreateParticlesFromBounds(particleRadius, 3500f,
                containerClothPos[i], containerClothScale[i], containerClothPos[i], containerClothScale[i]);
            clothBody[i] = new ClothBody(
                bodyController,
                sourceCloth,
                3500f,
                Matrix4x4.identity,
                new Vector3(0, 0, 0)
            )
            {
                springK = 5000f,
                damping = 0.2f,
                bounds = sourceCloth.Bounds,
                id = (byte)i
            };

            bodyController.AddBody(clothBody[i], new Vector4(1, 1, 1, 1));
        }
    }

    public void StartFluid()
    {
        //define the particle radius here in order to create fluid body particles
        particleRadius = 0.03f;
        const float density = 2000.0f;

        //Generate boundries
        containerPos = containerTransform[0].position;
        containerScale = containerTransform[0].localScale;

        //General Boundary
        fusion_FluidBoundary = CreateBoundary(particleRadius, 4000, containerPos, containerScale);

        bodyController = new BodyController(containerTransform[0], particleRadius, particlesMass);

        byte counter = 0;
        foreach (var chunk in FluidChunkTransform)
        {
            if (chunk == null)
                continue;

            var fluidBodyPos = chunk.position;
            var fluidBodyScale = chunk.localScale;
            var bd = chunk.GetComponent<FluidBodyMb>();
            //Fluid
            var source = CreateParticlesFromBounds(particleRadius * 1.40f, bd.density, containerPos, containerScale, fluidBodyPos, fluidBodyScale);
            fusion_FluidBody[counter] = new FluidBody(bodyController, source, bd.density, Matrix4x4.identity, FluidInitialVelocity)
            {
                bounds = fusion_FluidBoundary.Bounds,
                id = (byte)(counter + 3),
                ViscosityCoeff = UseWaterPhysicsPreset ? WaterViscosityCoeff : bd.ViscosityCoeff,
                DampingCoeff = UseWaterPhysicsPreset ? WaterDampingCoeff : bd.DampingCoeff
            };

            var color = new Vector4(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1);
            bodyController.AddBody(fusion_FluidBody[counter], color);

            counter++;
        }

        //Create and initialize cloth
        InitializerCloth(containerClothTransform.Count(x => x != null));

        fusion_FluidSolver = new FluidSolverN(bodyController, fusion_FluidBody, fusion_FluidBoundary,
            DensityComputeIterations, ConstraintComputeIterations,
            fluidSolver, gridHash, bitonicSort, bodyController.phaseBuffer);


        clothSolver = new ClothSolver(bodyController, gridHash,
            bitonicSort, fusion_FluidBoundary, clothUpdateShader,
            bodyController.phaseBuffer, clothBody);



        bodyController.InitializeBuffers();

        for (var b = 0; b < bodyController.cpuSideClothInfo.Count; b++)
        {
            var size = (int)clothBody[b].width * (int)clothBody[b].height;
            clothSolver.SetupPressuresAndDensities(size, b);
        }

        // Snapshot initial fluid positions for respawn. Fluid bodies are added
        // before cloth in StartFluid, so their particles occupy the contiguous
        // range [0, fluidParticleCount) inside BodyController's buffers.
        fluidParticleCount = 0;
        for (int i = 0; i < fusion_FluidBody.Length; i++)
        {
            if (fusion_FluidBody[i] == null) continue;
            fluidParticleCount += fusion_FluidBody[i].source.NumParticles;
        }
        if (fluidParticleCount > 0)
        {
            initialFluidPositions = new Vector4[fluidParticleCount];
            var sum = Vector3.zero;
            for (int i = 0; i < fluidParticleCount; i++)
            {
                var p = bodyController.Positions[i];
                initialFluidPositions[i] = p;
                sum += new Vector3(p.x, p.y, p.z);
            }
            initialFluidCentroid = sum / fluidParticleCount;
        }

        // Per-particle respawn buffers. Sized to the full particle pool so
        // that shader-side indexing matches NumParticles. Cloth slots are
        // unused (the shader's phase guard skips them).
        var totalCount = bodyController.NumParticles;
        if (totalCount > 0)
        {
            agesBuffer = new ComputeBuffer(totalCount, sizeof(float));
            initialPositionsBuffer = new ComputeBuffer(totalCount, 4 * sizeof(float));

            var ages = new float[totalCount];
            var inits = new Vector4[totalCount];
            // Stagger: random initial ages in [0, interval). If interval is 0,
            // ages stay at 0 (shader skips the reset block via its > 0 guard).
            var stagger = RespawnInterval > 0f ? RespawnInterval : 0f;
            for (int i = 0; i < totalCount; i++)
            {
                inits[i] = bodyController.Positions[i];
                ages[i] = stagger > 0f ? Random.Range(0f, stagger) : 0f;
            }
            agesBuffer.SetData(ages);
            initialPositionsBuffer.SetData(inits);

            fusion_FluidSolver.Ages = agesBuffer;
            fusion_FluidSolver.InitialPositions = initialPositionsBuffer;
        }
    }

    [ContextMenu("Respawn All Fluid Now")]
    public void RespawnFluidParticles()
    {
        // Manual "force all particles to respawn this frame" path: set every
        // age past the interval. Useful for testing.
        if (agesBuffer == null || RespawnInterval <= 0f) return;
        var ages = new float[agesBuffer.count];
        for (int i = 0; i < ages.Length; i++) ages[i] = RespawnInterval + 1f;
        agesBuffer.SetData(ages);
    }

    Vector4[] data;
    public GameObject[] boundaryGos;

    // Per-instance color buffer for the boundary particles. The instanced shader
    // (Instanced/InstancedSurfaceShader) reads `particleColors[unity_InstanceID]`
    // in setup(), so we must bind a buffer of length == boundary particle count.
    // Allocated lazily on first UpdateFluid() call and released in OnDestroy.
    private ComputeBuffer boundaryColorsBuffer;

    private void EnsureBoundaryRenderState()
    {
        // Force the procedural-instancing shader. The material as authored in
        // the inspector typically points at Standard, which silently ignores the
        // procedural setup() and renders nothing useful.
        var instancedShader = Shader.Find("Instanced/InstancedSurfaceShader");
        if (instancedShader != null && BoundsParticleMat.shader != instancedShader)
        {
            BoundsParticleMat.shader = instancedShader;
        }

        if (boundaryColorsBuffer == null && fusion_FluidBoundary != null && fusion_FluidBoundary.NumParticles > 0)
        {
            boundaryColorsBuffer = new ComputeBuffer(fusion_FluidBoundary.NumParticles, 4 * sizeof(float));
            var colors = new Vector4[fusion_FluidBoundary.NumParticles];
            var c = new Vector4(0.1f, 0.1f, 0.1f, 0.35f);
            for (int i = 0; i < colors.Length; i++) colors[i] = c;
            boundaryColorsBuffer.SetData(colors);
        }
    }

    private void UpdateFluid()
    {
        fluidSolver.SetFloat("_GroundLevel", GroundLevel);

        // Push hard simulation boundary from the BoundaryCube's world AABB.
        // Set on the shader directly so PredictPositions can clamp on all 6 faces.
        // Tell the solver not to overwrite these with its FluidBoundary AABB.
        if (BoundaryCube != null)
        {
            Vector3 min, max;
            var rend = BoundaryCube.GetComponent<Renderer>();
            if (rend != null)
            {
                min = rend.bounds.min;
                max = rend.bounds.max;
            }
            else
            {
                var t = BoundaryCube.transform;
                var half = t.lossyScale * 0.5f;
                min = t.position - half;
                max = t.position + half;
            }
            fluidSolver.SetVector("BoundMin", min);
            fluidSolver.SetVector("BoundMax", max);
            fusion_FluidSolver.ExternalBoundsOverride = true;
        }
        else
        {
            fusion_FluidSolver.ExternalBoundsOverride = false;
        }

        // Per-particle respawn parameters. The buffers themselves are bound
        // inside FluidSolverN.PredictPositions; we just push the live offset
        // and interval each frame so RespawnTarget can move at runtime.
        if (fluidParticleCount > 0)
        {
            var target = RespawnTarget != null ? RespawnTarget.position : initialFluidCentroid;
            fusion_FluidSolver.RespawnInterval = RespawnInterval;
            fusion_FluidSolver.RespawnOffset = target - initialFluidCentroid;
        }

        fusion_FluidSolver.InterphaseBoundaryStrength = InterphaseBoundaryStrength;
        fusion_FluidSolver.DensityComputeIterations = DensityComputeIterations;
        fusion_FluidSolver.ConstraintComputeIterations = UseWaterPhysicsPreset
            ? Mathf.Max(ConstraintComputeIterations, WaterConstraintIterations)
            : ConstraintComputeIterations;
        fusion_FluidSolver.PressureRelaxation = PressureRelaxation;
        fusion_FluidSolver.ParticleRestDistanceMultiplier = ParticleRestDistanceMultiplier;
        fusion_FluidSolver.ParticleContactStiffness = ParticleContactStiffness;
        fusion_FluidSolver.ParticleCohesion = ParticleCohesion;
        fusion_FluidSolver.ParticleCohesionRadiusMultiplier = ParticleCohesionRadiusMultiplier;

        fusion_FluidSolver.StepPhysics(customTimeStep);
        clothSolver.StepPhysics(customTimeStep);


        //draw particles using GPU instancing
        //the draw function is defined in fluid body class
        if (RenderFluidParticles)
        {
            bodyController.Draw(mesh, mat, props, camera1);
        }


        //Draw bounds
        // (DrawMeshInstancedProcedural takes the instance count directly; no args
        // buffer is required, so we don't allocate one here. Allocating a fresh
        // ComputeBuffer per frame without disposing it leaks native handles.)
        if (RenderBoundaryParticles)
        {
            EnsureBoundaryRenderState();
            BoundsParticleMat.SetBuffer(Shader.PropertyToID("positions"), fusion_FluidBoundary.PositionsBuffer);
            BoundsParticleMat.SetBuffer(Shader.PropertyToID("particleColors"), boundaryColorsBuffer);
            BoundsParticleMat.SetFloat(Shader.PropertyToID("Diameter"), ParticleDiameter);
            BoundsParticleMat.SetInt(Shader.PropertyToID("numPart"), fusion_FluidBoundary.NumParticles);

            const ShadowCastingMode castShadow = ShadowCastingMode.Off;
            const bool receiveShadow = false;

            Graphics.DrawMeshInstancedProcedural(
                mesh, 0, BoundsParticleMat,
                new Bounds(Vector3.zero, new Vector3(10, 10, 10)),
                fusion_FluidBoundary.NumParticles, props, castShadow, receiveShadow);
        }
        //Draw bounds
    }

    //given a cube region, generate boundary particles around it
    private FluidBoundary CreateBoundary(float radius, float density, Vector3 containerPos, Vector3 resizeFactor)
    {
        //innerBounds defines the region that fluid particles could move 
        var innerBounds = new Bounds();
        //create the fluid boundary according to the position and size of the container 
        //the information about the container is passed in via the Transformation
        var min = new Vector3(containerPos[0] - 0.5f * resizeFactor[0], containerPos[1] - 0.5f * resizeFactor[1],
            containerPos[2] - 0.5f * resizeFactor[2]);
        var max = new Vector3(containerPos[0] + 0.5f * resizeFactor[0], containerPos[1] + 0.5f * resizeFactor[1],
            containerPos[2] + 0.5f * resizeFactor[2]);
        innerBounds.SetMinMax(min, max);

        //Make the boundary 1 particle thick.
        //The multiple by 1.2 adds a little of extra
        //thickness in case the radius does not evenly
        //divide into the bounds size. You might have
        //particles missing from one side of the source
        //bounds other wise.
        float BoundaryThickness = 2;
        var diameter = radius * 2;
        min.x -= diameter * BoundaryThickness * 1.2f;
        min.y -= diameter * BoundaryThickness * 1.2f;
        min.z -= diameter * BoundaryThickness * 1.2f;

        max.x += diameter * BoundaryThickness * 1.2f;
        max.y += diameter * BoundaryThickness * 1.2f;
        max.z += diameter * BoundaryThickness * 1.2f;
        //outerBounds is the outmost bound of all particles
        //A.K.A the boundary of the entire simulation
        var outerBounds = new Bounds();
        outerBounds.SetMinMax(min, max);

        //The source will create a array of particles
        //evenly spaced between the inner and outer bounds.
        ParticleSource source = new ParticlesFromBounds(diameter, outerBounds, innerBounds, true);

        // Carve holes out of the boundary shell using the world-space AABBs of
        // BoundaryHoles transforms. Done here (after the source has generated
        // its grid) so we don't have to extend the source's constructor API.
        if (BoundaryHoles != null && BoundaryHoles.Length > 0)
        {
            var holeAabbs = new List<Bounds>(BoundaryHoles.Length);
            foreach (var h in BoundaryHoles)
            {
                if (h == null) continue;
                var hr = h.GetComponent<Renderer>();
                if (hr != null)
                {
                    holeAabbs.Add(hr.bounds);
                }
                else
                {
                    holeAabbs.Add(new Bounds(h.position, h.lossyScale));
                }
            }
            if (holeAabbs.Count > 0)
            {
                // RemoveAll mutates the list in place — needed because
                // ParticleSource.Positions has a protected setter.
                source.Positions.RemoveAll(p =>
                {
                    for (int b = 0; b < holeAabbs.Count; b++)
                    {
                        if (holeAabbs[b].Contains(p)) return true;
                    }
                    return false;
                });
            }
        }

        //print out the number of particles
        Debug.Log("Boundary Particles = " + source.NumParticles);

        data = new Vector4[source.NumParticles];
        boundaryGos = new GameObject[source.NumParticles];
        for (int i = 0; i < source.NumParticles; i++)
        {
            var p = data[i];
            //boundaryGos[i] = GameObject.Instantiate(BoundaryGo, new Vector3(p.x, p.y, p.z), Quaternion.identity);
        }

        //given the particle positions contained in "source" object
        //create the fluid boundary object
        var fb = new FluidBoundary(source, radius, density, Matrix4x4.identity, gridHash, bitonicSort, createBoundaryShader);

        fb.PositionsBuffer.GetData(data);
        for (int i = 0; i < data.Length; i++)
        {
            var p = data[i];
            //boundaryGos[i] = GameObject.Instantiate(BoundaryGo, new Vector3(p.x, p.y, p.z), Quaternion.identity);
        }

        return fb;
        //pass bounds objects
        //fusion_innerSource = innerBounds;
        //fusion_outerSource = outerBounds;
    }

    //given a cube region, create a fluid body 
    //the fluid body's size is defined relative to the size of the container
    private ParticlesFromBounds CreateParticlesFromBounds(float radius, float density, Vector3 containerPos, Vector3 resizeFactor,
        Vector3 fluidBodyPos, Vector3 fluidBodyScale)
    {
        //the bounds of the (initial) fluid region
        var bounds = new Bounds();
        //Vector3 min = new Vector3(-8, 0, -1);
        //Vector3 max = new Vector3(0, 8, 1);

        var fluidChunkBound = new Bounds(fluidBodyPos, fluidBodyScale);
        // Vector3 min = new Vector3(fluidBodyPos[0] - 0.5f * fluidBodyScale[0], fluidBodyPos[1] - 0.5f * fluidBodyScale[1], fluidBodyPos[2] - 0.5f * fluidBodyScale[2]);
        // Vector3 max = new Vector3(fluidBodyPos[0] + 0.5f * fluidBodyScale[0], fluidBodyPos[1] + 0.5f * fluidBodyScale[1], fluidBodyPos[2] + 0.5f * fluidBodyScale[2]);
        var min = fluidChunkBound.min;
        var max = fluidChunkBound.max;

        //create the fluid body according to the size and position of the container
        //Vector3 ContainerMin = new Vector3(containerPos[0] - 0.5f * resizeFactor[0], containerPos[1] - 0.4f * resizeFactor[1], containerPos[2] - 0.25f * resizeFactor[2]);
        //Vector3 ContainerMax = new Vector3(containerPos[0] + 0.02f * resizeFactor[0], containerPos[1] + 0.4f * resizeFactor[1], containerPos[2] + 0.25f * resizeFactor[2]);
        //need to minus/plus a radius since the particles are defined as spheres
        min.x += radius;
        min.y += radius;
        min.z += radius;

        max.x -= radius;
        max.y -= radius;
        max.z -= radius;
        //set the bound
        bounds.SetMinMax(min, max);

        //The source will create a array of particles evenly spaced inside the bounds. 
        //Multiple the spacing by 0.9 to pack more particles into bounds.
        //create particles from the bound
        var diameter = radius * 2;
        var source = new ParticlesFromBounds(diameter * 0.9f, bounds, false);
        Debug.Log(" Particles = " + source.NumParticles);

        return source;
    }
}
