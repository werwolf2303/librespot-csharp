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
            return ProtoUtils.JsonToContextPages(Obj.TryGetValue("pages", out var value) ? value.ToObject<JArray>() : null);
        }

        public JObject Metadata()
        {
            return Obj.TryGetValue("metadata", out var value) ? value.ToObject<JObject>() : null;
        }

        public String Uri()
        {
            return Obj.TryGetValue("uri", out var value) ? value.ToObject<String>() : null;
        }

        public String Url()
        {
            return Obj.TryGetValue("url", out var value) ? value.ToObject<String>() : null;
        }
    }
}