using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web;
using Connectstate;
using csharp;
using lib.common;
using lib.core;
using lib.dealer;
using lib.mercury;
using log4net;
using log4net.Util;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities.Encoders;
using ProtoBuf;
using spotify.player.proto;
using Version = lib.Version;

namespace player.state
{
    public class DeviceStateHandler : IDisposable, DealerClient.MessageListener, DealerClient.RequestListener
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(DeviceStateHandler));
        private Session _session;
        private DeviceInfo _deviceInfo;
        private List<Listener> _listeners = new List<Listener>();
        private PutStateRequest _putState;
        private ScheduledExecutorService _scheduler;
        private volatile String _connectionId = null;
        private volatile bool _closing = false;
        private String _lastCommandSentByDeviceId;
        private Object _funcLock = new Object();

        public DeviceStateHandler(Session session, PlayerConfiguration conf)
        {
            _session = session;
            _deviceInfo = InitializeDeviceInfo(session, conf);
            _scheduler = _session.GetScheduledExecutorService();
            _putState = new PutStateRequest
            {
                MemberType = MemberType.ConnectState,
                Device = new Device
                {
                    DeviceInfo = _deviceInfo
                }
            };
            
            session.GetDealer().AddMessageListener(this, "hm://pusher/v1/connections/", "hm://connect-state/v1/connect/volume", "hm://connect-state/v1/cluster");
            session.GetDealer().AddRequestListener(this, "hm://connect-state/v1/");
        }

        private DeviceInfo InitializeDeviceInfo(Session session, PlayerConfiguration conf)
        {
            return new DeviceInfo
            {
                CanPlay = true,
                Volume = (uint)conf._initialVolume,
                Name = session.GetDeviceName(),
                DeviceId = session.GetDeviceId(),
                DeviceType = session.GetDeviceType(),
                DeviceSoftwareVersion = Version.versionString(),
                ClientId = MercuryRequests.KEYMASTER_CLIENT_ID,
                SpircVersion = "3.2.6",
                Capabilities = new Capabilities
                {
                    CanBePlayer = true,
                    GaiaEqConnectId = true,
                    SupportsLogout = true,
                    IsObservable = true,
                    CommandAcks = true,
                    SupportsRename = false,
                    SupportsPlaylistV2 = true,
                    IsControllable = true,
                    SupportsTransferCommand = true,
                    SupportsGzipPushes = true,
                    NeedsFullPlayerState = false,
                    SupportedTypes = { "audio/episode", "audio/track" },
                    VolumeSteps = conf._volumeSteps,
                    SupportsCommandRequest = true
                }
            };
        }

        public void AddListener(Listener listener)
        {
            _listeners.Add(listener);
        }

        public void RemoveListener(Listener listener)
        {
            _listeners.Remove(listener);
        }

        private void NotifyReady()
        {
            _listeners.ForEach(listener => listener.Ready());
        }

        private void NotifyCommand(Endpoint endpoint, CommandBody data)
        {
            if (_listeners.Count == 0)
            {
                LOGGER.Warn("Cannot dispatch command because there are no listeners. (command: " + endpoint + ")");
                return;
            }
            
            _listeners.ForEach(listener => listener.Command(endpoint, data));
        }

        private void NotifyVolumeChange()
        {
            _listeners.ForEach(listener => listener.VolumeChanged());
        }

        private void NotifyNotActive()
        {
            _listeners.ForEach(listener => listener.NotActive());
        }

        private void UpdateConnectionId(String newer)
        {
            lock (_funcLock)
            {
                newer = HttpUtility.UrlDecode(newer, Encoding.UTF8);

                if (_connectionId == null || !_connectionId.Equals(newer))
                {
                    _connectionId = newer;
                    LOGGER.Debug("Updated Spotify-Connection-Id: " + _connectionId);
                    NotifyReady();
                }
            }
        }

        public void OnMessage(String uri, Dictionary<String, String> headers, byte[] payload)
        {
            if (uri.StartsWith("hm://pusher/v1/connections/")) {
                UpdateConnectionId(headers["Spotify-Connection-Id"]);
            } else if (Equals(uri, "hm://connect-state/v1/connect/volume")) {
                SetVolumeCommand cmd = Serializer.Deserialize<SetVolumeCommand>(new MemoryStream(payload));
                SetVolume((uint)cmd.Volume);
            } else if (Equals(uri, "hm://connect-state/v1/cluster"))
            {
                FileStream stream = File.OpenWrite("cluster_update.dump");
                stream.Write(payload, 0, payload.Length);
                stream.Close();
                ClusterUpdate update = Serializer.Deserialize<ClusterUpdate>(new MemoryStream(payload));

                long now = TimeProvider.currentTimeMillis();
                LOGGER.DebugFormat("Received cluster update at {0}: {1}", now, JToken.FromObject(update));
                
                long ts = update.Cluster.Timestamp - 3000; // Workaround
                if (!_session.GetDeviceId().Equals(update.Cluster.ActiveDeviceId) && IsActive() && now > StartedPlayingAt() && ts > StartedPlayingAt())
                    NotifyNotActive();
            } else {
                LOGGER.WarnFormat("Message left unhandled! (uri: {0})", uri);
            }
        }

        public DealerClient.RequestResult OnRequest(String mid, int pid, String sender, JObject command)
        {
            _lastCommandSentByDeviceId = sender;
            
            Endpoint endpoint = Endpoint.Parse(command["endpoint"].ToObject<string>());
            NotifyCommand(endpoint, new CommandBody(command));
            return DealerClient.RequestResult.Success;
        }
        
        public String GetLastCommandSentByDeviceId()
        {
            lock (_funcLock)
            {
                return _lastCommandSentByDeviceId;
            }
        }

        private long StartedPlayingAt()
        {
            lock (_funcLock)
            {
                return (long)_putState.StartedPlayingAt;
            }
        }
        
        public bool IsActive()
        {
            lock (_funcLock)
            {
                return _putState.IsActive;
            }
        }
        
        public void SetIsActive(bool active)
        {
            lock (_funcLock)
            {
                if (active)
                {
                    if (!_putState.IsActive)
                    {
                        ulong now = (ulong)TimeProvider.currentTimeMillis();
                        _putState.IsActive = true;
                        _putState.StartedPlayingAt = now;
                        LOGGER.DebugFormat("Device is now active. (ts: {0})", now);
                    }
                }
                else
                {
                    _putState.IsActive = false;
                    _putState.StartedPlayingAt = 0;
                }
            }
        }
        
        public void UpdateState(PutStateReason reason, int playerTime, PlayerState state)
        {
            lock (_funcLock)
            {
                if (_connectionId == null) throw new Exception();

                long timestamp = TimeProvider.currentTimeMillis();

                if (playerTime == -1)
                    _putState.HasBeenPlayingForMs = 0;
                else
                    _putState.HasBeenPlayingForMs =
                        (ulong)Math.Min(playerTime, timestamp - (long)_putState.StartedPlayingAt);

                _putState.PutStateReason = reason;
                _putState.ClientSideTimestamp = (ulong)timestamp;
                _putState.Device.DeviceInfo = _deviceInfo;
                _putState.Device.PlayerState = state;

                _scheduler.schedule(new ScheduledExecutorService.ScheduledFuture<int>(() =>
                {
                    PutConnectState(_putState);
                    return 0;
                }, 1));
            }
        }
        
        public uint GetVolume()
        {
            lock (_funcLock)
            {
                return _deviceInfo.Volume;
            }
        }

        public void SetVolume(uint val)
        {
            lock (this)
            {
                _deviceInfo.Volume = val;
            }
            
            NotifyVolumeChange();
            LOGGER.DebugFormat("Update volume. (volume: {0}/{1})", val, Player.VOLUME_MAX);
        }

        public void Dispose()
        {
            _closing = true;
            
            _session.GetDealer().RemoveMessageListener(this);
            _session.GetDealer().RemoveRequestListener(this);
            
            _listeners.Clear();
        }

        private void PutConnectState(PutStateRequest req) {
            try {
                _session.GetApi().PutConnectState(_connectionId, req);
                LOGGER.InfoFormat("Put state. (ts: {0}, connId: {1}, reason: {2}, request: {3})", req.ClientSideTimestamp, 
                    Utils.truncateMiddle(_connectionId, 10), req.PutStateReason, JToken.FromObject(req));
            } catch (Exception ex) {
                if (ex is IOException || ex is MercuryClient.MercuryException)
                {
                    LOGGER.ErrorExt("Failed updating state.", ex);
                }
                else throw;
            }
        }

        public class Endpoint : Enumeration
        {
            public static readonly Endpoint Play = new Endpoint("play");
            public static readonly Endpoint Pause = new Endpoint("pause");
            public static readonly Endpoint Resume = new Endpoint("resume");
            public static readonly Endpoint SeekTo = new Endpoint("seek_to");
            public static readonly Endpoint SkipNext = new Endpoint("skip_next");
            public static readonly Endpoint SkipPrev = new Endpoint("skip_prev");
            public static readonly Endpoint SetShufflingContext = new Endpoint("set_shuffling_context");
            public static readonly Endpoint SetRepeatingContext = new Endpoint("set_repeating_context");
            public static readonly Endpoint UpdateContext = new Endpoint("update_context");
            public static readonly Endpoint SetQueue = new Endpoint("set_queue");
            public static readonly Endpoint AddToQueue = new Endpoint("add_to_queue");
            public static readonly Endpoint Transfer = new Endpoint("transfer");
            
            private String val;

            private Endpoint(String val)
            {
                this.val = val;
            }

            public Endpoint()
            {
            }

            public static Endpoint Parse(String value)
            {
                foreach (Endpoint e in GetAll<Endpoint>())
                {
                    if (e.val.Equals(value))
                        return e;
                }
                
                throw new ArgumentException("Unknown endpoint for " + value);
            }
        }

        public interface Listener
        {
            void Ready();

            void Command(Endpoint endpoint, CommandBody data);

            void VolumeChanged();

            void NotActive();
        }

        public class PlayCommandHelper
        {
            private PlayCommandHelper()
            {
            }

            public static bool? IsInitiallyPaused(JObject obj)
            {
                JObject options = Utils.OptionalJSON<JObject>(obj, "options", null); 
                if (options == null) return null;

                JToken elm;
                if (
                    (elm = Utils.OptionalJSON<JObject>(options, "initially_paused", null)) != null
                    && elm.Type == JTokenType.Boolean)
                {
                    return elm.ToObject<bool>();
                }
                
                return null;
            }

            public static string GetContextUri(JObject obj)
            {
                JObject context = Utils.OptionalJSON<JObject>(obj, "context", null);
                if (context == null) return null;
                
                JToken elm;
                if ((elm = Utils.OptionalJSON<JObject>(context, "uri", null)) != null
                    && elm.Type == JTokenType.String)
                {
                    return elm.ToObject<string>();
                }
                
                return null;
            }

            public static JObject GetPlayOrigin(JObject obj)
            {
                return Utils.OptionalJSON<JObject>(obj, "play_origin", null);
            }

            public static JObject GetContext(JObject obj)
            {
                return Utils.OptionalJSON<JObject>(obj, "context", null);
            }

            public static JObject GetPlayerOptionsOverride(JObject obj)
            {
                return Utils.OptionalJSON<JObject>(obj, "options", null);
            }

            public static bool WillSkipToSomething(JObject obj)
            { 
                JObject parent = Utils.OptionalJSON<JObject>(obj, "options", null);
                if (parent == null) return false;
                
                parent = Utils.OptionalJSON<JObject>(parent, "skip_to", null);
                if (parent == null) return false;

                return parent.ContainsKey("track_uid") || parent.ContainsKey("track_uri") ||
                       parent.ContainsKey("track_index");
            }

            public static string GetSkipToUid(JObject obj)
            {
                JObject parent = Utils.OptionalJSON<JObject>(obj, "options", null);
                if (parent == null) return null;
                
                parent = Utils.OptionalJSON<JObject>(parent, "skip_to", null);
                if (parent == null) return null;
                
                JToken elm;
                if ((elm = Utils.OptionalJSON<JToken>(parent, "track_uid", null)) != null
                    && elm.Type == JTokenType.String)
                {
                    return elm.ToObject<string>();
                }
                
                return null;
            }

            public static string GetSkipToUri(JObject obj)
            {
                JObject parent = Utils.OptionalJSON<JObject>(obj, "options", null);
                if (parent == null) return null;
                
                parent = Utils.OptionalJSON<JObject>(parent, "skip_to", null);
                if (parent == null) return null;
                
                JToken elm;
                if ((elm = Utils.OptionalJSON<JToken>(parent, "track_uri", null)) != null
                    && elm.Type == JTokenType.String)
                {
                    return elm.ToObject<string>();
                }
                
                return null;
            }

            public static List<ContextTrack> GetNextTracks(JObject obj)
            {
                JArray nextTracks = Utils.OptionalJSON<JArray>(obj, "next_tracks", null);
                if (nextTracks == null) return null;
                
                return ProtoUtils.JsonToContextTracks(nextTracks);
            }

            public static List<ContextTrack> GetPrevTracks(JObject obj)
            {
                JArray prevTracks = Utils.OptionalJSON<JArray>(obj, "prev_tracks", null);
                if (prevTracks == null) return null;
                
                return ProtoUtils.JsonToContextTracks(prevTracks);
            }

            public static ContextTrack GetTrack(JObject obj)
            {
                JObject track = Utils.OptionalJSON<JObject>(obj, "track", null);
                if (track == null) return null;
                return ProtoUtils.JsonToContextTrack(track);
            }

            public static int? GetSkipToIndex(JObject obj)
            {
                JObject parent = Utils.OptionalJSON<JObject>(obj, "options", null);
                if (parent == null) return null;
                
                parent = Utils.OptionalJSON<JObject>(parent, "skip_to", null);
                if (parent == null) return null;
                
                JToken elm;
                if ((elm = Utils.OptionalJSON<JToken>(parent, "track_index", null)) != null
                    && elm.Type == JTokenType.Integer)
                {
                    return elm.ToObject<int>();
                }
                
                return null;
            }

            public static int? GetSeekTo(JObject obj)
            {
                JObject options = Utils.OptionalJSON<JObject>(obj, "options", null);
                if (options == null) return null;
                
                JToken elm;
                if ((elm = Utils.OptionalJSON<JToken>(options, "seek_to", null)) != null
                    && elm.Type == JTokenType.Integer)
                {
                    return elm.ToObject<int>();
                }
                
                return null;
            }
        }

        public class CommandBody
        {
            private JObject _obj;
            private byte[] _data;
            private String _value;

            internal CommandBody(JObject obj)
            {
                _obj = obj;
                
                if (_obj.ContainsKey("data")) _data = Base64.Decode(_obj["data"].ToObject<string>());
                else _data = null;
                
                if (_obj.ContainsKey("value")) _value = _obj["value"].ToObject<string>();
                else _value = null;
            }

            public JObject GetObj()
            {
                return _obj;
            }

            public byte[] GetData()
            {
                return _data;
            }

            public String GetValue()
            {
                return _value;
            }

            public int? GetValueInt()
            {
                if (_value == null) return null;
                return Int32.Parse(_value);
            }

            public bool? GetValueBool()
            {
                if (_value == null) return null;
                return Boolean.Parse(_value);
            }
        }
    }
}