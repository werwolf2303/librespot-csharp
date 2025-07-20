using System.Text;
using deps.jorbis.jogg;

namespace deps.jorbis.jorbis
{
    public class Info
    {
        private const int OvEbadPacket = -136;
        private const int OvENotAudio = -135;

        private static readonly byte[] VorbisBytes = Encoding.UTF8.GetBytes("vorbis");
        private const int ViTimeB = 1;
        private const int ViFloorB = 2;
        private const int ViResB = 3;
        private const int ViMapB = 1;
        private const int ViWindowB = 1;

        public int Version { get; set; }
        public int Channels { get; set; }
        public int Rate { get; set; }

        public int BitrateUpper { get; set; }
        public int BitrateNominal { get; set; }
        public int BitrateLower { get; set; }

        public int[] BlockSizes { get; set; } = new int[2];

        public int Modes { get; set; }
        public int Maps { get; set; }
        public int Times { get; set; }
        public int Floors { get; set; }
        public int Residues { get; set; }
        public int Books { get; set; }
        public int Psys { get; set; } // encode only

        public InfoMode[] ModeParam { get; set; }

        public int[] MapType { get; set; }
        public object[] MapParam { get; set; }

        public int[] TimeType { get; set; }
        public object[] TimeParam { get; set; }

        public int[] FloorType { get; set; }
        public object[] FloorParam { get; set; }

        public int[] ResidueType { get; set; }
        public object[] ResidueParam { get; set; }

        public StaticCodeBook[] BookParam { get; set; }

        public PsyInfo[] PsyParam { get; set; } = new PsyInfo[64]; // encode only

        public int EnvelopeSa { get; set; }
        public float PreechoThresh { get; set; }
        public float PreechoClamp { get; set; }

        public void Init()
        {
            Rate = 0;
        }

        public void Clear()
        {
            for (int i = 0; i < Modes; i++)
            {
                ModeParam[i] = null;
            }

            ModeParam = null;

            for (int i = 0; i < Maps; i++)
            {
                FuncMapping.MappingP[MapType[i]].FreeInfo(MapParam[i]);
            }

            MapParam = null;

            for (int i = 0; i < Times; i++)
            {
                FuncTime.TimeP[TimeType[i]].FreeInfo(TimeParam[i]);
            }

            TimeParam = null;

            for (int i = 0; i < Floors; i++)
            {
                FuncFloor.FloorP[FloorType[i]].FreeInfo(FloorParam[i]);
            }

            FloorParam = null;

            for (int i = 0; i < Residues; i++)
            {
                FuncResidue.ResidueP[ResidueType[i]].FreeInfo(ResidueParam[i]);
            }

            ResidueParam = null;

            for (int i = 0; i < Books; i++)
            {
                if (BookParam[i] != null)
                {
                    BookParam[i].Clear();
                    BookParam[i] = null;
                }
            }

            BookParam = null;

            for (int i = 0; i < Psys; i++)
            {
                PsyParam[i].Free();
            }
        }

        public int UnpackInfo(Buffer opb)
        {
            Version = opb.Read(32);
            if (Version != 0)
                return -1;

            Channels = opb.Read(8);
            Rate = opb.Read(32);

            BitrateUpper = opb.Read(32);
            BitrateNominal = opb.Read(32);
            BitrateLower = opb.Read(32);

            BlockSizes[0] = 1 << opb.Read(4);
            BlockSizes[1] = 1 << opb.Read(4);

            if ((Rate < 1) || (Channels < 1) || (BlockSizes[0] < 8) || (BlockSizes[1] < BlockSizes[0]) ||
                (opb.Read(1) != 1))
            {
                Clear();
                return -1;
            }

            return 0;
        }

