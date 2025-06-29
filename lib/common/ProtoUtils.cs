using System;
using System.Collections.Generic;
using lib.metadata;
using Newtonsoft.Json.Linq;
using spotify.player.proto;

namespace lib.common
{
    public class ProtoUtils
    {
        public static ContextTrack JsonToContextTrack(JObject obj)
        {
            ContextTrack track = new ContextTrack();

            String uri = Utils.OptionalJSON<string>(obj, "uri", null);
            if (uri != null) {
                track.Uri = uri;

                IPlayableId playable = PlayableId.FromUri(uri);
                if (playable.HasGid()) track.Gid = playable.GetGid();
            }

            track.Uid = Utils.OptionalJSON<string>(obj, "uid", null);

            if (obj.ContainsKey("metadata"))
            {
                JObject metadata = obj["metadata"].ToObject<JObject>();
                foreach (KeyValuePair<string, JToken> entry in metadata) 
                    track.Metadatas.Add(entry.Key, entry.Value.ToObject<string>());
            }

            return track;
        }
        
        public static List<ContextTrack> JsonToContextTracks(JArray array)
        {
            List<ContextTrack> list = new List<ContextTrack>(array.Count);
            foreach (JToken elm in array) list.Add(JsonToContextTrack(elm as JObject));
            return list;
        }
        
        public static ContextPage JsonToContextPage(JObject obj)
        {
            ContextPage page = new ContextPage
            {
                NextPageUrl = Utils.OptionalJSON<string>(obj, "next_page_url", null),
                PageUrl = Utils.OptionalJSON<string>(obj, "page_url", null),
            };

            if (obj.ContainsKey("tracks"))
            {
                page.Tracks.AddRange(JsonToContextTracks(obj["tracks"] as JArray));
            }

            return page;
        }
        
        public static List<ContextPage> JsonToContextPages(JArray array) {
            List<ContextPage> list = new List<ContextPage>(array.Count);
            foreach (JToken elm in array)
            {
                list.Add(JsonToContextPage(elm as JObject));
            }
            return list;
        }
    }
}