using System;
using lib.audio.decoders;

namespace player
{
    public class PlayerConfiguration
    {
        // Audio
        public AudioQuality _preferredQuality;
        public bool _enableNormalisation;
        public bool _useAlbumGain;
        public float _normalisationPregain;
        public bool _autoplayEnabled;
        public int _crossfadeDuration;
        public AudioOutputMethod _audioOutputMethod;
        public String _audioOutputClass;
        public bool _preloadEnabled;
        
        // Output
        public AudioOutput _output;
        public String _outputClass;
        public Object[] _outputClassParams;
        public String _outputPipe;
        public String _metadataPipe;
        public String[] _mixerSearchKeywords;
        public bool _logAvailableMixers;
        public int _releaseLineDelay;
        
        // Volume
        public int _initialVolume;
        public int _volumeSteps;
        public bool _bypassSinkVolume;
        
        // Local files
        public String _localFilesPath;

        private PlayerConfiguration(
            bool preloadEnabled,
            AudioQuality preferredQuality,
            bool enableNormalisation,
            bool useAlbumGain,
            float normalisationPregain,
            bool autoplayEnabled,
            int crossfadeDuration,
            String audioOutputClass,
            AudioOutput output,
            String outputClass,
            Object[] outputClassParams,
            String outputPipe,
            String metadataPipe,
            String[] mixerSearchKeywords,
            bool logAvailableMixers,
            AudioOutputMethod audioOutputMethod,
            int releaseLineDelay,
            int initialVolume,
            int volumeSteps,
            bool bypassSinkVolume,
            String localFilesPath
            )
        {
            _preloadEnabled = preloadEnabled;
            _preferredQuality = preferredQuality;
            _enableNormalisation = enableNormalisation;
            _useAlbumGain = useAlbumGain;
            _normalisationPregain = normalisationPregain;
            _autoplayEnabled = autoplayEnabled;
            _crossfadeDuration = crossfadeDuration;
            _audioOutputClass = audioOutputClass;
            _output = output;
            _outputClass = outputClass;
            _outputClassParams = outputClassParams;
            _outputPipe = outputPipe;
            _metadataPipe = metadataPipe;
            _mixerSearchKeywords = mixerSearchKeywords;
            _logAvailableMixers = logAvailableMixers;
            _audioOutputMethod = audioOutputMethod;
            _releaseLineDelay = releaseLineDelay;
            _initialVolume = initialVolume;
            _volumeSteps = volumeSteps;
            _bypassSinkVolume = bypassSinkVolume;
            _localFilesPath = localFilesPath;
        }
        
        public enum AudioOutput
        {
            MIXER, PIPE, STDOUT, CUSTOM
        }

        public enum AudioOutputMethod
        {
            AUTO, ALSA, CUSTOM
        }
        
        public class Builder
        {
            // Audio
            private AudioQuality _preferredQuality = AudioQuality.NORMAL;
            private bool _enableNormalisation = true;
            private bool _useAlbumGain = false;
            private float _normalisationPregain = 3.0f;
            private bool _autoplayEnabled = true;
            private int _crossfadeDuration = 0;
            private bool _preloadEnabled = true;
            private AudioOutputMethod _audioOutputMethod = AudioOutputMethod.AUTO;
            private String _audioOutputClass = "";
            
            // Output
            private AudioOutput _output = AudioOutput.MIXER;
            private String _outputClass;
            private Object[] _outputClassParams;
            private String _outputPipe;
            private String _metadataPipe;
            private String[] _mixerSearchKeywords;
            private bool _logAvailableMixers = true;
            private int _releaseLineDelay = 20;
            
            // Volume
            private int _initialVolume = Player.VOLUME_MAX;
            private int _volumeSteps = 64;
            private bool _bypassSinkVolume = false;
            
            // Local files
            private String _localFilesPath;
            
            public Builder() {
            }

            public Builder SetPreferredQuality(AudioQuality preferredQuality)
            {
                _preferredQuality = preferredQuality;
                return this;
            }

            public Builder SetEnableNormalisation(bool enableNormalisation)
            {
                _enableNormalisation = enableNormalisation;
                return this;
            }

            public Builder SetUseAlbumGain(bool useAlbumGain)
            {
                _useAlbumGain = useAlbumGain;
                return this;
            }

            public Builder SetNormalisationPregain(float normalisationPregain)
            {
                _normalisationPregain = normalisationPregain;
                return this;
            }

            public Builder SetAudioOutputClass(String classPath)
            {
                _audioOutputClass = classPath;
                return this;
            }

            public Builder SetAutoplayEnabled(bool autoplayEnabled)
            {
                _autoplayEnabled = autoplayEnabled;
                return this;
            }

            public Builder SetCrossfadeDuration(int crossfadeDuration)
            {
                _crossfadeDuration = crossfadeDuration;
                return this;
            }

            public Builder SetOutput(AudioOutput output)
            {
                _output = output;
                return this;
            }

            public Builder SetOutputClass(String outputClass)
            {
                _outputClass = outputClass;
                return this;
            }

            public Builder SetOutputPipe(String outputPipe)
            {
                _outputPipe = outputPipe;
                return this;
            }

            public Builder SetMetadataPipe(String metadataPipe)
            {
                _metadataPipe = metadataPipe;
                return this;
            }

            public Builder SetLogAvailableMixers(bool logAvailableMixers)
            {
                _logAvailableMixers = logAvailableMixers;
                return this;
            }

            public Builder SetAudioOutputMethod(AudioOutputMethod method)
            {
                _audioOutputMethod = method;
                return this;
            }

            public Builder SetReleaseLineDelay(int releaseLineDelay)
            {
                _releaseLineDelay = releaseLineDelay;
                return this;
            }

            public Builder SetInitialVolume(int initialVolume)
            {
                if (initialVolume < 0 || initialVolume > Player.VOLUME_MAX)
                    throw new ArgumentException("Invalid volume: " + initialVolume);
                
                _initialVolume = initialVolume;
                return this;
            }

            public Builder SetVolumeSteps(int volumeSteps)
            {
                if (volumeSteps < 0 || volumeSteps > Player.VOLUME_MAX)
                    throw new ArgumentException("Invalid volume steps: " + volumeSteps);

                _volumeSteps = volumeSteps;
                return this;
            }

            public Builder SetPreloadEnabled(bool preloadEnabled)
            {
                _preloadEnabled = preloadEnabled;
                return this;
            }

            public Builder SetBypassSinkVolume(bool bypassSinkVolume)
            {
                _bypassSinkVolume = bypassSinkVolume;
                return this;
            }

            public Builder SetLocalFilesPath(string localFilesPath)
            {
                _localFilesPath = localFilesPath;
                return this;
            }

            public PlayerConfiguration Build()
            {
                return new PlayerConfiguration(
                    _preloadEnabled,
                    _preferredQuality,
                    _enableNormalisation,
                    _useAlbumGain,
                    _normalisationPregain,
                    _autoplayEnabled,
                    _crossfadeDuration,
                    _audioOutputClass,
                    _output,
                    _outputClass,
                    _outputClassParams,
                    _outputPipe,
                    _metadataPipe,
                    _mixerSearchKeywords,
                    _logAvailableMixers,
                    _audioOutputMethod,
                    _releaseLineDelay,
                    _initialVolume,
                    _volumeSteps,
                    _bypassSinkVolume,
                    _localFilesPath
                );
            }
        }
    }
}