using System;
using lib.core;

namespace player.metrics
{
    public class NewSessionIdEvent : EventService.GenericEvent
    {
        private String _sessionId;
        private StateWrapper _state;

        public NewSessionIdEvent(String sessionId, StateWrapper state)
        {
            _sessionId = sessionId;
            _state = state;
        }
        
        public EventService.EventBuilder Build()
        {
            String contextUri = _state.GetContextUri();

            EventService.EventBuilder eventBuilder = new EventService.EventBuilder(EventService.Type.NewSessionId);
            eventBuilder.Append(_sessionId);
            eventBuilder.Append(contextUri);
            eventBuilder.Append(contextUri);
            eventBuilder.Append(TimeProvider.currentTimeMillis().ToString());
            eventBuilder.Append("").Append(_state.GetContextSize().ToString());
            eventBuilder.Append(_state.GetContextUrl());
            return eventBuilder;
        }
    }
}