using GPU;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.GPU.Fluids
{

    public class FluidBody : Body
    {
        //Viscosity constant
        public float ViscosityCoeff { get; set; } = 0.0005f;
        //velocity damping coefficient
        public float DampingCoeff { get; set; } = 0.01f;

        public FluidBody( BodyController controller, ParticleSource source, float density, Matrix4x4 rts, Vector3 initialVel)
        : base(controller, source, density, rts, initialVel)
        {
            ViscosityCoeff = 0.0005f;
            DampingCoeff = 0.01f;
        }

        public override void CreateParticles(Vector4 color)
        {
            const float inf = float.PositiveInfinity;
            var min = new Vector3(inf, inf, inf);
            var max = new Vector3(-inf, -inf, -inf);

            startParticleId = (uint) (controller.Positions.Count - 1);
            
            for (var i = 0; i < source.NumParticles; i++)
            {
                var pos = rts * source.Positions[i];
                controller.Positions.Add(pos);
                controller.Predicted.Add(pos);

                controller.Colors.Add(color);

                //modified: add initial velocity to the particles
                //velocities[i] = new Vector3(5.0f, 0.0f, 0.0f);
                controller.Velocities.Add(initialVel);

                if (pos.x < min.x) min.x = pos.x;
                if (pos.y < min.y) min.y = pos.y;
                if (pos.z < min.z) min.z = pos.z;

                if (pos.x > max.x) max.x = pos.x;
                if (pos.y > max.y) max.y = pos.y;
                if (pos.z > max.z) max.z = pos.z;

                controller.phaseArray.Add(id);
            }

            endParticleId = (uint) (controller.Positions.Count - 1);
            
            min.x -= controller.ParticleRadius;
            min.y -= controller.ParticleRadius;
            min.z -= controller.ParticleRadius;

            max.x += controller.ParticleRadius;
            max.y += controller.ParticleRadius;
            max.z += controller.ParticleRadius;

            bounds = new Bounds();
            bounds.SetMinMax(min, max);

            controller.NumParticles += source.NumParticles;
        }
        
        
        public void Dispose()
        {
        }
    }
    
    //Compute shader data structure
}