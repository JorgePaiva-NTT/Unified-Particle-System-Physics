using System;
using System.Collections.Generic;
using Assets.Scripts.GPU.Fluids;
using DefaultNamespace;
using UnityEngine;
using Cloth = UnityEngine.Cloth;

namespace GPU.Fluids
{
    public class FluidSolverN : Solver
    {
        //group size
        private const int THREADS = 128;
        
        //number of groups
        public int Groups { get; private set; }

        //the fluid body object
        public FluidBody[] Body { get; private set; }
        protected ComputeShader fluidSolverShader;


        //the number of iterations
        public int DensityComputeIterations { get; set; }
        //the number of constraint iterations
        public int ConstraintComputeIterations { get; set; }
        //the smoothing Kernel object, contains the mathematical calculations of various Kernels
        public SmoothingKernel Kernel { get; private set; }

        public FluidSolverN(
            BodyController controller, FluidBody[] body, FluidBoundary boundary, 
            int densityComputeIteration, int constraintComputeIteration,
            ComputeShader fluidSolverShader,
            ComputeShader gridHashingShader, ComputeShader bitonicSortShader, ComputeBuffer phaseBuffer)
            : base(boundary, controller, gridHashingShader, bitonicSortShader, phaseBuffer)
        {
            DensityComputeIterations = densityComputeIteration;
            ConstraintComputeIterations = constraintComputeIteration;

            Body = body;

            var cellSize = controller.ParticleRadius * 4.0f;
            var total = controller.NumParticles + boundary.NumParticles;
            Hash = new GridHash(boundary.Bounds, total, cellSize,
                this.gridHashingShader, this.bitonicSortShader);
            Kernel = new SmoothingKernel(cellSize);

            var numParticles = controller.NumParticles;
            Debug.Log("Number of Particles: " + numParticles);
            
            Groups = numParticles / THREADS;
            if (numParticles % THREADS != 0) Groups++;

            Debug.Log("Groups: " + Groups);

            this.fluidSolverShader = fluidSolverShader;
        }

        public override void StepPhysics(float dt)
        {
            if (dt <= 0.0) return;
            if (DensityComputeIterations <= 0 || ConstraintComputeIterations <= 0) return;

            dt /= DensityComputeIterations;

            foreach (var body in Body)
            {
                if (body == null)
                    continue;

                fluidSolverShader.SetInt("NumParticles", controller.NumParticles);
                fluidSolverShader.SetVector("Gravity", new Vector3(0.0f, -9.81f, 0.0f));
                fluidSolverShader.SetFloat("Dampning", body.DampingCoeff);
                fluidSolverShader.SetFloat("DeltaTime", dt);
                fluidSolverShader.SetFloat("Density", body.density);
                fluidSolverShader.SetFloat("Viscosity", body.ViscosityCoeff);
                fluidSolverShader.SetFloat("ParticleMass", controller.ParticleMass);

                fluidSolverShader.SetFloat("KernelRadius", Kernel.Radius);
                fluidSolverShader.SetFloat("KernelRadius2", Kernel.Radius2);
                fluidSolverShader.SetFloat("Poly6Zero", Kernel.Poly6(Vector3.zero));
                fluidSolverShader.SetFloat("Poly6", Kernel.POLY6);
                fluidSolverShader.SetFloat("SpikyGrad", Kernel.SPIKY_GRAD);
                fluidSolverShader.SetFloat("ViscLap", Kernel.VISC_LAP);

                fluidSolverShader.SetFloat("HashScale", Hash.InvCellSize);
                fluidSolverShader.SetVector("HashSize", Hash.Bounds.size);
                fluidSolverShader.SetVector("HashTranslate", Hash.Bounds.min);

                fluidSolverShader.SetVector("BoundMin", boundary.Bounds.min);
                fluidSolverShader.SetVector("BoundMax", boundary.Bounds.max);

                fluidSolverShader.SetInt("fluidId", body.id);

                //Predicted and velocities use a double buffer as solver step
                //needs to read from many locations of buffer and write the result
                //in same pass. Could be removed if needed as long as buffer writes 
                //are atomic. Not sure if they are.

                for (var i = 0; i < DensityComputeIterations; i++)
                {
                    PredictPositions();

                    Hash.Process(controller.PredictedBuffer[READ], boundary.PositionsBuffer);

                    ConstrainPositions();
                    
                    UpdateVelocities();

                    SolveViscosity();

                    UpdatePositions();
                }
            }
        }

        private void PredictPositions()
        {
            int kernel = fluidSolverShader.FindKernel("PredictPositions");

            fluidSolverShader.SetBuffer(kernel, "Positions", controller.PositionsBuffer);
            fluidSolverShader.SetBuffer(kernel, "PredictedWRITE", controller.PredictedBuffer[WRITE]);
            fluidSolverShader.SetBuffer(kernel, "VelocitiesREAD", controller.VelocitiesBuffer[READ]);
            fluidSolverShader.SetBuffer(kernel, "VelocitiesWRITE", controller.VelocitiesBuffer[WRITE]);
            fluidSolverShader.SetBuffer(kernel, "phase", controller.phaseBuffer);

            fluidSolverShader.Dispatch(kernel, Groups, 1, 1);

            STDUtils.Swap(controller.PredictedBuffer);
            STDUtils.Swap(controller.VelocitiesBuffer);
        }

