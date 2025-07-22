using System;
using System.Text;
using lib.audio;
using lib.audio.format;
using player.crossfade;
using sink_api;
using Decoder = decoder_api.Decoder;

namespace player.metrics
{
    public class PlayerMetrics
    {
        public PlayableContentFeeder.Metrics _contentMetrics;
        public int _decodedLenth = 0;
        public int _size = 0;
        public int _bitrate = 0;
        public float _sampleRate = 0;
        public int _duration = 0;
        public String _encoding = null;
        public int _fadeOverlap = 0;
        public String _transition = "none";
        public int _decryptTime = 0;

        public PlayerMetrics(PlayableContentFeeder.Metrics contentMetrics, CrossfadeController crossfade,
            IDecodedAudioStream stream, Decoder decoder)
        {
            _contentMetrics = contentMetrics;

            if (decoder != null)
            {
                _size = decoder.Size();
                _duration = decoder.DurationMs();

                OutputAudioFormat format = decoder.GetAudioFormat();
                _bitrate = (int)(format.getFrameRate() * format.getFrameSize());
                _sampleRate = format.getSampleRate();
            }

            if (stream != null)
            {
                _decryptTime = stream.DecryptTimeMS();
                _decodedLenth = stream.Stream().DecodedLength();

                if (Equals(stream.Codec(), SuperAudioFormat.MP3))
                {
                    _encoding = "mp3";
                } else if (Equals(stream.Codec(), SuperAudioFormat.VORBIS))
                {
                    _encoding = "vorbis";
                } else if (Equals(stream.Codec(), SuperAudioFormat.AAC))
                {
                    _encoding = "aac";
                }
            }

            if (crossfade != null)
            {
                _transition = "crossfade";
                _fadeOverlap = crossfade.FadeOverlap();
            }
        }
    }
}