using System;
using System.Collections.Generic;
using lib.metadata;
using log4net;
using Newtonsoft.Json.Linq;
using player.metrics;

namespace player.crossfade
{
    public class CrossfadeController
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(CrossfadeController));
        private String _playbackId;
        private int _trackDuration;
        private Dictionary<PlaybackMetrics.Reason, FadeInterval> _fadeOutMap = new Dictionary<PlaybackMetrics.Reason, FadeInterval>();
        private Dictionary<PlaybackMetrics.Reason, FadeInterval> _fadeInMap = new Dictionary<PlaybackMetrics.Reason, FadeInterval>();
        private int _defaultFadeDuration;
        private IPlayableId _fadeOutPlayable;
        private FadeInterval _fadeIn = null;
        private FadeInterval _fadeOut = null;
        private FadeInterval _activeInterval = null;
        private float _lastGain = 1;
        private int _fadeOverlap = 0;

        public CrossfadeController(String playbackId, int duration, Dictionary<String, String> metadata,
            PlayerConfiguration conf)
        {
            _playbackId = playbackId;
            _trackDuration = duration;
            _defaultFadeDuration = conf._crossfadeDuration;

            String fadeOutUri = metadata["audio.fade_out_uri"];
            _fadeOutPlayable = fadeOutUri == null ? null : PlayableId.FromUri(fadeOutUri);

            PopulateFadeIn(metadata);
            PopulateFadeOut(metadata);
            
            LOGGER.DebugFormat("Loaded crossfade intervals (id: {0}, in: {1}, out: {2}", playbackId, _fadeInMap, _fadeOutMap);
        }

        private static JArray GetFadeCurve(JArray curves)
        {
            JObject curve = curves[0].ToObject<JObject>();
            if (curve["start_pint"].ToObject<float>() != 0 || curve["end_pint"].ToObject<float>() != 1)
                throw new InvalidOperationException();
            
            return curve["fade_curve"].ToObject<JArray>();
        }

        private void PopulateFadeIn(Dictionary<string, string> metadata)
        {
            int fadeInDuration =
                int.Parse(metadata.TryGetValue("audio.fade_in_duration", out var value) ? value : "-1");
            int fadeInStartTime =
                int.Parse(metadata.TryGetValue("audio.fade_in_start_time", out var value2) ? value2 : "-1");

            JArray fadeInCurves = JObject
                .Parse(metadata.TryGetValue("audio.fade_in_curves", out var value3) ? value3 : "[]").ToObject<JArray>();
            if (fadeInCurves.Count > 1) throw new InvalidOperationException(fadeInCurves.ToString());

            if (fadeInDuration != 0 && fadeInCurves.Count > 0)
                _fadeInMap.Add(PlaybackMetrics.Reason.TrackDone,
                    new FadeInterval(fadeInStartTime, fadeInDuration,
                        LookupInterpolator.FromJson(GetFadeCurve(fadeInCurves))));
            else if (_defaultFadeDuration > 0)
                _fadeInMap.Add(PlaybackMetrics.Reason.TrackDone,
                    new FadeInterval(0, _defaultFadeDuration, new LinearIncreasingInterpolator()));

            int fwdFadeInStartTime = int.Parse(metadata.TryGetValue("audio.fwdbtn.fade_in_start_time", out var value4)
                ? value4
                : "-1");
            int fwdFadeInDuration =
                int.Parse(metadata.TryGetValue("audio.fwdbtn.fade_in_duration", out var value5) ? value5 : "-1");
            if (fwdFadeInDuration > 0)
                _fadeInMap.Add(PlaybackMetrics.Reason.ForwardBtn,
                    new FadeInterval(fwdFadeInStartTime, fwdFadeInDuration, new LinearIncreasingInterpolator()));

            int backFadeInStartTime = int.Parse(metadata.TryGetValue("audio.backbtn.fade_in_start_time", out var value6)
                ? value6
                : "-1");
            int backFadeInDuration =
                int.Parse(metadata.TryGetValue("audio.backbtn.fade_in_duration", out var value7) ? value7 : "-1");
            if (backFadeInDuration > 0)
                _fadeInMap.Add(PlaybackMetrics.Reason.BackBtn,
                    new FadeInterval(backFadeInStartTime, backFadeInDuration, new LinearIncreasingInterpolator()));
        }

        private void PopulateFadeOut(Dictionary<string, string> metadata)
        {
            int fadeOutDuration =
                int.Parse(metadata.TryGetValue("audio.fade_out_duration", out var value1) ? value1 : "-1");
            int fadeOutStartTime =
                int.Parse(metadata.TryGetValue("audio.fade_out_start_time", out var value2) ? value2 : "-1");
            JArray fadeOutCurves =
                JArray.Parse(metadata.TryGetValue("audio.fade_out_curves", out var value3) ? value3 : "[]");
            if (fadeOutCurves.Count > 1) throw new InvalidOperationException(fadeOutCurves.ToString());

            if (fadeOutDuration != 0 && fadeOutCurves.Count > 0)
                _fadeOutMap.Add(PlaybackMetrics.Reason.TrackDone,
                    new FadeInterval(fadeOutStartTime, fadeOutDuration,
                        LookupInterpolator.FromJson(GetFadeCurve(fadeOutCurves))));
            else if (_defaultFadeDuration > 0)
                _fadeOutMap.Add(PlaybackMetrics.Reason.TrackDone,
                    new FadeInterval(_trackDuration - _defaultFadeDuration, _defaultFadeDuration,
                        new LinearDecreasingInterpolator()));

            int backFadeOutDuration = int.Parse(metadata.TryGetValue("audio.backbtn.fade_out_duration", out var value4)
                ? value4
                : "-1");
            if (backFadeOutDuration > 0)
                _fadeOutMap.Add(PlaybackMetrics.Reason.BackBtn, new PartialFadeInterval(backFadeOutDuration, new LinearDecreasingInterpolator()));

            int fwdFadeOutDuration =
                int.Parse(metadata.TryGetValue("audio.fwdbtn.fade_out_duration", out var value6) ? value6 : "-1");
            if (fwdFadeOutDuration > 0)
                _fadeOutMap.Add(PlaybackMetrics.Reason.ForwardBtn, new PartialFadeInterval(fwdFadeOutDuration, new LinearDecreasingInterpolator()));
        }

        public float GetGain(int pos)
        {
            if (_activeInterval == null && _fadeIn == null && _fadeOut == null)
                return _lastGain;

            if (_activeInterval != null && _activeInterval.End() <= pos)
            {
                _lastGain = _activeInterval._interpolator.Last();

                if (_activeInterval == _fadeIn)
                {
                    _fadeIn = null;
                    LOGGER.Debug("Cleared fade in. (id: " + _playbackId + ")");
                } else if (_activeInterval == _fadeOut)
                {
                    _fadeOut = null;
                    LOGGER.Debug("Cleared fade out. (id: " + _playbackId + ")");
                }
                
                _activeInterval = null;
            }

            if (_activeInterval == null)
            {
                if (_fadeIn != null && pos >= _fadeIn._start && _fadeIn.End() >= pos)
                {
                    _activeInterval = _fadeIn;
                    _fadeOverlap += _fadeIn._duration;
                } else if (_fadeOut != null && pos >= _fadeOut._start && _fadeOut.End() >= pos)
                {
                    _activeInterval = _fadeOut;
                    _fadeOverlap += _fadeOut._duration;
                }
            }
            
            if (_activeInterval == null) return _lastGain;

            return _lastGain = _activeInterval.Interpolate(pos);
        }

        public FadeInterval SelectFadeIn(PlaybackMetrics.Reason reason, bool customFade)
        {
            if ((!customFade && _fadeOutPlayable != null) && reason == PlaybackMetrics.Reason.TrackDone)
            {
                _fadeIn = null;
                _activeInterval = null;
                LOGGER.Debug("Cleared fade in because custom fade doesn't apply. (id: " + _playbackId + ")");
                return null;
            }
            else
            {
                _fadeIn = _fadeInMap[reason];
                _activeInterval = null;
                LOGGER.DebugFormat("Changed fade in. (curr: {0}, custom: {1}, why: {2}, id: {3}", _fadeIn, customFade,
                    reason, _playbackId);
                return _fadeIn;
            }
        }

        public int FadeOutStartTimeMin()
        {
            int fadeOutStartTime = -1;
            foreach (FadeInterval interval in _fadeInMap.Values)
            {
                if (interval is PartialFadeInterval) continue;

                if (fadeOutStartTime == -1 || fadeOutStartTime > interval._start) 
                    fadeOutStartTime = interval._start;
            }

            if (fadeOutStartTime == -1) return _trackDuration;
            else return fadeOutStartTime;
        }

        public bool HasAnyFadeOut()
        {
            return _fadeOutMap.Count != 0;
        }

        public int FadeOverlap()
        {
            return _fadeOverlap;
        }

        public IPlayableId FadeOutPlayable()
        {
            return _fadeOutPlayable;
        }

        public class PartialFadeInterval : FadeInterval
        {
            private int _partialStart = -1;
            
            internal PartialFadeInterval(int duration, IGainInterpolator interpolator) : base(-1, duration, interpolator)
            {
            }

            public int Start()
            {
                if (_partialStart == -1) throw new InvalidOperationException();
                return _partialStart;
            }

            public int End(int now)
            {
                _partialStart = now;
                return End();
            }

            public int End()
            {
                if (_partialStart == -1) throw new InvalidOperationException();
                return _partialStart + _duration;
            }

            internal float Interpolate(int trackPos)
            {
                if (_partialStart == -1) throw new InvalidOperationException();
                return base.Interpolate(trackPos - 1 - _partialStart);
            }

            public override string ToString()
            {
                return "PartialFadeInterval(duration=" + _duration + ", interpolator=" + _interpolator + ")";
            }
        }

        public class FadeInterval
        {
            internal int _start;
            internal int _duration;
            internal IGainInterpolator _interpolator;

            internal FadeInterval(int start, int duration, IGainInterpolator interpolator)
            {
                _start = start;
                _duration = duration;
                _interpolator = interpolator;
            }

            public int End()
            {
                return _start + _duration;
            }

            public int Duration()
            {
                return _duration;
            }

            public int Start()
            {
                return _start;
            }

            internal float Interpolate(int trackPos)
            {
                float pos = ((float) trackPos - _start) / _duration;
                pos = Math.Min(pos, 1);
                pos = Math.Max(pos, 0);
                return _interpolator.Interpolate(pos);
            }

            public override string ToString()
            {
                return "FadeInterval(start=" + _start + ", duration=" + _duration + ", interpolator=" + _interpolator + ")";
            }
        }
    }
}