using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GPU;
using UnityEngine;
using Cloth = GPU.Fluids.Cloth;
using Random = System.Random;

namespace Assets.Scripts.GPU.Fluids
{
    public class ClothBody : Body
    {
        private ComputeBuffer Objects;

        public float springK = 10000f;
        public float damping = 120f;
        public Vector3 externalForce;

        public uint width = 0;
        public uint height = 0;
        
        public ClothBody(
            BodyController controller, ParticleSource source, float density, Matrix4x4 rts, Vector3 initialVel) 
            : base(controller, source, density, rts, initialVel)
        {
            width = 8*2;
            height = 8*2;
        }
        
        
        public override void CreateParticles(Vector4 color)
        {
            uint vertexRow = height;
            uint vertexColumn = width;
            
            const float inf = float.PositiveInfinity;
            var min = new Vector3(inf, inf, inf);
            var max = new Vector3(-inf, -inf, -inf);
            
            var dx = bounds.size.x / (vertexRow - 1);
            var dy = bounds.size.y / (vertexColumn - 1);
            var structuralConstraint = Math.Max(dx, dy);
            var shearConstraint = (float)Math.Sqrt(dx * dx + dy * dx);
            int i, j;

            var startingIndex = controller.Positions.Count;

            int clothParticleNum = 0;
            
            Vector4 offset = new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, 1);
            for (i = 0; i < vertexRow; i++)
            {
                for (j = 0; j < vertexColumn; j++)
                {
                    var pos =  offset + new Vector4(
                                  dx * j - bounds.size.x / 2,
                                  0.0f,
                                  dy * i - bounds.size.y / 2);
                    clothParticleNum++;
                    controller.Positions.Add(pos);
                    controller.Predicted.Add(pos);
                    //TODO: Set cloth initial velocity
                    controller.Velocities.Add(new Vector4(0, 0, 0));

                    controller.phaseArray.Add(id);
                    controller.Colors.Add(color);

                    if (pos.x < min.x) min.x = pos.x;
                    if (pos.y < min.y) min.y = pos.y;
                    if (pos.z < min.z) min.z = pos.z;

                    if (pos.x > max.x) max.x = pos.x;
                    if (pos.y > max.y) max.y = pos.y;
                    if (pos.z > max.z) max.z = pos.z;
                }
            }

            var endIndex = controller.Positions.Count - 1;
            
            min.x -= controller.ParticleRadius;
            min.y -= controller.ParticleRadius;
            min.z -= controller.ParticleRadius;

            max.x += controller.ParticleRadius;
            max.y += controller.ParticleRadius;
            max.z += controller.ParticleRadius;

            bounds = new Bounds();
            bounds.SetMinMax(min, max);
            
            controller.cpuSideClothInfo.Add(new Cloth {
                startIndex = startingIndex,
                endIndex = endIndex,
                
                bodyId = id,
                
                RestLengthDiag = Mathf.Sqrt(Mathf.Pow(dx, 2) + Mathf.Pow(dy, 2)),
                RestLengthHoriz = dx,
                RestLengthVert = dy,

                vertexColumn = vertexColumn,
                vertexRow = vertexRow,

                shearConstraint = shearConstraint,
                structualConstraint = structuralConstraint,
                shearConstraint1 = shearConstraint * 10.1f,
                structualConstraint1 = structuralConstraint * 10.1f
            });

            controller.cpuClothData.SetData(controller.cpuSideClothInfo);
            controller.NumParticles += clothParticleNum;
        }
    }
}