using System;
using Assets.Scripts.GPU.Fluids;
using GPU.Fluids;
using UnityEngine;

namespace GPU
{
    public class Body
    {
        //Body id from 0 to 255
        //Stores info about what Compute shader to use
        public byte id = 0;

        public uint startParticleId = 0;
        public uint endParticleId = 0;
        public ParticleSource source;
        public Matrix4x4 rts;
        public Vector3 initialVel;
        public float density;

        public Bounds bounds;

        protected BodyController controller;

        public Body(BodyController controller, ParticleSource source, float density, Matrix4x4 rts, Vector3 initialVel)
        {
            this.controller = controller;
            this.source = source;
            this.density = density;
            this.rts = rts;
            this.initialVel = initialVel;
        }

        public virtual void CreateParticles(Vector4 color)
        { }
    }
}