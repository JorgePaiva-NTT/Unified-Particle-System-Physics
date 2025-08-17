using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.CPU
{
    class CPUBodyController
    {
        static public CPUBodyController BodyController {
            get {
                if (bodyController == null) bodyController = new CPUBodyController();
                return bodyController;
            }
        }
        static private CPUBodyController bodyController;

        private List<CPUBody> bodies = new List<CPUBody>();

        public void AddBody(CPUBody body) {
            int startIndex = 0;
            //int lastStartIndex = 0;
            foreach (var b in bodies) {
                startIndex += b.StartIndex + b.Volume;
            }
            if(startIndex != 0) startIndex += 1;
            body.StartIndex = startIndex;
            bodies.Add(body);
        }

        public void SolveBodiesConstraints(
            float4[] oldPos, 
            float4[] oldVel, 
            ref float4[] newVel, 
            ref float4[] newPos
            )
        {
            foreach (var body in bodies)
            {
                body.Solve(oldPos, oldVel, ref newVel);
            }
        }
    }
}
