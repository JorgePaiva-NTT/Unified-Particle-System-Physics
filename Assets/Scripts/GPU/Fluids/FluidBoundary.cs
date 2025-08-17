using System;
using DefaultNamespace;
using GPU;
using UnityEngine;

namespace Assets.Scripts.GPU.Fluids
{
    public class FluidBoundary : IDisposable
    {
        //group size
        private const int THREADS = 128;
        //number of particles consisting the boundary
        public int NumParticles { get; private set; }
        //boundary in 3D space
        public Bounds Bounds;
        //particle radius, as in fluid body
        public float ParticleRadius { get; private set; }
        //diameter
        public float ParticleDiameter { get { return ParticleRadius * 2.0f; } }
        //density coefficient
        public float DensityCoeff { get; private set; }
        //Compute buffer that stores the positions of those particles
        
        public ComputeBuffer PositionsBuffer { get; private set; }
        //argument buffer for GPU instance drawing
        //private ComputeBuffer m_argsBuffer;
        
        
        public FluidBoundary(ParticleSource source, float radius, float density, Matrix4x4 RTS,
            ComputeShader gridHashingShader, ComputeShader bitonicSortShader, 
            ComputeShader createBoundary)
        {
            NumParticles = source.NumParticles;
            ParticleRadius = radius;
            DensityCoeff = density;
            
            CreateParticles(source, RTS);
            CreateBoundryPsi(gridHashingShader, bitonicSortShader, createBoundary);
        }


        public void Dispose()
        {
            if (PositionsBuffer == null) return;
            PositionsBuffer.Release();
            PositionsBuffer = null;
        }

        private void CreateParticles(ParticleSource source, Matrix4x4 RTS)
        {
            var positions = new Vector4[NumParticles];

            var inf = float.PositiveInfinity;
            var min = new Vector3(inf, inf, inf);
            var max = new Vector3(-inf, -inf, -inf);

            for (var i = 0; i < NumParticles; i++)
            {
                Vector4 pos = RTS * source.Positions[i];
                positions[i] = pos;

                if (pos.x < min.x) min.x = pos.x;
                if (pos.y < min.y) min.y = pos.y;
                if (pos.z < min.z) min.z = pos.z;

                if (pos.x > max.x) max.x = pos.x;
                if (pos.y > max.y) max.y = pos.y;
                if (pos.z > max.z) max.z = pos.z;
            }

            min.x -= ParticleRadius;
            min.y -= ParticleRadius;
            min.z -= ParticleRadius;

            max.x += ParticleRadius;
            max.y += ParticleRadius;
            max.z += ParticleRadius;

            Bounds = new Bounds();
            Bounds.SetMinMax(min, max);

            PositionsBuffer = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            PositionsBuffer.SetData(positions);

        }

        private void CreateBoundryPsi(ComputeShader gridHashingShader, ComputeShader bitonicSortShader, 
            ComputeShader createBoundary)
        {

            var cellSize = ParticleRadius * 4.0f;
            var K = new SmoothingKernel(cellSize);

            var grid = new GridHash(Bounds, NumParticles, cellSize, gridHashingShader, bitonicSortShader);
            grid.Process(PositionsBuffer);

            var createBoundaryShader = createBoundary;

            var psiKernelID = createBoundaryShader.FindKernel("ComputePsi");

            createBoundaryShader.SetFloat("Density", DensityCoeff);
            createBoundaryShader.SetFloat("KernelRadiuse", K.Radius);
            createBoundaryShader.SetFloat("KernelRadius2", K.Radius2);
            createBoundaryShader.SetFloat("Poly6", K.POLY6);
            createBoundaryShader.SetFloat("Poly6Zero", K.Poly6(Vector3.zero));
            createBoundaryShader.SetInt("NumParticles", NumParticles);
            
            createBoundaryShader.SetFloat("HashScale", grid.InvCellSize);
            createBoundaryShader.SetVector("HashSize", grid.Bounds.size);
            createBoundaryShader.SetVector("HashTranslate", grid.Bounds.min);
            createBoundaryShader.SetBuffer(psiKernelID, "IndexMap", grid.IndexMap);
            createBoundaryShader.SetBuffer(psiKernelID, "Table", grid.Table);
            
            createBoundaryShader.SetBuffer(psiKernelID, "Boundary", PositionsBuffer);

            var groups = NumParticles / THREADS;
            if (NumParticles % THREADS != 0) groups++;

            //Fills the boundaries psi array so the fluid can
            //collide against it smoothly. The original computes
            //the phi for each boundary particle based on the
            //density of the boundary but I find the fluid 
            //leaks out so Im just using a const value.
            createBoundaryShader.Dispatch(psiKernelID, groups, 1, 1);

            grid.Dispose();
        }

    }
}