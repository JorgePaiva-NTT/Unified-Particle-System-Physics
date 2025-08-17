//Function Declarations

//Simple Sphere to Sphere Collisions
//Using DEM
float3 collideSpheres(float4 particleAPos, 
                      float4 particleBPos, 
                      float3 particleAVel,
                      float3 particleBVel, 
                      float attraction, 
                      float radiusA, 
                      float radiusB);

float3 collideCell(int3 gridPos, uint index, float4 pos, float4 vel);

void Collide();

//Integrate physics for each particle
void integrateForces(inout float4 pos, inout float4 velocity);


//Grid Functions
int3 calcGridPos(float4 pos, float3 worldOrigin, float3 cellSize);
uint calcGridAddress(int3 gridPos, float3 gridSize);
void addParticleToCell(int3 gridPos, uint index, uint maxParticlesPerCell, float3 gridSize);
void updateGridD(float4 pos, uint index, float3 gridSize, float3 cellSize, float3 worldOrigin, uint maxParticlesPerCell);

//Not Implemented Yet
//Sort
void calcHash(float4 pos, /*particle world position */uint index, uint3 gridSize, float3 cellSize, float3 worldOrigin);
void findCellStart(uint index, uint numParticles, uint numCells);

//Collisions
float3 collideCell(uint index, float4 pos, float4 vel, int3 gridPos, float3 gridSize, uint maxParticlesPerCell);