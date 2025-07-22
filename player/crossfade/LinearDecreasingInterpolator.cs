namespace player.crossfade
{
    public class LinearDecreasingInterpolator : IGainInterpolator
    {
        public float Interpolate(float x)
        {
            return 1 - x;
        }

        public float Last()
        {
            return 0;
        }
    }
}