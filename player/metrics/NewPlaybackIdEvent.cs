using System;
using lib.core;

namespace player.metrics
{
    public class NewPlaybackIdEvent : EventService.GenericEvent
    {
        private String _sessionId;
        private String _playbackId;

        public NewPlaybackIdEvent(String sessionId, String playbackId)
        {
            _sessionId = sessionId;
            _playbackId = playbackId;
        }
        
        public EventService.EventBuilder Build()
        {
            EventService.EventBuilder eventBuilder = new EventService.EventBuilder(EventService.Type.NewPlaybackId);
            eventBuilder.Append(_playbackId).Append(_sessionId).Append(TimeProvider.currentTimeMillis().ToString());
            return eventBuilder;
        }
    }
}