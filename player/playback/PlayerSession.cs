using System;
using System.Collections.Generic;
using lib.audio;
using lib.common;
using lib.core;
using lib.metadata;
using log4net;
using player.crossfade;
using player.metrics;
using player.mixing;

namespace player.playback
{
    public class PlayerSession : IDisposable, PlayerQueueEntry.IListener
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(PlayerSession));
        private ScheduledExecutorService _executorService;
        private Session _session;
        private AudioSink _sink;
        private PlayerConfiguration _conf;
        private String _sessionId;
        private IListener _listener;
        private PlayerQueue _queue;
        private int _lastPlayPos = 0;
        private PlaybackMetrics.Reason _lastPlayReason;
        private volatile bool _closed = false;

        public PlayerSession(Session session, AudioSink sink, PlayerConfiguration conf, String sessionId,
            IListener listener)
        {
            _session = session;
            _sink = sink;
            _conf = conf;
            _sessionId = sessionId;
            _listener = listener;
            _executorService = _session.GetScheduledExecutorService();
            _queue = new PlayerQueue();
            LOGGER.Info("Created new session. (id: " + _sessionId + ")");
            
            _sink.ClearOutputs();
        }

        private void Add(IPlayableId playable, bool preloaded)
        {
            PlayerQueueEntry entry = new PlayerQueueEntry(_sink, _session, _conf, playable, preloaded, this);
            _queue.Add(entry);
            if (_queue.Next() == entry)
            {
                PlayerQueueEntry head = _queue.Head();
                if (head != null && head.Crossfade != null)
                {
                    bool customFade = entry.Playable.Equals(head.Crossfade.FadeOutPlayable());
                    CrossfadeController.FadeInterval fadeOut;
                    if ((fadeOut = head.Crossfade.SelectFadeOut(PlaybackMetrics.Reason.TrackDone, customFade)) != null) 
                        head.NotifyInstant(PlayerQueueEntry.INSTANT_START_NEXT, fadeOut.Start());
                }
            }
        }

        private void AddNext()
        {
            IPlayableId playable = _listener.NextPlayableDoNotSet();
            if (playable != null) Add(playable, true);
        }
        
        private bool AdvanceTo(IPlayableId id)
        {
            do
            {
                PlayerQueueEntry entry = _queue.Head();
                if (entry == null) return false;
                if (entry.Playable.Equals(id))
                {
                    PlayerQueueEntry next = _queue.Next();
                    if (next == null || !next.Playable.Equals(id))
                        return true;
                }
            } while (_queue.Advance());

            return false;
        }

        private void Advance(PlaybackMetrics.Reason reason)
        {
            if (_closed) return;

            IPlayableId next = _listener.NextPlayable();
            if (next == null)
                return;

            EntryWithPos entry = PlayInternal(next, 0, reason);
            _listener.TrackChanged(entry._entry.PlaybackId, entry._entry.Metadata(), entry._pos, reason);
        }

        public void InstantReached(PlayerQueueEntry entry, int callbackId, int exactTime)
        {
            switch (callbackId)
            {
                case PlayerQueueEntry.INSTANT_PRELOAD:
                    if (entry == _queue.Head()) _executorService.schedule(new ScheduledExecutorService.ScheduledFuture<int>(
                        () =>
                        {
                            AddNext();
                            return 0;
                        }, 100, ScheduledExecutorService.TimeUnit.MILLISECONDS));
                    break;
                case PlayerQueueEntry.INSTANT_START_NEXT:
                    _executorService.schedule(new ScheduledExecutorService.ScheduledFuture<int>(
                        () =>
                        {
                            Advance(PlaybackMetrics.Reason.TrackDone);
                            return 0;
                        }, 100, ScheduledExecutorService.TimeUnit.MILLISECONDS));
                    break;
                case PlayerQueueEntry.INSTANT_END:
                    entry.Dispose();
                    break;
                default:
                    throw new InvalidOperationException("Unknown callback: " + callbackId);
            }
        }

        public void PlaybackEnded(PlayerQueueEntry entry)
        {
            _listener.TrackPlayed(entry.PlaybackId, entry.EndReason, entry.Metrics(), entry.GetTimeNoThrow());
            
            if (entry == _queue.Head()) 
                Advance(PlaybackMetrics.Reason.TrackDone);
        }

        public void StartedLoading(PlayerQueueEntry entry)
        {
            if (entry == _queue.Head()) _listener.StartedLoading();
        }

        public void LoadingError(PlayerQueueEntry entry, Exception ex, bool retried)
        {
            if (entry == _queue.Head())
            {
                if (ex is PlayableContentFeeder.ContentRestrictedException)
                {
                    Advance(PlaybackMetrics.Reason.TrackError);
                } else if (!retried)
                {
                    PlayerQueueEntry newEntry = entry.RetrySelf(false);
                    _executorService.schedule(new ScheduledExecutorService.ScheduledFuture<int>(() =>
                    {
                        _queue.Swap(entry, newEntry);
                        PlayInternal(newEntry.Playable, _lastPlayPos,
                            _lastPlayReason == null ? PlaybackMetrics.Reason.TrackError : _lastPlayReason);
                        return 0;
                    }, 100, ScheduledExecutorService.TimeUnit.MILLISECONDS));
                }
            } else if (entry == _queue.Next())
            {
                if (!(ex is PlayableContentFeeder.ContentRestrictedException) && !retried)
                {
                    PlayerQueueEntry newEntry = entry.RetrySelf(false);
                    _executorService.schedule(new ScheduledExecutorService.ScheduledFuture<int>(() =>
                    {
                        _queue.Swap(entry, newEntry);
                        return 0;
                    }, 100, ScheduledExecutorService.TimeUnit.MILLISECONDS));
                }
            }
            
            _queue.Remove(entry);
        }

        public void FinishedLoading(PlayerQueueEntry entry, MetadataWrapper metadata)
        {
            if (entry == _queue.Head()) _listener.FinishedLoading(metadata);
        }

        public Dictionary<String, String> MetadataFor(IPlayableId playable)
        {
            return _listener.MetadataFor(playable);
        }

        public void PlaybackError(PlayerQueueEntry entry, Exception ex)
        {
            if (entry == _queue.Head()) _listener.PlaybackError(ex);
            _queue.Remove(entry);
        }

        public void PlaybackHalted(PlayerQueueEntry entry, int chunk)
        {
            if (entry == _queue.Head()) _listener.PlaybackHalted(chunk);
        }

        public void PlaybackResumed(PlayerQueueEntry entry, int chunk, int diff)
        {
            if (entry == _queue.Head()) _listener.PlaybackResumedFromHalt(chunk, diff);
        }
        
        // ================================ //
        // =========== Playback =========== //
        // ================================ //

        private EntryWithPos PlayInternal(IPlayableId playable, int pos, PlaybackMetrics.Reason reason)
        {
            _lastPlayPos = pos;
            _lastPlayReason = reason;

            if (!AdvanceTo(playable))
            {
                Add(playable, false);
                _queue.Advance();
            }
            
            PlayerQueueEntry head = _queue.Head();
            if (head == null) throw new InvalidOperationException();

            bool customFade = false;
            if (head.Prev != null)
            {
                head.Prev.EndReason = reason;
                if (head.Prev.Crossfade == null)
                {
                    head.Prev.Dispose();
                    customFade = false;
                }
                else
                {
                    customFade = head.Playable.Equals(head.Prev.Crossfade.FadeOutPlayable());
                    CrossfadeController.FadeInterval fadeOut;
                    if (head.Prev.Crossfade == null ||
                        (fadeOut = head.Prev.Crossfade.SelectFadeOut(reason, customFade)) == null)
                    {
                        head.Prev.Dispose();
                    }
                    else
                    {
                        if (fadeOut is CrossfadeController.PartialFadeInterval)
                        {
                            int time = head.Prev.GetTime();
                            head.Prev.NotifyInstant(PlayerQueueEntry.INSTANT_END,
                                ((CrossfadeController.PartialFadeInterval)fadeOut).End(time));
                        }
                        else
                        {
                            head.Prev.NotifyInstant(PlayerQueueEntry.INSTANT_END, fadeOut.End());
                        }
                    }
                }
            }
            
            MixingLine.MixingOutput output = _sink.SomeOutput();
            if (output == null) throw new InvalidOperationException();

            CrossfadeController.FadeInterval fadeIn;
            if (head.Crossfade != null && (fadeIn = head.Crossfade.SelectFadeIn(reason, customFade)) != null)
            {
                head.Seek(pos = fadeIn.Start());
            }
            else
            {
                head.Seek(pos);
            }
            
            head.SetOutput(output);
            LOGGER.DebugFormat("{0} has been added to the output. (sessionId: {1}, pos: {2}, reason: {3})", head,
                _sessionId, pos, reason);
            return new EntryWithPos(head, pos);
        }

        public String Play(IPlayableId playable, int pos, PlaybackMetrics.Reason reason)
        {
            return PlayInternal(playable, pos, reason)._entry.PlaybackId;
        }

        public void SeekCurrent(int pos)
        {
            if (_queue.Head() == null) return;

            PlayerQueueEntry entry;
            if ((entry = _queue.Prev()) != null && entry.HasOutput()) _queue.Remove(entry);
            if ((entry = _queue.Next()) != null && entry.HasOutput()) _queue.Remove(entry);
            
            _queue.Head().Seek(pos);
        }

        public PlayerMetrics CurrentMetrics()
        {
            if (_queue.Head() == null) return null;
            else return _queue.Head().Metrics();
        }

        public MetadataWrapper CurrentMetadata()
        {
            if (_queue.Head() == null) return null;
            else return _queue.Head().Metadata();
        }

        public int CurrentTime()
        {
            if (_queue.Head() == null) return -1;
            else return _queue.Head().GetTime();
        }

        public String CurrentPlaybackId()
        {
            if (_queue.Head() == null) return null;
            else return _queue.Head().PlaybackId;
        }

        public String SessionId()
        {
            return _sessionId;
        }

        public void Dispose()
        {
            _closed = true;
            _queue.Dispose();
        }

        public interface IListener
        {
            IPlayableId CurrentPlayable();
            
            IPlayableId NextPlayable();

            IPlayableId NextPlayableDoNotSet();

            Dictionary<String, String> MetadataFor(IPlayableId playableId);

            void PlaybackHalted(int chunk);

            void PlaybackResumedFromHalt(int chunk, long diff);

            void StartedLoading();

            void LoadingError(Exception ex);

            void FinishedLoading(MetadataWrapper metadata);
            
            void PlaybackError(Exception ex);

            void TrackChanged(String playbackId, MetadataWrapper metadata, int pos,
                PlaybackMetrics.Reason startedReason);

            void TrackPlayed(String playbackId, PlaybackMetrics.Reason endReason, PlayerMetrics playerMetrics,
                int endedAt);
        }

        private class EntryWithPos
        {
            internal PlayerQueueEntry _entry;
            internal int _pos;

            internal EntryWithPos(PlayerQueueEntry entry, int pos)
            {
                _entry = entry;
                _pos = pos;
            }
        }
    }
}