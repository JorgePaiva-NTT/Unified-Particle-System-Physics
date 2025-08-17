using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Assets.Scripts.CPU;
using UnityEditor;
using UnityEngine;

public class float4
{
    public float x;
    public float y;
    public float z;
    public float w;

    public float3 xyz {
        get { return float3.Make_float3(x, y, z); }
    }

    public static float4 Make_float4(Vector4 vec){
        return new float4(vec.x, vec.y, vec.z, vec.w);
    }

    public static float4 Make_float4(float x, float y, float z, float w) {
        return new float4(x, y, z, w);
    }

    public static float4 Make_float4(float3 v, float w) {
        return new float4(v.x, v.y, v.z, w);
    }
    public float4() {
        this.x = 0.0f;
        this.y = 0.0f;
        this.z = 0.0f;
        this.w = 0.0f;
    }

    private float4(float x, float y, float z, float w) {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public static float4 operator /(float4 r, float4 l)
    {
        float4 res = Make_float4(0, 0, 0, 0);
        res.x = r.x / l.x;
        res.y = r.y / l.y;
        res.z = r.z / l.z;
        res.w = r.w / l.w;
        return res;
    }

    public static float4 operator *(float4 r, float4 l)
    {
        float4 res = Make_float4(0, 0, 0, 0);
        res.x = r.x * l.x;
        res.y = r.y * l.y;
        res.z = r.z * l.z;
        res.w = r.w * l.w;
        return res;
    }

    public static float4 operator +(float4 r, float4 l)
    {
        float4 res = Make_float4(0, 0, 0, 0);
        res.x = r.x + l.x;
        res.y = r.y + l.y;
        res.z = r.z + l.z;
        res.w = r.w + l.w;
        return res;
    }

    public static float4 operator -(float4 r, float4 l)
    {
        var res = Make_float4(0, 0, 0, 0);
        res.x = r.x - l.x;
        res.y = r.y - l.y;
        res.z = r.z - l.z;
        res.w = r.w - l.w;
        return res;
    }

    public static float4 operator /(float4 r, float l)
    {
        float4 res = Make_float4(0, 0, 0, 0);
        res.x = r.x / l;
        res.y = r.y / l;
        res.z = r.z / l;
        res.w = r.w / l;
        return res;
    }

    public static float4 operator *(float4 r, float l)
    {
        float4 res = Make_float4(0, 0, 0, 0);
        res.x = r.x * l;
        res.y = r.y * l;
        res.z = r.z * l;
        res.w = r.w * l;
        return res;
    }

    public static float4 operator *(float r, float4 l)
    {
        float4 res = Make_float4(0, 0, 0, 0);
        res.x = r * l.x;
        res.y = r * l.y;
        res.z = r * l.z;
        res.w = r * l.w;
        return res;
    }

    public static explicit operator Vector4(float4 v) {
        return new Vector4(v.x, v.y, v.z, v.w);
    }

    internal static float4 Make_float4(Vector3 velocity, float v) {
        return new float4(velocity.x, velocity.y, velocity.z, v);
    }
}

public class float3 {
    public float x;
    public float y;
    public float z;
    public static float3 Make_float3(Vector3 vec) {
        return new float3(vec.x, vec.y, vec.z);
    }
    public static float3 Make_float3(float3 vec) {
        return new float3(vec.x, vec.y, vec.z);
    }
    public static float3 Make_float3(float x, float y, float z) {
        return new float3(x, y, z);
    }
    public float3() {
        this.x = 0.0f;
        this.y = 0.0f;
        this.z = 0.0f;
    }
    private float3(float x, float y, float z) {
        this.x = x;
        this.y = y;
        this.z = z;
    }
    public static float3 operator /(float3 r, float3 l) {
        float3 res = Make_float3(0, 0, 0);
        res.x = r.x / l.x;
        res.y = r.y / l.y;
        res.z = r.z / l.z;
        return res;
    } 
    public static float3 operator *(float3 r, float3 l) {
        float3 res = Make_float3(0, 0, 0);
        res.x = r.x * l.x;
        res.y = r.y * l.y;
        res.z = r.z * l.z;
        return res;
    }
    public static float3 operator /(float3 r, float l) {
        float3 res = Make_float3(0, 0, 0);
        res.x = r.x / l;
        res.y = r.y / l;
        res.z = r.z / l;
        return res;
    }
    public static float3 operator *(float3 r, float l) {
        float3 res = Make_float3(0, 0, 0);
        res.x = r.x * l;
        res.y = r.y * l;
        res.z = r.z * l;
        return res;
    }
    public static float3 operator *(float r, float3 l) {
        float3 res = Make_float3(0, 0, 0);
        res.x = r * l.x;
        res.y = r * l.y;
        res.z = r * l.z;
        return res;
    }
    public static float3 operator +(float3 r, float3 l) {
        float3 res = Make_float3(0, 0, 0);
        res.x = r.x + l.x;
        res.y = r.y + l.y;
        res.z = r.z + l.z;
        return res;
    }
    public static float3 operator -(float3 r, float3 l) {
        float3 res = Make_float3(0, 0, 0);
        res.x = r.x - l.x;
        res.y = r.y - l.y;
        res.z = r.z - l.z;
        return res;
    }

