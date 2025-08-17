using System.Collections.Generic;

namespace Assets.Scripts.CPU
{
    class CPUCloth : CPUBody
    {        
        private List<CPUClothConstraint> constraints;

        public CPUCloth(int height, int width) {
            Width = width;
            Height = height;
            Depth = 1;
            constraints = new List<CPUClothConstraint>();
        }
        
        public void SetPositions(float offsetX, float offsetY, float offsetZ,
                                 ref float4[] oldPosition, 
                                 ref float4[] oldVelocity, 
                                 ref float4[] newPosition, 
                                 ref float4[] newVelocity,
                                 float particleRadius,
                                 int numParticles,
                                 bool[] particlesToFreeze,
                                 ref int[] isBody) {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {

                    if (y * Width + x > numParticles)
                        return;
                    int index = y * Width + x;

                    oldPosition[StartIndex + index] = float4.Make_float4(
                        0.7f * (x / (float)Width) + offsetX,
                        offsetY,
                        -0.7f * (y / (float)Height) + offsetZ,
                        particleRadius);
                    float mass = 0.5f;
                    if (particlesToFreeze[0] && x == 0         && y == 0) mass = 0.0f;
                    if (particlesToFreeze[1] && x == Width - 1 && y == 0) mass = 0.0f;
                    if (particlesToFreeze[2] && x == 0         && y == Height - 1) mass = 0.0f;
                    if (particlesToFreeze[3] && x == Width - 1 && y == Height - 1) mass = 0.0f;
                    oldVelocity[StartIndex + index] = float4.Make_float4(0.0f, 0.0f, 0.0f, mass);
                    newPosition[StartIndex + index] = oldPosition[StartIndex + index];
                    newVelocity[StartIndex + index] = float4.Make_float4(0.0f, 0.0f, 0.0f, mass);
                    isBody[StartIndex + index] = 1;
                }
            }
        }

        public void SetConstraints(float4[] oldPositions) {
            for (int x = 0; x < Width; x++) {
                for (int y = 0; y < Height; y++) {
                    if (x < Width - 1) MakeConstraint(oldPositions, y * Width + x, y * Width + (x + 1));
                    if (y < Height - 1) MakeConstraint(oldPositions, y * Width + x, (y + 1) * Width + x);
                    if (x < Width - 1 && y < Height - 1) MakeConstraint(oldPositions, y * Width + x, (y + 1) * Width + (x + 1));
                    if (x < Width - 1 && y < Height - 1) MakeConstraint(oldPositions, y * Width + (x + 1), (y + 1) * Width + x);
                    if (x > Width + 1 && y < 0) MakeConstraint(oldPositions, y * Width + (x + 1), (y - 1) * Width + x);

                    if (x < Width - 2) MakeConstraint(oldPositions, y * Width + x, y * Width + (x + 2));
                    if (y < Height - 2) MakeConstraint(oldPositions, y * Width + x, (y + 2) * Width + x);
                    if (x < Width - 2 && y < Height - 2) MakeConstraint(oldPositions, y * Width + x, (y + 2) * Width + (x + 2));
                    if (x < Width - 2 && y < Height - 2) MakeConstraint(oldPositions, y * Width + (x + 2), (y + 2) * Width + x);
                    if (x < Width - 2 && y < 0) MakeConstraint(oldPositions, y * Width + x, (y - 2) * Width + (x + 2));
                }
            }
        }

        private void MakeConstraint(float4[] oldPosition, int one, int two) {
            constraints.Add(new CPUClothConstraint(StartIndex + one, StartIndex + two, 
                oldPosition[StartIndex + one].xyz, oldPosition[StartIndex + two].xyz));
        }

        public override void Solve(float4[] oldPos, float4[] oldVel, ref float4[] newVel)
        {
            foreach (var constraint in constraints) {
                constraint.SatisfyConstraint(oldPos, oldVel, ref newVel);
            }
        }
    }
}
