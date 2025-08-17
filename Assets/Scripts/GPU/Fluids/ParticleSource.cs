using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.GPU.Fluids
{
    public abstract class ParticleSource
    {
        //number of the particles
        public int NumParticles => Positions.Count;

        //particle positions
        public List<Vector3> Positions { get; protected set; }
        
        //the interval(or stride, spacing), defining how the space is divided into particles
        public float Interval { get; private set; }

        public float HalfInterval => Interval * 0.5f;

        public ParticleSource(float interval)
        {
            Interval = interval;
        }

    }
}