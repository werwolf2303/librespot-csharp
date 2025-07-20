namespace deps.jorbis.jorbis
{
    public class PsyInfo
    {
        public int Athp { get; set; }
        public int Decayp { get; set; }
        public int Smoothp { get; set; }
        public int NoiseFitp { get; set; }
        public int NoiseFitSubBlock { get; set; }
        public float NoiseFitThreshdB { get; set; }

        public float AthAtt { get; set; }

        public int ToneMaskp { get; set; }
        public float[] ToneAtt125Hz { get; set; } = new float[5];
        public float[] ToneAtt250Hz { get; set; } = new float[5];
        public float[] ToneAtt500Hz { get; set; } = new float[5];
        public float[] ToneAtt1000Hz { get; set; } = new float[5];
        public float[] ToneAtt2000Hz { get; set; } = new float[5];
        public float[] ToneAtt4000Hz { get; set; } = new float[5];
        public float[] ToneAtt8000Hz { get; set; } = new float[5];

        public int PeakAttp { get; set; }
        public float[] PeakAtt125Hz { get; set; } = new float[5];
        public float[] PeakAtt250Hz { get; set; } = new float[5];
        public float[] PeakAtt500Hz { get; set; } = new float[5];
        public float[] PeakAtt1000Hz { get; set; } = new float[5];
        public float[] PeakAtt2000Hz { get; set; } = new float[5];
        public float[] PeakAtt4000Hz { get; set; } = new float[5];
        public float[] PeakAtt8000Hz { get; set; } = new float[5];

        public int NoiseMaskp { get; set; }
        public float[] NoiseAtt125Hz { get; set; } = new float[5];
        public float[] NoiseAtt250Hz { get; set; } = new float[5];
        public float[] NoiseAtt500Hz { get; set; } = new float[5];
        public float[] NoiseAtt1000Hz { get; set; } = new float[5];
        public float[] NoiseAtt2000Hz { get; set; } = new float[5];
        public float[] NoiseAtt4000Hz { get; set; } = new float[5];
        public float[] NoiseAtt8000Hz { get; set; } = new float[5];

        public float MaxCurveDb { get; set; }

        public float AttackCoeff { get; set; }
        public float DecayCoeff { get; set; }

        public void Free()
        {
        }
    }
}