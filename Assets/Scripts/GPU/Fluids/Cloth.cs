namespace GPU.Fluids
{
    public struct Cloth {
        public uint bodyId;
        
        public int startIndex;
        public int endIndex;

        public float RestLengthHoriz;
        public float RestLengthVert;
        public float RestLengthDiag;
    
        public uint vertexColumn;
        public uint vertexRow;

        public float structualConstraint;
        public float shearConstraint;
        public float structualConstraint1;
        public float shearConstraint1;
    }
}