        public int UnpackBooks(Buffer opb)
        {
            Books = opb.Read(8) + 1;

            if (BookParam == null || BookParam.Length != Books)
                BookParam = new StaticCodeBook[Books];

            for (int i = 0; i < Books; i++)
            {
                BookParam[i] = new StaticCodeBook();
                if (BookParam[i].Unpack(opb) != 0)
                {
                    Clear();
                    return -1;
                }
            }

            Times = opb.Read(6) + 1;
            if (TimeType == null || TimeType.Length != Times)
                TimeType = new int[Times];
            if (TimeParam == null || TimeParam.Length != Times)
                TimeParam = new object[Times];

            for (int i = 0; i < Times; i++)
            {
                TimeType[i] = opb.Read(16);
                if (TimeType[i] < 0 || TimeType[i] >= ViTimeB)
                {
                    Clear();
                    return -1;
                }

                TimeParam[i] = FuncTime.TimeP[TimeType[i]].Unpack(this, opb);
                if (TimeParam[i] == null)
                {
                    Clear();
                    return -1;
                }
            }

            Floors = opb.Read(6) + 1;
            if (FloorType == null || FloorType.Length != Floors)
                FloorType = new int[Floors];
            if (FloorParam == null || FloorParam.Length != Floors)
                FloorParam = new object[Floors];

            for (int i = 0; i < Floors; i++)
            {
                FloorType[i] = opb.Read(16);
                if (FloorType[i] < 0 || FloorType[i] >= ViFloorB)
                {
                    Clear();
                    return -1;
                }

                FloorParam[i] = FuncFloor.FloorP[FloorType[i]].Unpack(this, opb);
                if (FloorParam[i] == null)
                {
                    Clear();
                    return -1;
                }
            }

            Residues = opb.Read(6) + 1;
            if (ResidueType == null || ResidueType.Length != Residues)
                ResidueType = new int[Residues];
            if (ResidueParam == null || ResidueParam.Length != Residues)
                ResidueParam = new object[Residues];

            for (int i = 0; i < Residues; i++)
            {
                ResidueType[i] = opb.Read(16);
                if (ResidueType[i] < 0 || ResidueType[i] >= ViResB)
                {
                    Clear();
                    return -1;
                }

                ResidueParam[i] = FuncResidue.ResidueP[ResidueType[i]].Unpack(this, opb);
                if (ResidueParam[i] == null)
                {
                    Clear();
                    return -1;
                }
            }

            Maps = opb.Read(6) + 1;
            if (MapType == null || MapType.Length != Maps)
                MapType = new int[Maps];
            if (MapParam == null || MapParam.Length != Maps)
                MapParam = new object[Maps];

            for (int i = 0; i < Maps; i++)
            {
                MapType[i] = opb.Read(16);
                if (MapType[i] < 0 || MapType[i] >= ViMapB)
                {
                    Clear();
                    return -1;
                }

                MapParam[i] = FuncMapping.MappingP[MapType[i]].Unpack(this, opb);
                if (MapParam[i] == null)
                {
                    Clear();
                    return -1;
                }
            }

            Modes = opb.Read(6) + 1;
            if (ModeParam == null || ModeParam.Length != Modes)
                ModeParam = new InfoMode[Modes];

            for (int i = 0; i < Modes; i++)
            {
                ModeParam[i] = new InfoMode
                {
                    BlockFlag = opb.Read(1),
                    WindowType = opb.Read(16),
                    TransformType = opb.Read(16),
                    Mapping = opb.Read(8)
                };

                if ((ModeParam[i].WindowType >= ViWindowB) ||
                    (ModeParam[i].TransformType >= ViWindowB) ||
                    (ModeParam[i].Mapping >= Maps))
                {
                    Clear();
                    return -1;
                }
            }

            if (opb.Read(1) != 1)
            {
                Clear();
                return -1;
            }

            return 0;
        }

