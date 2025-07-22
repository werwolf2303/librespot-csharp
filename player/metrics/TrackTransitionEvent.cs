using System;
using lib.core;

namespace player.metrics
{
    public class TrackTransitionEvent : EventService.GenericEvent
    {
        private static int _trackTransitionIncremental = 0;
        private String _deviceId;
        private String _lastCommandSentByDeviceId;
        private PlaybackMetrics _metrics;

        public TrackTransitionEvent(String deviceId, String lastCommandSentByDeviceId, PlaybackMetrics metrics)
        {
            _deviceId = deviceId;
            _lastCommandSentByDeviceId = lastCommandSentByDeviceId;
            _metrics = metrics;
        }
        
        public EventService.EventBuilder Build()
        {
            if (_metrics._player._contentMetrics == null)
                throw new InvalidOperationException();

            int when = _metrics.LastValue();
            EventService.EventBuilder eventBuilder = new EventService.EventBuilder(EventService.Type.TrackTransition);
            eventBuilder.Append(_trackTransitionIncremental++.ToString());
            eventBuilder.Append(_deviceId);
            eventBuilder.Append(_metrics._playbackId).Append("00000000000000000000000000000000");
            eventBuilder.Append(_metrics._sourceStart)
                .Append(_metrics._reasonStart == null ? null : _metrics._reasonStart._val);
            eventBuilder.Append(_metrics._sourceEnd)
                .Append(_metrics._reasonEnd == null ? null : _metrics._reasonEnd._val);
            eventBuilder.Append(_metrics._player._decodedLenth.ToString()).Append(_metrics._player._size.ToString());
            eventBuilder.Append(when.ToString()).Append(when.ToString());
            eventBuilder.Append(_metrics._player._duration.ToString());
            eventBuilder.Append(_metrics._player._decryptTime.ToString())
                .Append(_metrics._player._fadeOverlap.ToString()).Append("0").Append("0");
            eventBuilder.Append(_metrics.FirstValue() == 0 ? "0" : "1").Append(_metrics.FirstValue().ToString());
            eventBuilder.Append("0").Append("-1").Append("context");
            eventBuilder.Append(_metrics._player._contentMetrics.AudioKeyTime.ToString()).Append("0");
            eventBuilder.Append(_metrics._player._contentMetrics.PreloadedAudioKey ? "1" : "0").Append("0").Append("0")
                .Append("0");
            eventBuilder.Append(when.ToString()).Append(when.ToString());
            eventBuilder.Append("0").Append(_metrics._player._bitrate.ToString());
            eventBuilder.Append(_metrics._contextUri).Append(_metrics._player._encoding);
            eventBuilder.Append(_metrics._id.HasGid() ? _metrics._id.HexId() : "").Append("");
            eventBuilder.Append("0").Append(_metrics._referrerIdentifier).Append(_metrics._featureVersion);
            eventBuilder.Append("com.spotify").Append(_metrics._player._transition).Append("none");
            eventBuilder.Append(_lastCommandSentByDeviceId).Append("na").Append("none");
            return eventBuilder;
        }
    }
}