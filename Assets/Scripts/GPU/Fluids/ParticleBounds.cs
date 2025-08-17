using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.GPU.Fluids
{
    public class ParticlesFromBounds : ParticleSource
    {
        //the outmost bound
        public Bounds Bounds { get; private set; }
        //for fluid body, need to create particles for space in the middle
        //but for boundaries not
        //the fluid boundary is defined by at least two Bounds: inner and outer
        public List<Bounds> BoundsForExclusion { get; private set; }

        public ParticlesFromBounds(float Interval, Bounds bounds, bool isFluidContainer) : base(Interval)
        {
            Bounds = bounds;
            BoundsForExclusion = new List<Bounds>();
            CreateParticles(isFluidContainer);
        }

        public ParticlesFromBounds(float Interval, Bounds bounds, Bounds boundsForExclusion, bool isFluidContainer) : base(Interval)
        {
            Bounds = bounds;
            BoundsForExclusion = new List<Bounds> {boundsForExclusion};
            CreateParticles(isFluidContainer);
        }

        private void CreateParticles(bool boundries = false)
        {

            var numX = (int)((Bounds.size.x + HalfInterval) / Interval);
            var numY = (int)((Bounds.size.y + HalfInterval) / Interval);
            var numZ = (int)((Bounds.size.z + HalfInterval) / Interval);

            if (numZ == 0)
                numZ = 1;
            
            Positions = new List<Vector3>();

            for (var z = 0; z < numZ; z++)
            {
                for (var y = 0; y < numY; y++)
                {
                    for (var x = 0; x < numX; x++)
                    {
                        var pos = new Vector3 {
                            x = Interval * x + Bounds.min.x + HalfInterval,
                            y = Interval * y + Bounds.min.y + HalfInterval,
                            z = Interval * z + Bounds.min.z + HalfInterval
                        };

                        if (boundries && y >= 10)
                            continue;

                        var exclude = false;
                        for (var i = 0; i < BoundsForExclusion.Count; i++)
                        {
                            if (BoundsForExclusion[i].Contains(pos))
                            {
                                exclude = true;
                                break;
                            }
                        }

                        if (!exclude)
                            Positions.Add(pos);
                    }
                }
            }

        }

    }
}