    public static float3 operator -(float3 r) {
        return Make_float3(-r.x, -r.y, -r.z);
    }
    public static explicit operator Vector3(float3 v) {
        return new Vector3(v.x, v.y, v.z);
    }

    public static float3 Make_float3(float4 vel)
    {
        return Make_float3(vel.x, vel.y, vel.z);
    }
}

public class uint3 {
    public uint x;
    public uint y;
    public uint z;
    public static uint3 Make_uint3(Vector3 vec) {
        return new uint3((uint)vec.x, (uint)vec.y, (uint)vec.z);
    }
    public static uint3 Make_uint3(uint3 vec) {
        return new uint3((uint)vec.x, (uint)vec.y, (uint)vec.z);
    }
    public static uint3 Make_uint3(uint x, uint y, uint z) {
        return new uint3(x, y, z);
    }
    public uint3() {
        this.x = 0;
        this.y = 0;
        this.z = 0;
    }
    private uint3(uint x, uint y, uint z) {
        this.x = x;
        this.y = y;
        this.z = z;
    }
    public static uint3 operator /(uint3 r, uint3 l) {
        uint3 res = Make_uint3(0, 0, 0);
        res.x = r.x / l.x;
        res.y = r.y / l.y;
        res.z = r.z / l.z;
        return res;
    }
    public static uint3 operator *(uint3 r, uint3 l) {
        uint3 res = Make_uint3(0, 0, 0);
        res.x = r.x * l.x;
        res.y = r.y * l.y;
        res.z = r.z * l.z;
        return res;
    }
    public static uint3 operator /(uint3 r, uint l) {
        uint3 res = Make_uint3(0, 0, 0);
        res.x = r.x / l;
        res.y = r.y / l;
        res.z = r.z / l;
        return res;
    }
    public static uint3 operator *(uint3 r, uint l) {
        uint3 res = Make_uint3(0, 0, 0);
        res.x = r.x * l;
        res.y = r.y * l;
        res.z = r.z * l;
        return res;
    }
    public static uint3 operator *(uint r, uint3 l) {
        uint3 res = Make_uint3(0, 0, 0);
        res.x = r * l.x;
        res.y = r * l.y;
        res.z = r * l.z;
        return res;
    }
    public static uint3 operator +(uint3 r, uint3 l) {
        uint3 res = Make_uint3(0, 0, 0);
        res.x = r.x + l.x;
        res.y = r.y + l.y;
        res.z = r.z + l.z;
        return res;
    }
    public static uint3 operator -(uint3 r, uint3 l) {
        uint3 res = Make_uint3(0, 0, 0);
        res.x = r.x - l.x;
        res.y = r.y - l.y;
        res.z = r.z - l.z;
        return res;
    }
}

public class int3 {
    public int x;
    public int y;
    public int z;
    public static int3 Make_int3(Vector3 vec) {
        return new int3((int)vec.x, (int)vec.y, (int)vec.z);
    }
    public static int3 Make_int3(int3 vec) {
        return new int3((int)vec.x, (int)vec.y, (int)vec.z);
    }
    public static int3 Make_int3(int x, int y, int z) {
        return new int3(x, y, z);
    }
    public int3() {
        this.x = 0;
        this.y = 0;
        this.z = 0;
    }
    private int3(int x, int y, int z) {
        this.x = x;
        this.y = y;
        this.z = z;
    }
    public static int3 operator /(int3 r, int3 l) {
        int3 res = Make_int3(0, 0, 0);
        res.x = r.x / l.x;
        res.y = r.y / l.y;
        res.z = r.z / l.z;
        return res;
    }
    public static int3 operator *(int3 r, int3 l) {
        int3 res = Make_int3(0, 0, 0);
        res.x = r.x * l.x;
        res.y = r.y * l.y;
        res.z = r.z * l.z;
        return res;
    }
    public static int3 operator /(int3 r, int l) {
        int3 res = Make_int3(0, 0, 0);
        res.x = r.x / l;
        res.y = r.y / l;
        res.z = r.z / l;
        return res;
    }
    public static int3 operator *(int3 r, int l) {
        int3 res = Make_int3(0, 0, 0);
        res.x = r.x * l;
        res.y = r.y * l;
        res.z = r.z * l;
        return res;
    }
    public static int3 operator *(int r, int3 l) {
        int3 res = Make_int3(0, 0, 0);
        res.x = r * l.x;
        res.y = r * l.y;
        res.z = r * l.z;
        return res;
    }
    public static int3 operator +(int3 r, int3 l) {
        int3 res = Make_int3(0, 0, 0);
        res.x = r.x + l.x;
        res.y = r.y + l.y;
        res.z = r.z + l.z;
        return res;
    }
    public static int3 operator -(int3 r, int3 l) {
        int3 res = Make_int3(0, 0, 0);
        res.x = r.x - l.x;
        res.y = r.y - l.y;
        res.z = r.z - l.z;
        return res;
    }
}

public class CPUParticleSystem : MonoBehaviour {

