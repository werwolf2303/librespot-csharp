using System;
using System.Collections.Generic;
using lib.common;
using Newtonsoft.Json.Linq;
using spotify.player.proto;

namespace lib.json
{
    public class ResolvedContextWrapper : JsonWrapper
    {
        public ResolvedContextWrapper(JObject obj) : base(obj)
        {
        }

        public List<ContextPage> Pages()
        {
            if(!Obj.ContainsKey("pages")) return new List<ContextPage>();
            return ProtoUtils.JsonToContextPages(Obj["pages"].ToObject<JArray>());
        }

        public JObject Metadata()
        {
            if(!Obj.ContainsKey("metadata")) return null;
            return Obj["metadata"].ToObject<JObject>();
        }

        public String Uri()
        {
            if(!Obj.ContainsKey("uri")) return null;
            return Obj["uri"].ToObject<string>();
        }

        public String Url()
        {
            if(!Obj.ContainsKey("url")) return null;
            return Obj["url"].ToObject<string>();
        }
    }
}