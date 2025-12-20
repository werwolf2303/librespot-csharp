using System;
using System.IO;
using decoder_api;
using log4net;
using sink_api;

namespace player.mixing
{
public sealed class MixingLine : Stream
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(MixingLine));
        internal bool _switchFormat = false;
        private GainAwareCircularBuffer _fcb;
        private GainAwareCircularBuffer _scb;
        private FirstOutputStream _fout;
        private SecondOutputStream _sout;
        private volatile bool _fe = false;
        private volatile bool _se = false;
        private volatile float _fg = 1;
        private volatile float _sg = 1;
        private volatile float _gg = 1;
        private OutputAudioFormat _format = OutputAudioFormat.DEFAULT_FORMAT;
        private AudioSink _audioSink;

        public MixingLine() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (this)
            {
                if (_fe && _fcb != null && _se && _scb != null)
                {
                    int willRead = Math.Min(_fcb.Available(), _scb.Available());
                    willRead = Math.Min(willRead, count);
                    if (_format != null) willRead -= willRead % _format.getFrameSize();

                    _fcb.Read(buffer, offset, willRead); 
                    _scb.ReadMergeGain(buffer, offset, willRead, _gg, _fg, _sg);
                    return willRead;
                }
                else if (_fe && _fcb != null)
                {
                    _fcb.ReadGain(buffer, offset, count, _gg * _fg);
                    return count;
                }
                else if (_se && _scb != null)
                {
                    _scb.ReadGain(buffer, offset, count, _gg * _sg);
                    return count;
                }
                else
                {
                    return 0;
                }
            }
        }
        
        public MixingOutput SomeOut(AudioSink sink)
        {
            _audioSink = sink;
            if (_fout == null) return FirstOut();
            if (_sout == null) return SecondOut();
            return null;
        }

        public MixingOutput FirstOut()
        {
            if (_fout == null)
            {
                _fcb = new GainAwareCircularBuffer(Decoder.BUFFER_SIZE * 4);
                _fout = new FirstOutputStream(this);
            }
            return _fout;
        }

        public MixingOutput SecondOut()
        {
            if (_sout == null)
            {
                _scb = new GainAwareCircularBuffer(Decoder.BUFFER_SIZE * 4);
                _sout = new SecondOutputStream(this);
            }
            return _sout;
        }

        public void SetGlobalGain(float gain)
        {
            _gg = gain;
        }

        public OutputAudioFormat GetFormat()
        {
            return _format;
        }

        private StreamConverter SetFormat(OutputAudioFormat format, MixingOutput from)
        {
            if (_format == null)
            {
                _format = format;
                return null;
            }
            else if (!_format.matches(format))
            {
                if (StreamConverter.CanConvert(format, _format))
                {
                    LOGGER.InfoFormat("Converting, '{0}' -> '{1}'", format, _format);
                    return StreamConverter.Converter(format, _format);
                }
                else
                {
                    if (_fout == from && _sout != null) _sout.Clear();
                    else if (_sout == from && _fout != null) _fout.Clear();

                    LOGGER.InfoFormat("Switching format, '{0}' -> '{1}'", format, _format);
                    _format = format;
                    _switchFormat = true;
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        #region Stream Overrides
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            _audioSink.Flush();
        }
        public override int ReadByte() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        #endregion

        public abstract class MixingOutput : Stream
        {
            protected readonly MixingLine _owner;
            protected StreamConverter _converter = null;
            protected MixingOutput(MixingLine owner) { _owner = owner; }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (_converter != null)
                {
                    _converter.Write(buffer, offset, count);
                    byte[] converted = _converter.Convert();
                    WriteBuffer(converted, 0, converted.Length);
                }
                else
                {
                    WriteBuffer(buffer, offset, count);
                }
            }
            protected abstract void WriteBuffer(byte[] b, int off, int len);
            public abstract void Toggle(bool enabled, OutputAudioFormat format);
            public abstract void SetGain(float gain);
            public abstract void Clear();
            public abstract void EmptyBuffer();

            #region Stream Overrides
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Flush()
            {
                _owner.Flush();
            }
            
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            #endregion
        }

        public class FirstOutputStream : MixingOutput
        {
            public FirstOutputStream(MixingLine owner) : base(owner) { }

            protected override void WriteBuffer(byte[] b, int off, int len)
            {
                if (_owner._fout == null || _owner._fout != this) return;
                _owner._fcb.Write(b, off, len);
            }

            public override void Toggle(bool enabled, OutputAudioFormat format)
            {
                if (enabled == _owner._fe) return;
                if (enabled && (_owner._fout == null || _owner._fout != this)) return;
                if (enabled && format == null) throw new ArgumentNullException(nameof(format));

                if (format != null) _converter = _owner.SetFormat(format, this);
                _owner._fe = enabled;
            }

            public override void SetGain(float gain)
            {
                if (_owner._fout == null || _owner._fout != this) return;
                _owner._fg = gain;
            }

            public override void Clear()
            {
                if (_owner._fout == null || _owner._fout != this) return;

                _owner._fg = 1;
                _owner._fe = false;

                _owner._fcb.Dispose();
                lock (_owner)
                {
                    _owner._fout = null;
                    _owner._fcb = null;
                }
            }
            
            public override void EmptyBuffer()
            {
                if (_owner._fout == null || _owner._fout != this) return;
                _owner._fcb.Empty();
            }
        }

        public class SecondOutputStream : MixingOutput
        {
            public SecondOutputStream(MixingLine owner) : base(owner) { }

            protected override void WriteBuffer(byte[] b, int off, int len)
            {
                if (_owner._sout == null || _owner._sout != this) return;
                _owner._scb.Write(b, off, len);
            }
            
            public override void Toggle(bool enabled, OutputAudioFormat format)
            {
                if (enabled == _owner._se) return;
                if (enabled && (_owner._sout == null || _owner._sout != this)) return;
                if (enabled && format == null) throw new ArgumentNullException(nameof(format));

                if (format != null) _converter = _owner.SetFormat(format, this);
                _owner._se = enabled;
            }

            public override void SetGain(float gain)
            {
                if (_owner._sout == null || _owner._sout != this) return;
                _owner._sg = gain;
            }

            public override void Clear()
            {
                if (_owner._sout == null || _owner._sout != this) return;

                _owner._sg = 1;
                _owner._se = false;

                _owner._scb.Dispose();
                lock (_owner)
                {
                    _owner._sout = null;
                    _owner._scb = null;
                }
            }

            public override void EmptyBuffer()
            {
                if (_owner._sout == null || _owner._sout != this) return;
                _owner._scb.Empty();
            }
        }
    }
}