namespace Assets.Scripts.CPU
{
    abstract class CPUBody
    {
        public int StartIndex { get; set; }
        public int Width { get;  protected set; }
        public int Height { get; protected set; }
        public int Depth { get;  protected set; }
        public int Volume { get { return Width * Height * Depth; } }
        public abstract void Solve(float4[] oldPos, float4[] oldVel, ref float4[] newVel);
    }
}
