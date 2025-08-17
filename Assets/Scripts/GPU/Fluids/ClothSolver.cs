using System.Collections.Generic;
using Assets.Scripts.GPU.Fluids;
using DefaultNamespace;
using UnityEngine;

namespace GPU.Fluids
{
    public class ClothSolver : Solver
    {
        ComputeShader clothUpdateShader;

        private int CLOTH_GROUPS_X = 1;
        private int CLOTH_GROUPS_Y = 1;
        private int CLOTH_GROUPS_Z = 1;

        public ClothBody[] clothBodies;

        private int looper = 50;
        private int solverIterations = 6;

        public ClothSolver(
            BodyController controller,
            ComputeShader gridHashingShader, ComputeShader bitonicSortShader,
            FluidBoundary boundary, ComputeShader clothShader, ComputeBuffer phaseBuffer, ClothBody[] clothBody)
            : base(boundary,controller, gridHashingShader, bitonicSortShader, phaseBuffer)
        {
            this.clothBodies = clothBody;
            clothUpdateShader = clothShader;
            
            var cellSize = controller.ParticleRadius * 2.0f;
            var total = controller.NumParticles + boundary.NumParticles;
            Hash = new GridHash(boundary.Bounds, total, cellSize,
                this.gridHashingShader, this.bitonicSortShader);
        }

        public override void Dispose()
        { }

        public override void StepPhysics(float dt)
        {
            for (int b = 0; b < controller.cpuSideClothInfo.Count; b++) {
                
                var r = UnityEngine.Random.Range(0.0001f, 2.0f);
                var r1 = UnityEngine.Random.Range(0.0001f, 2.0f);
                var r2 = UnityEngine.Random.Range(0.0001f, 2.0f);
                clothUpdateShader.SetVector("ExeternalForce", new Vector4(0,0,0,0));
                clothUpdateShader.SetFloat("SpringK", clothBodies[b].springK);
                clothUpdateShader.SetFloat("DampingConst", clothBodies[b].damping);
                clothUpdateShader.SetFloat("DeltaT", dt / solverIterations);
                clothUpdateShader.SetFloat("DeltaT2", dt / solverIterations * dt / solverIterations);
                clothUpdateShader.SetFloat("nodeMass", controller.ParticleMass);
                clothUpdateShader.SetFloat("Damping", clothBodies[b].damping);
                clothUpdateShader.SetInt("clothId", clothBodies[b].id);
                
                clothUpdateShader.SetInt("NumParticles", controller.NumParticles);
                clothUpdateShader.SetVector("Gravity", new Vector3(0.0f, -9.81f, 0.0f));
                
                clothUpdateShader.SetVector("HashTranslate", Hash.Bounds.min);
                clothUpdateShader.SetFloat("HashScale", Hash.InvCellSize);
                clothUpdateShader.SetVector("HashSize", Hash.Bounds.size);

                var size = (int) clothBodies[b].width * (int) clothBodies[b].height;
                
                for (var i = 0; i < solverIterations; i++)
                {
                    PredictPositions(size);

                    //Hash.Process(controller.PredictedBuffer[READ], boundary.PositionsBuffer);
                    
                    //SolveCollisions(controller.NumParticles);

                    UpdateVelocities(controller.NumParticles);

                    SolveCloth(b);

                    UpdatePositions(controller.NumParticles);
                }
            }
        }

        public void SetupPressuresAndDensities(int size, int b) {
            var kernel = clothUpdateShader.FindKernel("Setup");
            
            clothUpdateShader.SetFloat("Kden", clothBodies[b].density);
            
            clothUpdateShader.SetBuffer(kernel, "Densities", controller.DensitiesBuffer);
            clothUpdateShader.SetBuffer(kernel, "Pressures", controller.PressuresBuffer);
            clothUpdateShader.SetBuffer(kernel, "clothData", controller.cpuClothData);
            
            clothUpdateShader.Dispatch(kernel, controller.NumParticles / 8, 1, 1);
        }
        
        private void PredictPositions(int size)
        {
            var kernel = clothUpdateShader.FindKernel("PredictPositions");
            
            clothUpdateShader.SetBuffer(kernel, "clothData", controller.cpuClothData);
            clothUpdateShader.SetBuffer(kernel, "Positions", controller.PositionsBuffer);
            clothUpdateShader.SetBuffer(kernel, "PredictedWRITE", controller.PredictedBuffer[WRITE]);
            clothUpdateShader.SetBuffer(kernel, "VelocitiesREAD", controller.VelocitiesBuffer[READ]);
            clothUpdateShader.SetBuffer(kernel, "VelocitiesWRITE", controller.VelocitiesBuffer[WRITE]);
            clothUpdateShader.SetBuffer(kernel, "phase", controller.phaseBuffer);
            
            clothUpdateShader.Dispatch(kernel, controller.NumParticles / 8, 1, 1);

            STDUtils.Swap(controller.PredictedBuffer);
            STDUtils.Swap(controller.VelocitiesBuffer);
            
        }

