using System;
using Buffer = deps.jorbis.jogg.Buffer;

namespace deps.jorbis.jorbis
{
    public class Mapping0 : FuncMapping
    {
        private float[][] _pcmBundle;
        private int[] _zeroBundle;
        private int[] _nonZero;
        private object[] _floorMemo;
        private readonly object _inverseLock = new object();

        public override void FreeInfo(object imap)
        {
        }

        public override void FreeLook(object imap)
        {
        }

        public override object Look(DspState vd, InfoMode vm, object m)
        {
            Info vi = vd.Vi;
            LookMapping0 look = new LookMapping0();
            InfoMapping0 info = look.Map = (InfoMapping0)m;
            look.Mode = vm;

            look.TimeLook = new object[info.Submaps];
            look.FloorLook = new object[info.Submaps];
            look.ResidueLook = new object[info.Submaps];

            look.TimeFunc = new FuncTime[info.Submaps];
            look.FloorFunc = new FuncFloor[info.Submaps];
            look.ResidueFunc = new FuncResidue[info.Submaps];

            for (int i = 0; i < info.Submaps; i++)
            {
                int timenum = info.TimeSubmap[i];
                int floornum = info.FloorSubmap[i];
                int resnum = info.ResidueSubmap[i];

                look.TimeFunc[i] = FuncTime.TimeP[vi.TimeType[timenum]];
                look.TimeLook[i] = look.TimeFunc[i].Look(vd, vm, vi.TimeParam[timenum]);
                look.FloorFunc[i] = FuncFloor.FloorP[vi.FloorType[floornum]];
                look.FloorLook[i] = look.FloorFunc[i].Look(vd, vm, vi.FloorParam[floornum]);
                look.ResidueFunc[i] = FuncResidue.ResidueP[vi.ResidueType[resnum]];
                look.ResidueLook[i] = look.ResidueFunc[i].Look(vd, vm, vi.ResidueParam[resnum]);
            }

            look.Ch = vi.Channels;
            return look;
        }

        public override void Pack(Info vi, object imap, Buffer opb)
        {
            InfoMapping0 info = (InfoMapping0)imap;

            if (info.Submaps > 1)
            {
                opb.Write(1, 1);
                opb.Write(info.Submaps - 1, 4);
            }
            else
            {
                opb.Write(0, 1);
            }

            if (info.CouplingSteps > 0)
            {
                opb.Write(1, 1);
                opb.Write(info.CouplingSteps - 1, 8);
                for (int i = 0; i < info.CouplingSteps; i++)
                {
                    opb.Write(info.CouplingMag[i], Util.ILog2(vi.Channels));
                    opb.Write(info.CouplingAng[i], Util.ILog2(vi.Channels));
                }
            }
            else
            {
                opb.Write(0, 1);
            }

            opb.Write(0, 2); // reserved

            if (info.Submaps > 1)
            {
                for (int i = 0; i < vi.Channels; i++)
                    opb.Write(info.ChMuxList[i], 4);
            }

            for (int i = 0; i < info.Submaps; i++)
            {
                opb.Write(info.TimeSubmap[i], 8);
                opb.Write(info.FloorSubmap[i], 8);
                opb.Write(info.ResidueSubmap[i], 8);
            }
        }

        public override object Unpack(Info vi, Buffer opb)
        {
            InfoMapping0 info = new InfoMapping0();

            if (opb.Read(1) != 0)
            {
                info.Submaps = opb.Read(4) + 1;
            }
            else
            {
                info.Submaps = 1;
            }

            if (opb.Read(1) != 0)
            {
                info.CouplingSteps = opb.Read(8) + 1;
                for (int i = 0; i < info.CouplingSteps; i++)
                {
                    int testM = info.CouplingMag[i] = opb.Read(Util.ILog2(vi.Channels));
                    int testA = info.CouplingAng[i] = opb.Read(Util.ILog2(vi.Channels));
                    if (testM < 0 || testA < 0 || testM == testA || testM >= vi.Channels || testA >= vi.Channels)
                    {
                        info.Free();
                        return null;
                    }
                }
            }

            if (opb.Read(2) > 0)
            {
                info.Free();
                return null;
            }

            if (info.Submaps > 1)
            {
                for (int i = 0; i < vi.Channels; i++)
                {
                    info.ChMuxList[i] = opb.Read(4);
                    if (info.ChMuxList[i] >= info.Submaps)
                    {
                        info.Free();
                        return null;
                    }
                }
            }

            for (int i = 0; i < info.Submaps; i++)
            {
                info.TimeSubmap[i] = opb.Read(8);
                if (info.TimeSubmap[i] >= vi.Times)
                {
                    info.Free();
                    return null;
                }

                info.FloorSubmap[i] = opb.Read(8);
                if (info.FloorSubmap[i] >= vi.Floors)
                {
                    info.Free();
                    return null;
                }

                info.ResidueSubmap[i] = opb.Read(8);
                if (info.ResidueSubmap[i] >= vi.Residues)
                {
                    info.Free();
                    return null;
                }
            }

            return info;
        }

