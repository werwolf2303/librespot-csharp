namespace player.crossfade
{
    public interface IGainInterpolator
    {
        float Interpolate(float x);

        float Last();
    }
}