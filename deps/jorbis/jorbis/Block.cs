using System;
using deps.jorbis.jogg;
using Buffer = deps.jorbis.jogg.Buffer;

namespace deps.jorbis.jorbis
{
    public class Block
    {
        public readonly Buffer Opb = new Buffer();
        public DspState Vd;

        public float[][] Pcm { get; set; } = new float[0][];
        public int LW { get; set; }
        public int W { get; set; }
        public int NW { get; set; }
        public int PcmEnd { get; set; }
        public int Mode { get; set; }
        public int EofFlag { get; set; }
        public long GranulePos { get; set; }
        public long Sequence { get; set; }
        public int GlueBits { get; set; }
        public int TimeBits { get; set; }
        public int FloorBits { get; set; }
        public int ResBits { get; set; }

        public Block(DspState vd)
        {
            Vd = vd;
            if (vd.AnalysisP != 0)
            {
                Opb.WriteInit();
            }
        }

        public void Init(DspState vd)
        {
            Vd = vd;
        }

        public int Clear()
        {
            if (Vd != null)
            {
                if (Vd?.AnalysisP != 0)
                {
                    Opb.WriteClear();
                }
            }
            return 0;
        }

        public int Synthesis(Packet op)
        {
            Info vi = Vd.Vi;

            Opb.ReadInit(op.PacketBase, op.TPacket, op.Bytes);

            if (Opb.Read(1) != 0)
            {
                return -1;
            }
            
            int modeValue = Opb.Read(Vd.ModeBits);
            if (modeValue == -1)
                return -1;

            Mode = modeValue;
            W = vi.ModeParam[Mode].BlockFlag;
            if (W != 0)
            {
                LW = Opb.Read(1);
                NW = Opb.Read(1);
                if (NW == -1)
                    return -1;
            }
            else
            {
                LW = 0;
                NW = 0;
            }

            GranulePos = op.GranulePos;
            Sequence = op.PacketNo - 3;
            EofFlag = op.EndOfStream;

            PcmEnd = vi.BlockSizes[W];
            if (Pcm.Length < vi.Channels)
            {
                Pcm = new float[vi.Channels][];
            }

            for (int i = 0; i < vi.Channels; i++)
            {
                if (Pcm[i] == null || Pcm[i].Length < PcmEnd)
                {
                    Pcm[i] = new float[PcmEnd];
                }
                else
                {
                    for (int j = 0; j < PcmEnd; j++)
                    {
                        Pcm[i][j] = 0;
                    }
                }
            }

            int type = vi.MapType[vi.ModeParam[Mode].Mapping];
            return FuncMapping.MappingP[type].Inverse(this, Vd.Mode[Mode]);
        }
    }
}