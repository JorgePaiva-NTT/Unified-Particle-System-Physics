using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.CPU
{
    class CPUClothConstraint
    {
        private readonly float restDistance;
        private readonly float squaredRestDistace;

        public int index1, index2;
        public CPUClothConstraint(int id1, int id2, float3 one, float3 two) {
            float3 delta = float3.Make_float3(one.x - two.x,
                               one.y - two.y,
                               one.z - two.z);
            restDistance = Utils.Length(delta);

            squaredRestDistace = restDistance * restDistance;//(delta.x * delta.x) + (delta.y * delta.y) + (delta.z * delta.z);
            index1 = id1;
            index2 = id2;
        }

        public float stiffness = 30.0f;
        public float damping = 1.0f;
        public float tanVelPen = 0.002f;

        public void SetParameters(float ks, float kd, float tan) {
            this.stiffness = ks;
            this.damping = kd;
            this.tanVelPen = tan;
        }

        public void SatisfyConstraint(float4[] pos, float4[] vel, ref float4[] velocity) {

            var p1 = pos[index1].xyz;
            var p2 = pos[index2].xyz;
            var v1 = vel[index1].xyz;
            var v2 = vel[index2].xyz;

            var delta = p2 - p1;
            var deltaVel = v2 - v1;
            
            var currentDist = Utils.Length(delta);

            var invMass1 = vel[index1].w;
            var invMass2 = vel[index2].w;
            var squaredDist = currentDist * currentDist;//(delta.x * delta.x) + (delta.y * delta.y) + (delta.z * delta.z);

            var leftTerm = stiffness * (squaredDist - squaredRestDistace);
            var rightTerm = damping * ( (squaredRestDistace + squaredDist) * (invMass1 + invMass2) );
            var diff =  ( leftTerm / rightTerm );

            var norm = delta / currentDist;
            float3 tanVel = deltaVel - (Utils.Dot(deltaVel, norm) * norm);

            float3 accumulated1 = float3.Make_float3(delta * (invMass1 * diff));
            accumulated1 += tanVel * tanVelPen;

            float3 accumulated2 = float3.Make_float3(delta * (invMass2 * diff));
            accumulated2 += tanVel * tanVelPen;

            velocity[index1] += float4.Make_float4(accumulated1, 0.0f);
            velocity[index2] -= float4.Make_float4(accumulated2, 0.0f);
        }
    }
}
