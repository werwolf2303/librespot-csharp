using System;
using System.IO;
using System.Runtime.CompilerServices;
using decoder_api;
using deps.jorbis.jogg;
using deps.jorbis.jorbis;
using sink_api;

namespace lib.audio.decoders
{
    public class VorbisDecoder : Decoder
    {
        private static int _convertedBufferSize = BUFFER_SIZE;
        private StreamState _joggStreamState = new StreamState();
        private DspState _jorbisDspState = new DspState();
        private Block _jorbisBlock; 
        private Comment _jorbisComment = new Comment();
        private Info _jorbisInfo = new Info();
        private Packet _joggPacket = new Packet();
        private Page _joggPage = new Page();
        private SyncState _joggSyncState = new SyncState();
        private Object _readLock = new Object();
        private byte[] _convertedBuffer;
        private float[][][] _pcmInfo;
        private int[] _pcmIndex;
        private byte[] _buffer;
        private int _count;
        private int _index;
        private long _pcmOffset;

        public VorbisDecoder(Stream audioIn, float normalizationFactor, int duration) : base(audioIn, normalizationFactor, duration)
        {
            _jorbisBlock = new Block(_jorbisDspState);
            
            _joggSyncState.Init();
            _joggSyncState.GetBuffer(BUFFER_SIZE);
            _buffer = _joggSyncState.Data;

            ReadHeader();
            seekZero = audioIn.Position;

            _convertedBuffer = new byte[_convertedBufferSize];

            _jorbisDspState.SynthesisInit(_jorbisInfo);
            _jorbisBlock.Init(_jorbisDspState);

            _pcmInfo = new float[1][][];
            _pcmIndex = new int[_jorbisInfo.Channels];
            
            SetAudioFormat(new OutputAudioFormat(_jorbisInfo.Rate, 16, _jorbisInfo.Channels, true, false));
        }

        public override int Time()
        {
            return (int)(((float)_pcmOffset / (float) _jorbisInfo.Rate) * 1000f);
        }

        private void ReadHeader()
        {
            bool finished = false;
            int packet = 1;

            while (!finished) {
                _count = audioIn.Read(_buffer, _index, Decoder.BUFFER_SIZE);
                _joggSyncState.Wrote(_count);

                int result = _joggSyncState.PageOut(_joggPage);
                if (result == -1) {
                    throw new HoleInDataException();
                } else if (result == 0) {
                    // Read more
                } else if (result == 1) {
                    if (packet == 1) {
                        _joggStreamState.Init(_joggPage.Serialno());
                        _joggStreamState.Reset();

                        _jorbisInfo.Init();
                        _jorbisComment.Init();
                    }

                    if (_joggStreamState.PageIn(_joggPage) == -1)
                        throw new DecoderException("Failed reading page");

                    if (_joggStreamState.PacketOut(_joggPacket) == -1)
                        throw new HoleInDataException();
                    
                    if (_jorbisInfo.SynthesisHeaderIn(_jorbisComment, _joggPacket) < 0)
                        throw new NotVorbisException();

                    if (packet == 3) finished = true;
                    else packet++;
                }

                _index = _joggSyncState.GetBuffer(BUFFER_SIZE);
                _buffer = _joggSyncState.Data;

                if (_count == 0 && !finished)
                    throw new DecoderException("Buffer under-run");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        protected override int ReadInternal(Stream outStream)
        {
            if (closed) return -1;

            int written = 0;
            int result = _joggSyncState.PageOut(_joggPage);
            if (result == -1 || result == 0)
            {
                // Read more
            } else if (result == 1)
            {
                if (_joggStreamState.PageIn(_joggPage) == -1)
                    throw new DecoderException("Failed reading page");

                if (_joggPage.GranulePos() == 0)
                    return -1;

                while (true)
                {
                    lock (_readLock)
                    {
                        if (closed) return written;

                        result = _joggStreamState.PacketOut(_joggPacket);
                        if (result == -1 || result == 0)
                        {
                            break;
                        } else if (result == 1)
                        {
                            written += DecodeCurrentPacket(outStream);
                        }
                    }
                }

                if (_joggPage.Eos() != 0)
                    return -1;
            }
            
            _index = _joggSyncState.GetBuffer(BUFFER_SIZE);
            _buffer = _joggSyncState.Data;
            if (_index == -1) return -1;
            
            _count = audioIn.Read(_buffer, _index, Decoder.BUFFER_SIZE);
            _joggSyncState.Wrote(_count);
            if (_count == 0) return -1;
            
            return written;
        }

        private int DecodeCurrentPacket(Stream outStream)
        {
            if (_jorbisBlock.Synthesis(_joggPacket) == 0)
                _jorbisDspState.SynthesisBlockIn(_jorbisBlock);

            int written = 0;
            int range;
            int samples;
            while ((samples = _jorbisDspState.SynthesisPcmOut(_pcmInfo, _pcmIndex)) > 0)
            {
                range = Math.Min(samples, _convertedBufferSize);

                for (int i = 0; i < _jorbisInfo.Channels; i++)
                {
                    int sampleIndex = i * 2;
                    for (int j = 0; j < range; j++)
                    {
                        int value = (int)(_pcmInfo[0][i][_pcmIndex[i] + j] * 32767);
                        value *= (int) normalizationFactor;
                        
                        if (value > 32767) value = 32767;
                        else if (value < -32768) value = -32768;
                        else if (value < 0) value = value | 32768;

                        _convertedBuffer[sampleIndex] = (byte)(value);
                        _convertedBuffer[sampleIndex + 1] = (byte)((uint)value >> 8);
                        
                        sampleIndex += 2 * _jorbisInfo.Channels;
                    }
                }
                
                int c = 2 * _jorbisInfo.Channels * range;
                outStream.Write(_convertedBuffer, 0, c);
                outStream.Flush();
                written += c;
                _jorbisDspState.SynthesisRead(range);

                long granulepos = _joggPacket.GranulePos;
                if (granulepos != -1 && _joggPacket.EndOfStream == 0)
                {
                    granulepos -= samples;
                    granulepos -= (long)BUFFER_SIZE * 6 * SampleSizeBytes();
                    _pcmOffset = granulepos;
                }
            }

            return written;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            lock (_readLock)
            {
                _joggStreamState.Clear();
                _jorbisBlock.Clear();
                _jorbisDspState.Clear();
                _joggSyncState.Clear();
            }
        }

        private class NotVorbisException : DecoderException
        {
            internal NotVorbisException() : base("Data read is not vorbis data")
            {
            }
        }

        private class HoleInDataException : DecoderException
        {
            internal HoleInDataException() : base("Hole in vorbis data")
            {
            }
        }
    }
}