    float4[] oldPosition;
    float4[] newPosition;

    float4[] oldVelocity;
    float4[] newVelocity;

    int[] isBody;

    uint[] gridCounters;
    uint[] gridCells;

    public bool ShowGrid = true;

    ComputeBuffer argsBuffer;
    public uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    MaterialPropertyBlock props;

    [SerializeField] Material mat;
    [SerializeField] Mesh mesh;

    [SerializeField] float Gravity;
    [SerializeField] Vector3 Boundaries;
    [SerializeField] int NumParticles = 128;

    [SerializeField] float particleRadius = 1.0f;
    float particleDiameter
    {
        get {
            return particleRadius * 2.0f;
        }
    }
    [SerializeField] float particlesMass = 1.0f;

    [SerializeField] float collideSpring = 0.5f;
    [SerializeField] float collideDamping = 0.01f;
    [SerializeField] float collideShear = 0.01f;
    [SerializeField] float collideAttraction = 0.01f;
    [SerializeField] float globalDamping = 0.99f;
    [SerializeField] float boundaryDamping = 0.8f;
    Vector2 invClothSize;
    float step;

    [SerializeField] float unityTimeStep;
    [SerializeField] float customTimeStep = 0.01f;
    [SerializeField] bool synchronizeTime = false;

    public Transform SphereTransform;

#region WORLD AND GRID
    float3 m_worldSize = float3.Make_float3(2, 2, 2);
    float3 m_gridSize = float3.Make_float3(8, 8, 8);
    float3 m_worldOrigin;
    int totalCells;
    public uint maxParticlesPerCell = 16;
    double m_cellSize;
#endregion

    ComputeBuffer newPositionB;
    ComputeBuffer newVelocityB;
    ComputeBuffer Body;

    //Cloth Properties
    //private int width = 0, height = 0;
    public bool collide = true;
    [Space]
    [Header("Cloth Properties")]
    [Range(0.1f, 300.0f)] public float ks;
    [Range(0.1f, 300.0f)] public float kd;
    [Range(0.001f, 10.000f)] public float damp;
    [Range(1.00000f, 0.000001f)] public float tan;

