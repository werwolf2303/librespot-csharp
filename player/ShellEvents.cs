using System;
using System.Diagnostics;
using lib.audio;
using lib.core;
using lib.metadata;
using log4net;

namespace player
{
    public class ShellEvents : Player.IEventsListener, Session.ReconnectionListener
    {
        private static ILog LOGGER =  LogManager.GetLogger(typeof(ShellEvents));
        private Configuration _conf;

        public ShellEvents(Configuration conf)
        {
            _conf = conf;
        }

        private void exec(String command, params String[] envp)
        {
            if (!_conf.Enabled)
                return;

            if (command == null || command.Trim().Equals(""))
                return;

            Process p;
            if (_conf.ExecuteWithBash)
            {
                p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = "-c \"" + command + "\"",
                        RedirectStandardOutput = false,
                        RedirectStandardError = true
                    }
                };
            }
            else
            {
                p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command.Trim(),
                        Arguments = String.Join(" ", envp),
                        RedirectStandardOutput = false,
                        RedirectStandardError = true
                    }
                };
            }
            p.Start();
            p.WaitForExit();
            int exitCode = p.ExitCode;
            LOGGER.DebugFormat("Executed shell command: {0} -> {1}", command, exitCode);
        }

        public void OnContextChanged(Player player, string newUri)
        {
            exec(_conf.OnContextChanged, "CONTEXT_URI=" + newUri);
        }

        public void OnTrackChanged(Player player, IPlayableId id, MetadataWrapper metadata, bool userInitiated)
        {
            exec(_conf.OnTrackChanged, "TRACK_URI=" + id.ToSpotifyUri(),
                "NAME=" + (metadata == null ? "" : metadata.GetName()),
                "ARTIST=" + (metadata == null ? "" : metadata.GetArtist()),
                "ALBUM=" + (metadata == null ? "" : metadata.GetAlbumName()),
                "DURATION=" + (metadata == null ? "" : metadata.Duration().ToString()),
                "IS_USER=" + userInitiated);
        }

        public void OnPlaybackEnded(Player player)
        {
            exec(_conf.OnPlaybackEnded);
        }

        public void OnPlaybackPaused(Player player, long trackTime)
        {
            exec(_conf.OnPlaybackPaused, "POSITION=" + trackTime);
        }

        public void OnPlaybackResumed(Player player, long trackTime)
        {
            exec(_conf.OnPlaybackResumed, "POSITION=" + trackTime);
        }

        public void OnPlaybackFailed(Player player, Exception ex)
        {
            exec(_conf.OnPlaybackFailed, "EXCEPTION=", ex.GetType().FullName, "MESSAGE=" + ex.Message);
        }

        public void OnTrackSeeked(Player player, long trackTime)
        {
            exec(_conf.OnTrackSeeked, "POSTITION=" + trackTime);
        }

        public void OnMetadataAvailable(Player player, MetadataWrapper metadata)
        {
            exec(_conf.OnMetadataAvailable, "TRACK_URI=" + metadata._id.ToSpotifyUri(),
                "NAME=" + metadata.GetName(), "ARTIST=" + metadata.GetArtist(),
                "ALBUM=" + metadata.GetAlbumName(), "DURATION=" + metadata.Duration());
        }

        public void OnPlaybackHaltStateChanged(Player player, bool halted, long trackTime)
        {
        }

        public void OnInactiveSession(Player player, bool timeout)
        {
            exec(_conf.OnInactiveSession);
        }

        public void OnVolumeChanged(Player player, float volume)
        {
            exec(_conf.OnVolumeChanged, "VOLUME=" + Math.Round(volume * 100f));
        }

        public void OnPanicState(Player player)
        {
            exec(_conf.OnPanicState);
        }

        public void OnStartedLoading(Player player)
        {
            exec(_conf.OnStartedLoading);
        }

        public void OnFinishedLoading(Player player)
        {
            exec(_conf.OnFinishedLoading);
        }

        public void OnConnectionDropped()
        {
            exec(_conf.OnConnectionDropped);
        }

        public void OnConnectionEstablished()
        {
            exec(_conf.OnConnectionEstablished);
        }

        public class Configuration
        {
            public bool Enabled;
            public bool ExecuteWithBash;
            public String OnContextChanged;
            public String OnTrackChanged;
            public String OnPlaybackEnded;
            public String OnPlaybackPaused;
            public String OnPlaybackResumed;
            public String OnPlaybackFailed;
            public String OnTrackSeeked;
            public String OnMetadataAvailable;
            public String OnVolumeChanged;
            public String OnInactiveSession;
            public String OnPanicState;
            public String OnConnectionDropped;
            public String OnConnectionEstablished;
            public String OnStartedLoading;
            public String OnFinishedLoading;
            
            public Configuration(bool enabled, bool executeWithBash, String onContextChanged, String onTrackChanged, String onPlaybackEnded, String onPlaybackPaused,
                String onPlaybackResumed, String onPlaybackFailed, String onTrackSeeked, String onMetadataAvailable, String onVolumeChanged,
                String onInactiveSession, String onPanicState, String onConnectionDropped, String onConnectionEstablished,
                String onStartedLoading, String onFinishedLoading) {
                Enabled = enabled;
                ExecuteWithBash = executeWithBash;
                OnContextChanged = onContextChanged;
                OnTrackChanged = onTrackChanged;
                OnPlaybackEnded = onPlaybackEnded;
                OnPlaybackPaused = onPlaybackPaused;
                OnPlaybackResumed = onPlaybackResumed;
                OnPlaybackFailed = onPlaybackFailed;
                OnTrackSeeked = onTrackSeeked;
                OnMetadataAvailable = onMetadataAvailable;
                OnVolumeChanged = onVolumeChanged;
                OnInactiveSession = onInactiveSession;
                OnPanicState = onPanicState;
                OnConnectionDropped = onConnectionDropped;
                OnConnectionEstablished = onConnectionEstablished;
                OnStartedLoading = onStartedLoading;
                OnFinishedLoading = onFinishedLoading;
            }

            public class Builder
            {
                private bool Enabled = false;
                private bool ExecuteWithBash = false;
                private String OnContextChanged = "";
                private String OnTrackChanged = "";
                private String OnPlaybackEnded = "";
                private String OnPlaybackPaused = "";
                private String OnPlaybackResumed = "";
                private String OnPlaybackFailed = "";
                private String OnTrackSeeked = "";
                private String OnMetadataAvailable = "";
                private String OnVolumeChanged = "";
                private String OnInactiveSession = "";
                private String OnPanicState = "";
                private String OnConnectionDropped = "";
                private String OnConnectionEstablished = "";
                private String OnStartedLoading = "";
                private String OnFinishedLoading = "";

                public Builder setEnabled(bool enabled)
                {
                    Enabled = enabled;
                    return this;
                }

                public Builder setExecuteWithBash(bool executeWithBash)
                {
                    ExecuteWithBash = executeWithBash;
                    return this;
                }

                public Builder setOnContextChanged(String command)
                {
                    OnContextChanged = command;
                    return this;
                }

                public Builder setOnTrackChanged(String command)
                {
                    OnTrackChanged = command;
                    return this;
                }

                public Builder setOnPlaybackEnded(String command)
                {
                    OnPlaybackEnded = command;
                    return this;
                }

                public Builder setOnPlaybackPaused(String command)
                {
                    OnPlaybackPaused = command;
                    return this;
                }

                public Builder setOnPlaybackResumed(String command)
                {
                    OnPlaybackResumed = command;
                    return this;
                }

                public Builder setOnPlaybackFailed(String command)
                {
                    OnPlaybackFailed = command;
                    return this;
                }

                public Builder setOnTrackSeeked(String command)
                {
                    OnTrackSeeked = command;
                    return this;
                }

                public Builder setOnMetadataAvailable(String command)
                {
                    OnMetadataAvailable = command;
                    return this;
                }

                public Builder setOnVolumeChanged(String command)
                {
                    OnVolumeChanged = command;
                    return this;
                }

                public Builder setOnInactiveSession(String command)
                {
                    OnInactiveSession = command;
                    return this;
                }

                public Builder setOnPanicState(String command)
                {
                    OnPanicState = command;
                    return this;
                }

                public Builder setOnConnectionDropped(String command)
                {
                    OnConnectionDropped = command;
                    return this;
                }

                public Builder setOnConnectionEstablished(String command)
                {
                    OnConnectionEstablished = command;
                    return this;
                }

                public Builder setOnStartedLoading(String onStartedLoading)
                {
                   OnStartedLoading = onStartedLoading;
                    return this;
                }

                public Builder setOnFinishedLoading(String onFinishedLoading)
                {
                    OnFinishedLoading = onFinishedLoading;
                    return this;
                }

                public Configuration Build()
                {
                    return new Configuration(Enabled, ExecuteWithBash, OnContextChanged, OnTrackChanged, 
                        OnPlaybackEnded, OnPlaybackPaused, OnPlaybackResumed,
                        OnPlaybackFailed, OnTrackSeeked, OnMetadataAvailable, OnVolumeChanged, OnInactiveSession,
                        OnPanicState, OnConnectionDropped, OnConnectionEstablished,
                        OnStartedLoading, OnFinishedLoading);
                }
            }
        }
    }
}