using System;
using System.Collections.Generic;
using lib.common;
using Newtonsoft.Json.Linq;
using spotify.player.proto;

namespace lib.json
{
    public class StationsWrapper : JsonWrapper
    {
        public StationsWrapper(JObject obj) : base(obj) { }

        public String Uri()
        {
            return Obj.TryGetValue("uri", out var value) ? value.ToObject<String>() : null;
        }

        public List<ContextTrack> Tracks()
        {
            return ProtoUtils.JsonToContextTracks(Obj.TryGetValue("tracks", out var value) ? value.ToObject<JArray>() : null);
        }
    }
}