    void Start()
    {
        m_worldOrigin = float3.Make_float3(-m_worldSize.x / 2, -m_worldSize.y / 2, -m_worldSize.z / 2);
        int boundariesVolume = Mathf.RoundToInt(m_worldSize.x * m_worldSize.y * m_worldSize.z);
        totalCells = Mathf.RoundToInt(m_gridSize.x * m_gridSize.y * m_gridSize.z);

        oldPosition = new float4[NumParticles];
        newPosition = new float4[NumParticles];
        oldVelocity = new float4[NumParticles];
        newVelocity = new float4[NumParticles];
        isBody = new int[NumParticles];

        m_cellSize = m_worldSize.x / m_gridSize.x;
        particleRadius = (float)m_cellSize / (maxParticlesPerCell * 0.5f);

        gridCounters = new uint[totalCells];
        gridCells = new uint[totalCells * maxParticlesPerCell];

        for (int i = 0; i < NumParticles; i++) {
            oldPosition[i] = float4.Make_float4(
                UnityEngine.Random.insideUnitSphere.x * 2.0f, 
                UnityEngine.Random.insideUnitSphere.y * 2.0f, 
                UnityEngine.Random.insideUnitSphere.z * 2.0f, 
                particleRadius);
            oldVelocity[i] = float4.Make_float4(0.0f, 0.0f, 0.0f, 1.0f);
            newPosition[i] = oldPosition[i];
            newVelocity[i] = oldVelocity[i];
            isBody[i] = 0;
        }

        newPositionB = new ComputeBuffer(NumParticles, Marshal.SizeOf(typeof(Vector4)));
        newVelocityB = new ComputeBuffer(NumParticles, Marshal.SizeOf(typeof(Vector4)));
        Body = new ComputeBuffer(NumParticles, sizeof(int));
        
        newPositionB.SetData(Array.ConvertAll(oldPosition, input => { return (Vector4)input; }));
        newVelocityB.SetData(Array.ConvertAll(oldVelocity, input => { return (Vector4)input; }));

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        if (argsBuffer == null)
            return;
        var numIndices = ( mesh != null ) ? mesh.GetIndexCount(0) : 0;
        args[0] = numIndices;
        args[1] = (uint)NumParticles;
        argsBuffer.SetData(args);

        mat.SetBuffer("pBuffer", newPositionB);
        mat.SetBuffer("vBuffer", newVelocityB);

        props = new MaterialPropertyBlock();
        props.SetFloat("_UniqueID", UnityEngine.Random.value);

        CPUCloth cloth1 = new CPUCloth(5, 5);
        CPUBodyController.BodyController.AddBody(cloth1);
        cloth1.SetPositions(-0.5f, -0.2f, 0.5f, ref oldPosition, ref oldVelocity, ref newPosition, ref newVelocity, 
            particleRadius, NumParticles, new bool[4] { true, true, true, true }, ref isBody);
        cloth1.SetConstraints(oldPosition);
            
        CPUCloth cloth2 = new CPUCloth(5, 5);
        CPUBodyController.BodyController.AddBody(cloth2);
        cloth2.SetPositions(0.0f, 0.2f, 0.1f, ref oldPosition, ref oldVelocity, ref newPosition, ref newVelocity,
            particleRadius, NumParticles, new bool[4] { true, true, true, true }, ref isBody);
        cloth2.SetConstraints(oldPosition);

        Body.SetData(isBody);
        mat.SetBuffer("Body", Body);
    }

    void Update() {
        unityTimeStep = Time.deltaTime;
        if (synchronizeTime)
            customTimeStep = Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.R)) {
            
        }

        Integrate();

