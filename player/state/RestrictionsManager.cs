using System;
using System.Runtime.CompilerServices;
using player.contexts;
using spotify.player.proto;
using Restrictions = Connectstate.Restrictions;

namespace player.state
{
    public class RestrictionsManager
    {
        public static String REASON_ENDLESS_CONTEXT = "endless_context";
        public static String REASON_NO_PREV_TRACK = "no_prev_track";
        public static String REASON_NO_NEXT_TRACK = "no_next_track";
        private Connectstate.Restrictions _restrictions;

        public RestrictionsManager(AbsSpotifyContext context)
        {
            _restrictions = new Connectstate.Restrictions();

            if (!context.IsFinite())
            {
                
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Restrictions ToProto()
        {
            return _restrictions;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Can(Action action)
        {
            switch (action)
            {
                case Action.SHUFFLE:
                    return _restrictions.DisallowTogglingShuffleReasons.Count == 0;
                case Action.REPEAT_CONTEXT:
                    return _restrictions.DisallowTogglingRepeatContextReasons.Count == 0;
                case Action.REPEAT_TRACK:
                    return _restrictions.DisallowTogglingRepeatTrackReasons.Count == 0;
                case Action.PAUSE:
                    return _restrictions.DisallowPausingReasons.Count == 0;
                case Action.RESUME:
                    return _restrictions.DisallowResumingReasons.Count == 0;
                case Action.SEEK:
                    return _restrictions.DisallowSeekingReasons.Count == 0;
                case Action.SKIP_PREV:
                    return _restrictions.DisallowSkippingPrevReasons.Count == 0;
                case Action.SKIP_NEXT:
                    return _restrictions.DisallowSkippingNextReasons.Count == 0;
                default:
                    throw new ArgumentException("Unknown restriction for " + action);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Allow(Action action)
        {
            switch (action) {
                case Action.SHUFFLE:
                    _restrictions.DisallowTogglingShuffleReasons.Clear();
                    break;
                case Action.REPEAT_CONTEXT:
                    _restrictions.DisallowTogglingRepeatContextReasons.Clear();
                    break;
                case Action.REPEAT_TRACK:
                    _restrictions.DisallowTogglingRepeatTrackReasons.Clear();
                    break;
                case Action.PAUSE:
                    _restrictions.DisallowPausingReasons.Clear();
                    break;
                case Action.RESUME:
                    _restrictions.DisallowResumingReasons.Clear();
                    break;
                case Action.SEEK:
                    _restrictions.DisallowSeekingReasons.Clear();
                    break;
                case Action.SKIP_PREV:
                    _restrictions.DisallowSkippingPrevReasons.Clear();
                    break;
                case Action.SKIP_NEXT:
                    _restrictions.DisallowSkippingNextReasons.Clear();
                    break;
                default:
                    throw new ArgumentException("Unknown restriction for " + action);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Disallow(Action action, String reason)
        {
            Allow(action);

            switch (action)
            {
                case Action.SHUFFLE:
                    _restrictions.DisallowTogglingShuffleReasons.Add(reason);
                    break;
                case Action.REPEAT_CONTEXT:
                    _restrictions.DisallowTogglingRepeatContextReasons.Add(reason);
                    break;
                case Action.REPEAT_TRACK:
                    _restrictions.DisallowTogglingRepeatTrackReasons.Add(reason);
                    break;
                case Action.PAUSE:
                    _restrictions.DisallowPausingReasons.Add(reason);
                    break;
                case Action.RESUME:
                    _restrictions.DisallowResumingReasons.Add(reason);
                    break;
                case Action.SEEK:
                    _restrictions.DisallowSeekingReasons.Add(reason);
                    break;
                case Action.SKIP_PREV:
                    _restrictions.DisallowSkippingPrevReasons.Add(reason);
                    break;
                case Action.SKIP_NEXT:
                    _restrictions.DisallowSkippingNextReasons.Add(reason);
                    break;
                default:
                    throw new ArgumentException("Unknown restriction for " + action);
            }
        }

        public enum Action {
            SHUFFLE, REPEAT_CONTEXT, REPEAT_TRACK, 
            PAUSE, RESUME, SEEK, SKIP_PREV, SKIP_NEXT
        }
    }
}