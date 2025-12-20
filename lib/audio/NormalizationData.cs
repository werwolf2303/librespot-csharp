using System;
using System.IO;
using System.Linq;
using lib.common;
using log4net;

namespace lib.audio
{
    public class NormalizationData
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(NormalizationData));
        public float TrackGainDb;
        public float TrackPeak;
        public float AlbumGainDb;
        public float AlbumPeak;

        private NormalizationData(float trackGainDb, float trackPeak, float albumGainDb, float albumPeak)
        {
            TrackGainDb = trackGainDb;
            TrackPeak = trackPeak;
            AlbumGainDb = albumGainDb;
            AlbumPeak = albumPeak;
            
            LOGGER.DebugFormat("Loaded normalization data, track_gain: {0}, track_peak: {1}, album_gain: {2}, album_peak: {3}",
                trackGainDb, trackPeak, albumGainDb, albumPeak);
        }

        public static NormalizationData Read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            if(stream.Seek(144, SeekOrigin.Current) - stream.Position + 144 != 144) throw new IOException();

            byte[] data = new byte[16];
            reader.ReadFully(data);
            stream.Seek(16, SeekOrigin.Begin);
            
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(data);
            
            reader = new BinaryReader(new MemoryStream(data));
            
            return new NormalizationData(
                reader.ReadSingle(), // Track Gain dB
                reader.ReadSingle(), // Track Peak
                reader.ReadSingle(), // Album Gain dB
                reader.ReadSingle()  // Album Peak
            );
        }

        public float GetFactor(float normalisationPregain, bool useAlbumGain)
        {
            float gain = useAlbumGain ? AlbumGainDb : TrackGainDb;
            LOGGER.Debug("Using gain: " + gain);
            float normalisationFactor = (float) Math.Pow(10, (gain + normalisationPregain) / 20.0);
             if (normalisationFactor * TrackPeak > 1)
             {
                 LOGGER.Warn("Reducing normalisation factor to prevent clipping. Please add negative pregain to avoid.");
                 normalisationFactor = 1 / TrackPeak;
             }
             
             return normalisationFactor;
        }
        
        public float GetFactor(float normlisationPregain)
        {
            return GetFactor(normlisationPregain, false);
        }
    }
}