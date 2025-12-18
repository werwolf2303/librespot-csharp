using System;
using System.Collections.Generic;
using csharp;
using lib.core;
using lib.metadata;
using log4net;
using player.state;

namespace player.metrics
{
    public class PlaybackMetrics
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(PlaybackMetrics));
        public IPlayableId _id;
        internal String _playbackId;
        internal String _featureVersion;
        internal String _referrerIdentifier;
        internal String _contextUri;
        internal long _timestamp;
        private List<Interval> _intervals = new List<Interval>(10);
        internal PlayerMetrics _player = null;
        internal Reason _reasonStart = null;
        internal String _sourceStart = null;
        internal Reason _reasonEnd = null;
        internal String _sourceEnd = null;
        private Interval _lastInterval = null;

        public PlaybackMetrics(IPlayableId id, String playbackId, StateWrapper state)
        {
            _id = id;
            _playbackId = playbackId;
            _contextUri = state.GetContextUri();
            _featureVersion = state.GetPlayOrigin().FeatureVersion;
            _referrerIdentifier = state.GetPlayOrigin().ReferrerIdentifier;
            _timestamp = TimeProvider.currentTimeMillis();
        }

        public int FirstValue()
        {
            if (_intervals.Count == 0) return 0;
            else return _intervals[0]._begin;
        }

        public int LastValue()
        {
            if (_intervals.Count == 0) return _player == null ? 0 : _player._duration;
            else return _intervals[_intervals.Count - 1]._end;
        }

        public void StartInterval(int begin)
        {
            _lastInterval = new Interval(begin);
        }

        public void EndInterval(int end)
        {
            if (_lastInterval == null) return;
            if (_lastInterval._begin == end)
            {
                _lastInterval = null;
                return;
            }

            _lastInterval._end = end;
            _intervals.Add(_lastInterval);
            _lastInterval = null;
        }

        public void StartedHow(Reason reason, String origin)
        {
            _reasonStart = reason;
            _sourceStart = string.IsNullOrEmpty(origin) ? "unknown" : origin;
        }

        public void EndedHow(Reason reason, String origin)
        {
            _reasonEnd = reason;
            _sourceEnd = string.IsNullOrEmpty(origin) ? "unknown" : origin;
        }

        public void Update(PlayerMetrics playerMetrics)
        {
            _player = playerMetrics;
        }

        public void SendEvents(Session session, DeviceStateHandler device)
        {
            if (_player == null || _player._contentMetrics == null || device.GetLastCommandSentByDeviceId() == null)
            {
                LOGGER.Warn("Did not send event because of missing metrics: " + _playbackId);
                return;
            }
            
            session.GetEventService().SendEvent(new TrackTransitionEvent(session.GetDeviceId(), device.GetLastCommandSentByDeviceId(), this));
        }

        public class Reason : Enumeration
        {
            public static readonly Reason TrackDone = new Reason("trackdone");
            public static readonly Reason TrackError = new Reason("trackerror");
            public static readonly Reason ForwardBtn = new Reason("fwdbtn");
            public static readonly Reason BackBtn = new Reason("backbtn");
            public static readonly Reason EndPlay = new Reason("endplay");
            public static readonly Reason PlayBtn = new Reason("playbtn");
            public static readonly Reason ClickRow = new Reason("clickrow");
            public static readonly Reason Logout = new Reason("logout");
            public static readonly Reason AppLoad = new Reason("appload");
            public static readonly Reason Remote = new Reason("remote");

            public String _val;

            private Reason(String val)
            {
                _val = val;
            }
        }

        internal class Interval
        {
            public int _begin;
            public int _end = -1;

            internal Interval(int begin)
            {
                _begin = begin;
            }
        }
    }
}