        public void ConstrainPositions()
        {

            int computeDensityKernelID = fluidSolverShader.FindKernel("ComputeDensity");
            int solveKernel = fluidSolverShader.FindKernel("SolveConstraint");
            int collider = fluidSolverShader.FindKernel("SolveClothCollision");
            
            fluidSolverShader.SetBuffer(computeDensityKernelID, "Densities", controller.DensitiesBuffer);
            fluidSolverShader.SetBuffer(computeDensityKernelID, "Pressures", controller.PressuresBuffer);
            fluidSolverShader.SetBuffer(computeDensityKernelID, "Boundary", boundary.PositionsBuffer);
            fluidSolverShader.SetBuffer(computeDensityKernelID, "IndexMap", Hash.IndexMap);
            fluidSolverShader.SetBuffer(computeDensityKernelID, "Table", Hash.Table);
            fluidSolverShader.SetBuffer(computeDensityKernelID, "phase", controller.phaseBuffer);
            
            fluidSolverShader.SetBuffer(solveKernel, "Pressures", controller.PressuresBuffer);
            fluidSolverShader.SetBuffer(solveKernel, "Boundary", boundary.PositionsBuffer);
            fluidSolverShader.SetBuffer(solveKernel, "IndexMap", Hash.IndexMap);
            fluidSolverShader.SetBuffer(solveKernel, "Table", Hash.Table);
            
            fluidSolverShader.SetBuffer(collider, "Boundary", boundary.PositionsBuffer);
            fluidSolverShader.SetBuffer(collider, "IndexMap", Hash.IndexMap);
            fluidSolverShader.SetBuffer(collider, "Table", Hash.Table);
            
            for (int i = 0; i < ConstraintComputeIterations; i++)
            {
                fluidSolverShader.SetBuffer(computeDensityKernelID, "PredictedREAD", controller.PredictedBuffer[READ]);
                fluidSolverShader.SetBuffer(computeDensityKernelID, "phase", controller.phaseBuffer);
                fluidSolverShader.Dispatch(computeDensityKernelID, Groups, 1, 1);

                fluidSolverShader.SetBuffer(solveKernel, "PredictedREAD", controller.PredictedBuffer[READ]);
                fluidSolverShader.SetBuffer(solveKernel, "PredictedWRITE", controller.PredictedBuffer[WRITE]);
                fluidSolverShader.SetBuffer(solveKernel, "phase", controller.phaseBuffer);
                fluidSolverShader.Dispatch(solveKernel, Groups, 1, 1);
                
                fluidSolverShader.SetBuffer(collider, "PredictedREAD", controller.PredictedBuffer[READ]);
                fluidSolverShader.SetBuffer(collider, "VelocitiesREAD", controller.VelocitiesBuffer[READ]);
                fluidSolverShader.SetBuffer(collider, "VelocitiesWRITE", controller.VelocitiesBuffer[WRITE]);
                fluidSolverShader.SetBuffer(collider, "phase", controller.phaseBuffer);
                fluidSolverShader.Dispatch(collider, Groups, 1, 1);
                
                STDUtils.Swap(controller.PredictedBuffer);
                STDUtils.Swap(controller.VelocitiesBuffer);
            }
        }

        private void UpdateVelocities()
        {
            var kernel = fluidSolverShader.FindKernel("UpdateVelocities");

            fluidSolverShader.SetBuffer(kernel, "Positions", controller.PositionsBuffer);
            fluidSolverShader.SetBuffer(kernel, "PredictedREAD", controller.PredictedBuffer[READ]);
            fluidSolverShader.SetBuffer(kernel, "VelocitiesWRITE", controller.VelocitiesBuffer[WRITE]);
            fluidSolverShader.SetBuffer(kernel, "phase", controller.phaseBuffer);
            
            fluidSolverShader.Dispatch(kernel, Groups, 1, 1);

            STDUtils.Swap(controller.VelocitiesBuffer);
        }

        private void SolveViscosity()
        {
            var kernel = fluidSolverShader.FindKernel("SolveViscosity");

            fluidSolverShader.SetBuffer(kernel, "Densities", controller.DensitiesBuffer);
            fluidSolverShader.SetBuffer(kernel, "Boundary", boundary.PositionsBuffer);
            fluidSolverShader.SetBuffer(kernel, "IndexMap", Hash.IndexMap);
            fluidSolverShader.SetBuffer(kernel, "Table", Hash.Table);

            fluidSolverShader.SetBuffer(kernel, "PredictedREAD", controller.PredictedBuffer[READ]);
            fluidSolverShader.SetBuffer(kernel, "VelocitiesREAD", controller.VelocitiesBuffer[READ]);
            fluidSolverShader.SetBuffer(kernel, "VelocitiesWRITE", controller.VelocitiesBuffer[WRITE]);
    
            fluidSolverShader.SetBuffer(kernel, "phase", controller.phaseBuffer);    
            
            fluidSolverShader.Dispatch(kernel, Groups, 1, 1);

            STDUtils.Swap(controller.VelocitiesBuffer);
        }

        private void UpdatePositions()
        {
            var kernel = fluidSolverShader.FindKernel("UpdatePositions");

            fluidSolverShader.SetBuffer(kernel, "Positions", controller.PositionsBuffer);
            fluidSolverShader.SetBuffer(kernel, "PredictedREAD", controller.PredictedBuffer[READ]);
            fluidSolverShader.SetBuffer(kernel, "phase", controller.phaseBuffer);
            fluidSolverShader.SetBuffer(kernel, "VelocitiesWRITE", controller.VelocitiesBuffer[WRITE]);

            fluidSolverShader.Dispatch(kernel, Groups, 1, 1);
        }
    }
}