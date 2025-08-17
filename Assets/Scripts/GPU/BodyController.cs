using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace GPU
{
    public class BodyController : IDisposable
    {
        //particle radius
        public float ParticleRadius { get; set; }
        //particle volume. treat particle as sphere to compute the volume. Used for mass computing
        public float ParticleVolume { get; set; }
        //mass
        public float ParticleMass { get; set; }



        public float ParticleDiameter => ParticleRadius * 2.0f;

        public ComputeBuffer ParticleColors { get; private set; }

        public ComputeBuffer PressuresBuffer { get; private set; }
        public ComputeBuffer DensitiesBuffer { get; private set; }

        public ComputeBuffer PositionsBuffer { get; private set; }
        //predicted positions and velocities are READ/WRITE, for swap
        public ComputeBuffer[] PredictedBuffer { get; private set; }
        public ComputeBuffer[] VelocitiesBuffer { get; private set; }

        public List<int> phaseArray = new List<int>();
        public ComputeBuffer phaseBuffer { get; private set; }

        public int NumParticles = 0;

        //argument buffer for GPU instancing
        protected ComputeBuffer m_argsBuffer = null;

        public List<Vector4> Positions = new List<Vector4>();
        public List<Vector4> Predicted = new List<Vector4>();
        public List<Vector4> Velocities = new List<Vector4>();
        public List<Vector4> Colors = new List<Vector4>();

        private Transform baseObjectTransform;
        private static readonly int Positions1 = Shader.PropertyToID("positions");
        private static readonly int Diameter = Shader.PropertyToID("Diameter");
        private static readonly int ColorsId = Shader.PropertyToID("particleColors");


        public ComputeBuffer cpuClothData;
        public List<Fluids.Cloth> cpuSideClothInfo = new List<Fluids.Cloth>();
        private static readonly int NumPart = Shader.PropertyToID("numPart");


        public BodyController(Transform transform, float particleRadius, float particleMass)
        {
            baseObjectTransform = transform;
            ParticleRadius = particleRadius;
            ParticleVolume = (4.0f / 3.0f) * Mathf.PI * Mathf.Pow(ParticleRadius, 3);
            ParticleMass = particleMass;
            cpuClothData = new ComputeBuffer(10, Marshal.SizeOf(typeof(Fluids.Cloth)));
        }

        public void AddBody(Body body, Vector4 color)
        {
            //Create new particles on the arrays
            CreateParticles(body, color);
        }


        /// <summary>
        /// Draws the mesh spheres when draw particles is enabled.
        /// </summary>
        public void Draw(Mesh mesh, Material material, MaterialPropertyBlock props, Camera cam)
        {
            if (m_argsBuffer == null)
                CreateArgBuffer(mesh.GetIndexCount(0));

            material.SetBuffer(Positions1, PositionsBuffer);
            material.SetBuffer(ColorsId, ParticleColors);
            material.SetFloat(Diameter, ParticleDiameter);
            material.SetInt(NumPart, NumParticles);

            const ShadowCastingMode castShadow = ShadowCastingMode.On;
            const bool receiveShadow = true;

            /*PositionsBuffer.GetData(data, 0, 0, PositionsBuffer.count);
            for (int i = 0; i < data.Length; i++)
            {
                var pos = data[i];
                gameObjects[i].transform.position = new Vector3(pos.x, pos.y, pos.z);
            }*/

            Graphics.DrawMeshInstancedIndirect(
                mesh, 0, material,
                new Bounds(Vector3.zero, new Vector3(20, 20, 20)),
                m_argsBuffer, 0, props, castShadow, receiveShadow);
        }

        private void CreateArgBuffer(uint indexCount)
        {
            var args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = indexCount;
            args[1] = (uint)NumParticles;

            m_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            m_argsBuffer.SetData(args);
        }

        public void InitializeBuffers()
        {
            /*data = new Vector4[Positions.Count];
            gameObjects = new GameObject[Positions.Count];
            Array.Copy(Positions.ToArray(), data, Positions.Count);
            for (int i = 0; i < data.Length; i++)
            {
                var p = data[i];
                var color = Colors[i];
                gameObjects[i] = GameObject.Instantiate(gameObject, new Vector3(p.x, p.y, p.z), Quaternion.identity);
                gameObjects[i].GetComponent<Renderer>().material.color = new Color(color.x, color.y, color.z, color.w);
            }*/

            ParticleColors = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            ParticleColors.SetData(Colors);

            phaseBuffer = new ComputeBuffer(NumParticles, sizeof(int));
            phaseBuffer.SetData(phaseArray);

            PositionsBuffer = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            PositionsBuffer.SetData(Positions);

            DensitiesBuffer = new ComputeBuffer(NumParticles, sizeof(float));
            PressuresBuffer = new ComputeBuffer(NumParticles, sizeof(float));

            //Predicted and velocities use a double buffer as solver step
            //needs to read from many locations of buffer and write the result
            //in same pass.

            PredictedBuffer = new ComputeBuffer[2];
            PredictedBuffer[0] = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            PredictedBuffer[0].SetData(Predicted);
            PredictedBuffer[1] = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            PredictedBuffer[1].SetData(Predicted);

            VelocitiesBuffer = new ComputeBuffer[2];
            VelocitiesBuffer[0] = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            VelocitiesBuffer[0].SetData(Velocities);
            VelocitiesBuffer[1] = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            VelocitiesBuffer[1].SetData(Velocities);
        }

        private void CreateParticles(Body body, Vector4 color)
        {
            body.CreateParticles(color);
        }

        public void Dispose()
        {
            if (PositionsBuffer != null)
            {
                PositionsBuffer.Release();
                PositionsBuffer = null;
            }
            ParticleColors.Release();
            STDUtils.Release(PredictedBuffer);
            STDUtils.Release(VelocitiesBuffer);
            STDUtils.Release(ref m_argsBuffer);
        }
    }
}