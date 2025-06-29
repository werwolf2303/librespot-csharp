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
            if(!Obj.ContainsKey("uri")) return null;
            return Obj["uri"].ToObject<string>();
        }

        public List<ContextTrack> Tracks()
        {
            if(!Obj.ContainsKey("tracks")) return new List<ContextTrack>();
            return ProtoUtils.JsonToContextTracks(Obj["tracks"].ToObject<JArray>());
        }
    }
}