        public int SynthesisHeaderIn(Comment vc, Packet op)
        {
            Buffer opb = new Buffer();

            if (op != null)
            {
                opb.ReadInit(op.PacketBase, op.TPacket, op.Bytes);

                byte[] buffer = new byte[6];
                int packtype = opb.Read(8);
                opb.Read(buffer, 6);
                if (buffer[0] != 'v' || buffer[1] != 'o' || buffer[2] != 'r' || buffer[3] != 'b' || buffer[4] != 'i' ||
                    buffer[5] != 's')
                {
                    return -1;
                }

                switch (packtype)
                {
                    case 0x01:
                        if (op.BeginningOfStream == 0)
                        {
                            return -1;
                        }

                        if (Rate != 0)
                        {
                            return -1;
                        }

                        return UnpackInfo(opb);
                    case 0x03:
                        if (Rate == 0)
                        {
                            return -1;
                        }

                        return vc.Unpack(opb);
                    case 0x05:
                        if (Rate == 0 || vc.Vendor == null)
                        {
                            return -1;
                        }

                        return UnpackBooks(opb);
                    default:
                        break;
                }
            }

            return -1;
        }

        public int PackInfo(Buffer opb)
        {
            opb.Write(0x01, 8);
            opb.Write(VorbisBytes);

            opb.Write(0x00, 32);
            opb.Write(Channels, 8);
            opb.Write(Rate, 32);

            opb.Write(BitrateUpper, 32);
            opb.Write(BitrateNominal, 32);
            opb.Write(BitrateLower, 32);

            opb.Write(Util.ILog2(BlockSizes[0]), 4);
            opb.Write(Util.ILog2(BlockSizes[1]), 4);
            opb.Write(1, 1);
            return 0;
        }

        public int PackBooks(Buffer opb)
        {
            opb.Write(0x05, 8);
            opb.Write(VorbisBytes);

            opb.Write(Books - 1, 8);
            for (int i = 0; i < Books; i++)
            {
                if (BookParam[i].Pack(opb) != 0)
                {
                    return -1;
                }
            }

            opb.Write(Times - 1, 6);
            for (int i = 0; i < Times; i++)
            {
                opb.Write(TimeType[i], 16);
                FuncTime.TimeP[TimeType[i]].Pack(this.TimeParam[i], opb);
            }

            opb.Write(Floors - 1, 6);
            for (int i = 0; i < Floors; i++)
            {
                opb.Write(FloorType[i], 16);
                FuncFloor.FloorP[FloorType[i]].Pack(FloorParam[i], opb);
            }

            opb.Write(Residues - 1, 6);
            for (int i = 0; i < Residues; i++)
            {
                opb.Write(ResidueType[i], 16);
                FuncResidue.ResidueP[ResidueType[i]].Pack(ResidueParam[i], opb);
            }

            opb.Write(Maps - 1, 6);
            for (int i = 0; i < Maps; i++)
            {
                opb.Write(MapType[i], 16);
                FuncMapping.MappingP[MapType[i]].Pack(this, MapParam[i], opb);
            }

            opb.Write(Modes - 1, 6);
            for (int i = 0; i < Modes; i++)
            {
                opb.Write(ModeParam[i].BlockFlag, 1);
                opb.Write(ModeParam[i].WindowType, 16);
                opb.Write(ModeParam[i].TransformType, 16);
                opb.Write(ModeParam[i].Mapping, 8);
            }

            opb.Write(1, 1);
            return 0;
        }

        public int Blocksize(Packet op)
        {
            Buffer opb = new Buffer();
            opb.ReadInit(op.PacketBase, op.TPacket, op.Bytes);

            if (opb.Read(1) != 0)
            {
                return OvENotAudio;
            }

            int modebits = 0;
            int v = Modes;
            while (v > 1)
            {
                modebits++;
                v = (int)((uint)v >> 1);
            }

            int mode = opb.Read(modebits);
            if (mode == -1)
                return OvEbadPacket;

            return BlockSizes[ModeParam[mode].BlockFlag];
        }

        public override string ToString()
        {
            return
                $"version:{Version}, channels:{Channels}, rate:{Rate}, bitrate:{BitrateUpper},{BitrateNominal},{BitrateLower}";
        }
    }
}