        public override int Inverse(Block vb, object l)
        {
            lock (_inverseLock)
            {
                DspState vd = vb.Vd;
                Info vi = vd.Vi;
                LookMapping0 look = (LookMapping0)l;
                InfoMapping0 info = look.Map;
                InfoMode mode = look.Mode;
                int n = vb.PcmEnd = vi.BlockSizes[vb.W];
                float[] window = vd.Window[vb.W][vb.LW][vb.NW][mode.WindowType];

                if (_pcmBundle == null || _pcmBundle.Length < vi.Channels)
                {
                    _pcmBundle = new float[vi.Channels][];
                    _nonZero = new int[vi.Channels];
                    _zeroBundle = new int[vi.Channels];
                    _floorMemo = new object[vi.Channels];
                }

                for (int i = 0; i < vi.Channels; i++)
                {
                    float[] pcm = vb.Pcm[i];
                    int submap = info.ChMuxList[i];
                    _floorMemo[i] = look.FloorFunc[submap].Inverse1(vb, look.FloorLook[submap], _floorMemo[i]);
                    _nonZero[i] = _floorMemo[i] != null ? 1 : 0;
                    Array.Clear(pcm, 0, n / 2);
                }

                for (int i = 0; i < info.CouplingSteps; i++)
                {
                    if (_nonZero[info.CouplingMag[i]] != 0 || _nonZero[info.CouplingAng[i]] != 0)
                    {
                        _nonZero[info.CouplingMag[i]] = 1;
                        _nonZero[info.CouplingAng[i]] = 1;
                    }
                }

                for (int i = 0; i < info.Submaps; i++)
                {
                    int chInBundle = 0;
                    for (int j = 0; j < vi.Channels; j++)
                    {
                        if (info.ChMuxList[j] == i)
                        {
                            _zeroBundle[chInBundle] = _nonZero[j] != 0 ? 1 : 0;
                            _pcmBundle[chInBundle++] = vb.Pcm[j];
                        }
                    }

                    look.ResidueFunc[i].Inverse(vb, look.ResidueLook[i], _pcmBundle, _zeroBundle, chInBundle);
                }

                for (int i = info.CouplingSteps - 1; i >= 0; i--)
                {
                    float[] pcmM = vb.Pcm[info.CouplingMag[i]];
                    float[] pcmA = vb.Pcm[info.CouplingAng[i]];
                    for (int j = 0; j < n / 2; j++)
                    {
                        float mag = pcmM[j];
                        float ang = pcmA[j];
                        if (mag > 0)
                        {
                            if (ang > 0)
                            {
                                pcmM[j] = mag;
                                pcmA[j] = mag - ang;
                            }
                            else
                            {
                                pcmA[j] = mag;
                                pcmM[j] = mag + ang;
                            }
                        }
                        else
                        {
                            if (ang > 0)
                            {
                                pcmM[j] = mag;
                                pcmA[j] = mag + ang;
                            }
                            else
                            {
                                pcmA[j] = mag;
                                pcmM[j] = mag - ang;
                            }
                        }
                    }
                }

                for (int i = 0; i < vi.Channels; i++)
                {
                    float[] pcm = vb.Pcm[i];
                    int submap = info.ChMuxList[i];
                    look.FloorFunc[submap].Inverse2(vb, look.FloorLook[submap], _floorMemo[i], pcm);
                }

                for (int i = 0; i < vi.Channels; i++)
                {
                    float[] pcm = vb.Pcm[i];
                    ((Mdct)vd.Transform[vb.W][0]).Backward(pcm, pcm);
                }

                for (int i = 0; i < vi.Channels; i++)
                {
                    float[] pcm = vb.Pcm[i];
                    if (_nonZero[i] != 0)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            pcm[j] *= window[j];
                        }
                    }
                    else
                    {
                        Array.Clear(pcm, 0, n);
                    }
                }

                return 0;
            }
        }

        public class InfoMapping0
        {
            public int Submaps { get; set; }
            public int[] ChMuxList { get; set; } = new int[256];
            public int[] TimeSubmap { get; set; } = new int[16];
            public int[] FloorSubmap { get; set; } = new int[16];
            public int[] ResidueSubmap { get; set; } = new int[16];
            public int[] PsySubmap { get; set; } = new int[16];
            public int CouplingSteps { get; set; }
            public int[] CouplingMag { get; set; } = new int[256];
            public int[] CouplingAng { get; set; } = new int[256];

            public void Free()
            {
                ChMuxList = null;
                TimeSubmap = null;
                FloorSubmap = null;
                ResidueSubmap = null;
                PsySubmap = null;
                CouplingMag = null;
                CouplingAng = null;
            }
        }

        public class LookMapping0
        {
            public InfoMode Mode { get; set; }
            public InfoMapping0 Map { get; set; }
            public object[] TimeLook { get; set; }
            public object[] FloorLook { get; set; }
            public object[] FloorState { get; set; }
            public object[] ResidueLook { get; set; }
            public PsyLook[] PsyLook { get; set; }
            public FuncTime[] TimeFunc { get; set; }
            public FuncFloor[] FloorFunc { get; set; }
            public FuncResidue[] ResidueFunc { get; set; }
            public int Ch { get; set; }
            public float[][] Decay { get; set; }
            public int LastFrame { get; set; }
        }
    }
}