        newPositionB.SetData(Array.ConvertAll(oldPosition, input => { return (Vector4)input; }));
        newVelocityB.SetData(Array.ConvertAll(oldVelocity, input => { return (Vector4)input; }));
        if (argsBuffer == null)
            return;
        var numIndices = ( mesh != null ) ? mesh.GetIndexCount(0) : 0;
        args[0] = numIndices;
        args[1] = (uint)NumParticles;
        argsBuffer.SetData(args);

        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat,
            new Bounds(transform.position, new Vector3(100, 100, 100)),
            argsBuffer, 0, props);
    }

    private void Integrate() {
        var mCellSize = m_worldSize.x / m_gridSize.x;

        IntegrateParticles();
        STDUtils.Swap(ref oldPosition, ref newPosition);
        STDUtils.Swap(ref oldVelocity, ref newVelocity);

        UpdateCloth();
        STDUtils.Swap(ref oldVelocity, ref newVelocity);
        
        if (collide)
        {
            if (useGrid) {
                gridCounters = new uint[totalCells];
                gridCells = new uint[totalCells * maxParticlesPerCell];

                UpdateGrid(mCellSize);
            }
            SolveCollisions(mCellSize, useGrid);
            STDUtils.Swap(ref oldVelocity, ref newVelocity);
        }
    }
    bool useGrid = true;
    private void OnDrawGizmos()
    {
        if (!ShowGrid)
            return;

        if (gridCounters == null || (gridCounters != null && gridCounters.Length == 0))
            return;
        var gridSizex = Mathf.RoundToInt(m_gridSize.x);
        var gridSizey = Mathf.RoundToInt(m_gridSize.y);
        var gridSizez = Mathf.RoundToInt(m_gridSize.z);
        var cellSize = new Vector3((float)m_cellSize, (float)m_cellSize, (float)m_cellSize);
        int startx = (int)-m_gridSize.x / 2, endx = (int)m_gridSize.x / 2;
        int starty = (int)-m_gridSize.y / 2, endy = (int)m_gridSize.y / 2;
        int startz = (int)-m_gridSize.z / 2, endz = (int)m_gridSize.z / 2;
        for (var x = startx; x < endx; x++) {
            for (var y = starty; y < endy; y++) {
                for (int z = startz; z < endz; z++) {
                    Vector3 center = (cellSize / 2) + new Vector3(x * (float)m_cellSize, y * (float)m_cellSize, z * (float)m_cellSize);
                    int xx = x + Mathf.RoundToInt(gridSizex * 0.5f);
                    int yy = y + Mathf.RoundToInt(gridSizey * 0.5f);
                    int zz = z + Mathf.RoundToInt(gridSizez * 0.5f);
                    int index = (( xx * gridSizex ) + yy ) * gridSizey + zz;
                    var pic = gridCounters[index];
                    if (index < gridCounters.Length && pic != 0) {
                        if (pic >= maxParticlesPerCell)
                            Gizmos.color = Color.red;
                        else if (pic > 1 && pic < maxParticlesPerCell)
                            Gizmos.color = Color.yellow;
                        else
                            Gizmos.color = Color.green;
                    }
                    else {
                        Gizmos.color = new Color(0.1f, 0.1f, 0.1f, 0.15f);
                    }
                    Gizmos.DrawWireCube(center, cellSize);
                }
            }
        }
    }

    private void OnDestroy() {
        if (newPositionB != null) newPositionB.Dispose();
        if (newVelocityB != null) newVelocityB.Dispose();
        if (argsBuffer != null) argsBuffer.Dispose();
    }

    private const float Ml = 10.0f;

    private void IntegrateParticles() {
        for (var index = 0; index < NumParticles; index++) {

            var composedVelocity = oldVelocity[index];
            var pos = oldPosition[index];

            if (Utils.Length(composedVelocity.xyz) >= Ml) {
                //Resize the velocity vector 
                var cv3 = Utils.Normalize(composedVelocity.xyz) * Ml;
                composedVelocity.x = cv3.x;
                composedVelocity.y = cv3.y;
                composedVelocity.z = cv3.z;
            }
            
            IntegrateForces(ref pos, ref composedVelocity);

            var pRad = pos.w;
            var bounceDamping = -boundaryDamping;
            if (pos.x > m_worldSize.x / 2 - pRad) {
                pos.x = m_worldSize.x / 2 - pRad;
                composedVelocity.x *= bounceDamping;
            }
            if (pos.x < -m_worldSize.x / 2 + pRad) {
                pos.x = -m_worldSize.x / 2 + pRad;
                composedVelocity.x *= bounceDamping;
            }
            if (pos.y > m_worldSize.y / 2 - pRad) {
                pos.y = m_worldSize.y / 2 - pRad;
                composedVelocity.y *= bounceDamping;
            }
            if (pos.y < -m_worldSize.y / 2 + pRad) {
                pos.y = -m_worldSize.y / 2 + pRad;
                composedVelocity.y *= bounceDamping;
            }
            if (pos.z > m_worldSize.z / 2 - pRad) {
                pos.z = m_worldSize.z / 2 - pRad;
                composedVelocity.z *= bounceDamping;
            }
            if (pos.z < -m_worldSize.z / 2 + pRad) {
                pos.z = -m_worldSize.z / 2 + pRad;
                composedVelocity.z *= bounceDamping;
            }

            newVelocity[index] = composedVelocity;
            newPosition[index] = pos;
        }
    }

    private void UpdateCloth() {
        CPUBodyController.BodyController.SolveBodiesConstraints(oldPosition, oldVelocity, ref newVelocity, ref newPosition);
    }

    private void UpdateGrid(float cellSize) {
        for (int index = 0; index < NumParticles; index++) {
            UpdateGridD(
                oldPosition[index], 
                (uint)index, 
                uint3.Make_uint3((uint)m_gridSize.x, (uint)m_gridSize.x, (uint)m_gridSize.x), 
                float3.Make_float3(cellSize, cellSize, cellSize), 
                m_worldOrigin, 
                maxParticlesPerCell);
        }
    }

    private void SolveCollisions(float cellSize, bool useGrid) {
        for (uint index = 0; index < NumParticles; index++) {
            float4 pos = oldPosition[index];
            float4 vel = oldVelocity[index];
            float3 force = float3.Make_float3(0.0f, 0.0f, 0.0f);
            if (useGrid) {
                SolveGridCollisions(ref force, index, cellSize, 1, pos, vel);
            }
            else {
                BruteForceSolveCollisions(ref force, index, pos, vel);
            }
            newVelocity[index] = vel + float4.Make_float4(force, 0.0f);
        }
    }
    private void BruteForceSolveCollisions(ref float3 force, uint index, float4 pos, float4 vel) {
        for (uint other = 0; other < NumParticles; other++) {
            if (other != index) {
                float4 pos2 = oldPosition[other];
                float4 vel2 = oldVelocity[other];
                float radiusB = pos2.w;
                //Collide them
                force += collideSpheres(pos, pos2, vel, vel2, collideAttraction, pos.w, radiusB);
            }
        }
    }

    private void SolveGridCollisions(ref float3 force, uint index, float cellSize, int searchRadius, float4 pos, float4 vel) {
        float3 cellS = float3.Make_float3(cellSize, cellSize, cellSize);
        float3 pos3 = float3.Make_float3(pos.x, pos.y, pos.z);

        int3 gridPos = calcGridPos(pos3, m_worldOrigin, cellS);

        for (int z = -searchRadius; z <= searchRadius; z++) 
        {
            for (int y = -searchRadius; y <= searchRadius; y++) 
            {
                for (int x = -searchRadius; x <= searchRadius; x++) 
                {
                    int3 nGridPos = gridPos + int3.Make_int3(x, y, z);
                    force += CollideCell(index, pos, vel, nGridPos,
                        uint3.Make_uint3((uint)m_gridSize.x, (uint)m_gridSize.x, (uint)m_gridSize.x), maxParticlesPerCell);
                }
            }
        }

        force += collideSpheres(pos, float4.Make_float4(SphereTransform.position), vel,
            float4.Make_float4(SphereTransform.GetComponent<Rigidbody>().velocity, 1.0f), collideAttraction, particleRadius, SphereTransform.lossyScale.x * 0.5f);
    }
    float3 collideSpheres(float4 particleAPos, float4 particleBPos, float4 particleAVel, float4 particleBVel, float attraction, float radiusA, float radiusB) {
        float3 posA = particleAPos.xyz;
        float3 velA = particleAVel.xyz;

        float3 posB = particleBPos.xyz;
        float3 velB = particleBVel.xyz;

        float3 relPos = float3.Make_float3(0, 0, 0);
        relPos = posB - posA;

        float dist = Utils.Length(relPos);

        float3 force = float3.Make_float3(0.0f, 0.0f, 0.0f);

        if (dist < radiusA + radiusB) {
            float3 norm = relPos / dist;
            float3 relVel = float3.Make_float3(0, 0, 0);
            relVel = velB - velA;
            float3 tanVel = relVel - ( Utils.Dot(relVel, norm) * norm );

            force = -collideSpring * ( radiusA + radiusB - dist ) * norm;
            force += collideDamping * relVel;
            force += collideShear * tanVel;
            force += attraction * relPos;
            force *= particleAVel.w;
        }
        return force;
    }
    void IntegrateForces(ref float4 pos, ref float4 velocity) {
        float mass = velocity.w;
        if (mass == 0.0f)
            return;
        // Calculate acceleration ( F = ma )
        float3 accel = float3.Make_float3(0.0f, 0.0f, 0.0f);
        if (mass != 0)
            accel = float3.Make_float3(0.0f, Gravity / mass, 0.0f);
        float3 f = mass * accel;

        // Integrate to get velocity
        velocity.x += ( f.x * customTimeStep ) * globalDamping;
        velocity.y += ( f.y * customTimeStep ) * globalDamping;
        velocity.z += ( f.z * customTimeStep ) * globalDamping;

        // Integrate to get position
        float3 vel3 = float3.Make_float3(velocity.x, velocity.y, velocity.z);
        pos += float4.Make_float4(vel3, 0.0f) * customTimeStep;
    }

    int3 calcGridPos(float3 pos, float3 worldOrigin, float3 cellSize)
    {
        int3 gridPos = int3.Make_int3(0, 0, 0);
        gridPos.x = Utils.Floor(( pos.x - worldOrigin.x ) / cellSize.x);
        gridPos.y = Utils.Floor(( pos.y - worldOrigin.y ) / cellSize.y);
        gridPos.z = Utils.Floor(( pos.z - worldOrigin.z ) / cellSize.z);
        return gridPos;
    }
    uint calcGridAddress(int3 gridPos, uint3 gridSize) {
        gridPos.x = Utils.Max(0, Utils.Min(gridPos.x, gridSize.x - 1));
        gridPos.y = Utils.Max(0, Utils.Min(gridPos.y, gridSize.y - 1));
        gridPos.z = Utils.Max(0, Utils.Min(gridPos.z, gridSize.z - 1));

        /**
         Isto foi fixed
         deveria ser :
                    gridPos.x * gridSize.y  e nao como inicialmente estava gridPos.z * gridSize.y 
                    e no final nao somar(+) gridPos.x e sim gridPos.z
         */
        return (uint)Mathf.RoundToInt((( gridPos.x * gridSize.y) * gridSize.x) + ( gridPos.y * gridSize.x) + gridPos.z);
    }
    void AddParticleToCell(int3 gridPos, uint index, uint maxPperCell, uint3 gridSize) {
        uint gridAddress = calcGridAddress(gridPos, gridSize);
        uint counter = 0;
        gridCounters[gridAddress] += 1;

        //Fix   subtrair 1 no final
        counter = gridCounters[gridAddress] - 1;
        counter = (uint)Utils.Min(counter, maxPperCell - 1);
        gridCells[gridAddress * maxPperCell + counter] = index;
    }
    void UpdateGridD(float4 pos, uint index, uint3 gridSize, float3 cellSize, float3 worldOrigin, uint maxParticlesPerCell) {
        int3 gridPos = calcGridPos(pos.xyz, worldOrigin, cellSize);
        AddParticleToCell(gridPos, index, maxParticlesPerCell, gridSize);
    }
    float3 CollideCell(uint index, float4 pos, float4 vel, int3 gridPos, uint3 gridSize, uint maxParticlesPerCell) {
        float3 force = float3.Make_float3(0.0f, 0.0f, 0.0f);

        ////Check out of bounds
        if (( gridPos.x < 0 ) || ( gridPos.x > (int)(gridSize.x) - 1 ) ||
            ( gridPos.y < 0 ) || ( gridPos.y > (int)(gridSize.y) - 1 ) ||
            ( gridPos.z < 0 ) || ( gridPos.z > (int)(gridSize.z) - 1 )) {
            return force;
        }

        uint gridAddress = calcGridAddress(gridPos, gridSize);

        //Now we dont iterate all the particles in the Buffer
        //as that would create a O(N^2) problem
        //=================================================
        // iterate over particles in this cell
        uint particlesInCell = gridCounters[gridAddress];
        particlesInCell = (uint)Utils.Min(particlesInCell, maxParticlesPerCell - 1);

        float radiusA = pos.w;

        /**
         Probelma encontrado neste loop
         sintoma : os indices das particulas dentro da sua celula estava a 
                    ser escritos 1 indice emediatamente aseguir ao suposto
                    
                    1 = Particula de index 1
                    cell 1  |cell 2 |cell 3...
                    |0|0|0|0|0|1|0|0|0|...
                    cell 1  |cell 2 |cell 3...
                    devia ser :
                    cell 1  |cell 2 |cell 3...
                    |0|0|0|0|1|0|0|0|0|...
                    cell 1  |cell 2 |cell 3...

         */
        for (uint i = 0; i < particlesInCell; i++) {
            uint index2 = gridCells[gridAddress * maxParticlesPerCell + i];
            if (isBody[index2] == 1.0f && isBody[index] == 1.0f)
                continue;
            if (index2 != index) {
                //Now we are fetching only the particle inside this collision cell
                float4 pos2 = oldPosition[index2];
                float4 vel2 = oldVelocity[index2];
                float radiusB = pos2.w;
                //Collide them
                force += collideSpheres(pos, pos2, vel, vel2, collideAttraction, radiusA, radiusB);
            }
        }
        return force;
    }
}
