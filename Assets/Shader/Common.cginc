#ifndef _PARTICLE_COMPUTE_COMMON_INCLUDED_
#define _PARTICLE_COMPUTE_COMMON_INCLUDED_

#include "./Common/Defines.cginc"

#ifndef USE_TEX
    static int THREAD_X = 32;
    static int THREAD_Y = 1;
    static int THREAD_Z = 1;
#else
    static int THREAD_X = 32;
    static int THREAD_Y = 32;
    static int THREAD_Z = 1;
#endif

#define factor 1

//Cells are considered cubes
//all sides have the same size
float _cellSize;
float3 _gridSize;

//Defines the origin of the grid
float3 _worldOrigin;
int _maxParticlesPerCell;

#ifndef USE_TEX

    RWBuffer<float4> oldPositionBuffer;
    RWBuffer<float4> newPositionBuffer;

    RWBuffer<float4> oldVelocityBuffer;
    RWBuffer<float4> newVelocityBuffer;

#else

    Texture2D<float4> oldPositionTexture;
    RWTexture2D<float4> newPositionTexture;
                
    Texture2D<float4> oldVelocityTexture;
    RWTexture2D<float4> newVelocityTexture;

#endif

RWBuffer<uint> phaseBuffer;

//Grid Buffers
RWBuffer<uint> gridCounters;
RWBuffer<uint> gridCells;
RWBuffer<uint2> particleHash;
RWBuffer<uint> cellStart;


#endif