        private void SolveCollisions(int size)
        {
            var solveKernel = clothUpdateShader.FindKernel("SolveCollisions");
            
            clothUpdateShader.SetBuffer(solveKernel, "Boundary", boundary.PositionsBuffer);
            clothUpdateShader.SetBuffer(solveKernel, "IndexMap", Hash.IndexMap);
            clothUpdateShader.SetBuffer(solveKernel, "Table", Hash.Table);

            for (var i = 0; i < 1; i++)
            {
                clothUpdateShader.SetBuffer(solveKernel, "PredictedREAD", controller.PredictedBuffer[READ]);
                clothUpdateShader.SetBuffer(solveKernel, "PredictedWRITE", controller.PredictedBuffer[WRITE]);
                clothUpdateShader.SetBuffer(solveKernel, "VelocitiesREAD", controller.VelocitiesBuffer[READ]);
                clothUpdateShader.SetBuffer(solveKernel, "phase", controller.phaseBuffer);
                
                
                clothUpdateShader.Dispatch(solveKernel, controller.NumParticles / 8, 1, 1);
                
                STDUtils.Swap(controller.PredictedBuffer);
            }
        }

        private void UpdateVelocities(int size)
        {
            var kernel = clothUpdateShader.FindKernel("UpdateVelocities");

            clothUpdateShader.SetBuffer(kernel, "clothData", controller.cpuClothData);
            clothUpdateShader.SetBuffer(kernel, "Positions", controller.PositionsBuffer);
            clothUpdateShader.SetBuffer(kernel, "PredictedREAD", controller.PredictedBuffer[READ]);
            clothUpdateShader.SetBuffer(kernel, "VelocitiesWRITE", controller.VelocitiesBuffer[WRITE]);
            clothUpdateShader.SetBuffer(kernel, "phase", controller.phaseBuffer);
            
            clothUpdateShader.Dispatch(kernel, controller.NumParticles / 128, 1, 1);

            STDUtils.Swap(controller.VelocitiesBuffer);
        }
        
        private void SolveCloth(int clothId)
        {
            var nodeUpdateComputeShaderHandle = clothUpdateShader.FindKernel("NodeUpdate");

            clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "phase", controller.phaseBuffer);
            clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "clothData", controller.cpuClothData);

            clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "Positions", controller.PositionsBuffer);
            clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "PredictedWRITE", controller.PredictedBuffer[WRITE]);
            clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "PredictedREAD", controller.PredictedBuffer[READ]);
            clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "VelocitiesWRITE", controller.VelocitiesBuffer[WRITE]);
            clothUpdateShader.SetBuffer(nodeUpdateComputeShaderHandle, "VelocitiesREAD", controller.VelocitiesBuffer[READ]);
                
            CLOTH_GROUPS_X = Mathf.FloorToInt(clothBodies[clothId].width / 8);
            CLOTH_GROUPS_Y = Mathf.FloorToInt(clothBodies[clothId].height / 8);
            for (var i = 0; i < looper; i++) {
                clothUpdateShader.Dispatch(nodeUpdateComputeShaderHandle, 
                    CLOTH_GROUPS_X, CLOTH_GROUPS_Y, CLOTH_GROUPS_Z);
            }
                
            STDUtils.Swap(controller.PredictedBuffer); 
            STDUtils.Swap(controller.VelocitiesBuffer);
        }
        
        private void UpdatePositions(int size)
        {
            var kernel = clothUpdateShader.FindKernel("UpdatePositions");

            clothUpdateShader.SetBuffer(kernel, "clothData", controller.cpuClothData);
            clothUpdateShader.SetBuffer(kernel, "Positions", controller.PositionsBuffer);
            clothUpdateShader.SetBuffer(kernel, "PredictedREAD", controller.PredictedBuffer[READ]);
            clothUpdateShader.SetBuffer(kernel, "phase", controller.phaseBuffer);
            
            clothUpdateShader.Dispatch(kernel, controller.NumParticles / 8, 1, 1);
        }
    }
}