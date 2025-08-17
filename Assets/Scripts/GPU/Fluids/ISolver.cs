namespace GPU.Fluids
{
    public interface ISolver
    {
        void StepPhysics(float dt);
    }
}