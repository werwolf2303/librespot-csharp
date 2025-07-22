namespace player.crossfade
{
    public class LinearIncreasingInterpolator : IGainInterpolator
    {
        public float Interpolate(float x)
        {
            return x;
        }

        public float Last()
        {
            return 1;
        }
    }
}