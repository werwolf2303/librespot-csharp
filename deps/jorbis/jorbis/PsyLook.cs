namespace deps.jorbis.jorbis
{
    public class PsyLook
    {
        public int N { get; set; }
        public PsyInfo Vi { get; set; }
        public float[][][] ToneCurves { get; set; }
        public float[][] PeakAtt { get; set; }
        public float[][][] NoiseCurves { get; set; }
        public float[] Ath { get; set; }
        public int[] Octave { get; set; }

        public void Init(PsyInfo vi, int n, int rate)
        {
        }
    }
}