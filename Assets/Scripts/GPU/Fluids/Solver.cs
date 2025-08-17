using System;
using Assets.Scripts.GPU.Fluids;
using DefaultNamespace;
using UnityEngine;

namespace GPU.Fluids
{
    public abstract class Solver : IDisposable, ISolver
    {
        protected uint[] phaseArray;

        public ComputeBuffer phaseBuffer;
        
        //Macros
        protected const int READ = 0;
        protected  const int WRITE = 1;
        
        protected ComputeShader gridHashingShader;
        protected ComputeShader bitonicSortShader;

        protected BodyController controller;
        protected FluidBoundary boundary;
        
        //the hashgrid object
        public GridHash Hash { get; protected set; }

        public Solver(FluidBoundary boundary, 
            BodyController controller, ComputeShader gridHashingShader,
            ComputeShader bitonicSortShader, ComputeBuffer phaseBuffer)
        {
            this.controller = controller;
            this.gridHashingShader = gridHashingShader;
            this.bitonicSortShader = bitonicSortShader;
            this.boundary = boundary;
            this.phaseBuffer = phaseBuffer;
        }
        
        public virtual void Dispose() {
            Hash.Dispose();
            controller.Dispose();
            boundary.Dispose();
        }

        public abstract void StepPhysics(float dt);
    }
}