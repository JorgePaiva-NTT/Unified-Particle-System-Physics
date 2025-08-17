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

    private float groundLevel = 0.0f;

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

    //the thickness of fluid boundary
    public float BoundaryThickness = 1.0f;

    public ComputeShader bitonicSort;
    public ComputeShader gridHash;
    public ComputeShader createBoundaryShader;
    public ComputeShader clothUpdateShader;
    public ComputeShader fluidSolver;

    private Camera camera1;
    private BodyController bodyController;

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
                ViscosityCoeff = bd.ViscosityCoeff,
                DampingCoeff = bd.DampingCoeff
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
    }

    Vector4[] data;
    public GameObject[] boundaryGos;

    private void UpdateFluid()
    {
        fluidSolver.SetFloat("_GroundLevel", groundLevel);
        fusion_FluidSolver.StepPhysics(customTimeStep);
        clothSolver.StepPhysics(customTimeStep);


        //draw particles using GPU instancing
        //the draw function is defined in fluid body class
        bodyController.Draw(mesh, mat, props, camera1);


        //Draw bounds
        var args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)fusion_FluidBoundary.NumParticles;

        var m_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        m_argsBuffer.SetData(args);

        BoundsParticleMat.SetBuffer(Shader.PropertyToID("positions"), fusion_FluidBoundary.PositionsBuffer);
        BoundsParticleMat.SetFloat(Shader.PropertyToID("Diameter"), ParticleDiameter);
        BoundsParticleMat.SetInt(Shader.PropertyToID("numPart"), fusion_FluidBoundary.NumParticles);

        const ShadowCastingMode castShadow = ShadowCastingMode.Off;
        const bool receiveShadow = false;

        Graphics.DrawMeshInstancedProcedural(
            mesh, 0, BoundsParticleMat,
            new Bounds(Vector3.zero, new Vector3(10, 10, 10)),
            fusion_FluidBoundary.NumParticles, props, castShadow, receiveShadow);
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
