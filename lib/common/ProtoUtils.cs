using System;
using System.Collections.Generic;
using System.Text;
using Connectstate;
using lib.metadata;
using Newtonsoft.Json.Linq;
using spotify.metadata.proto;
using spotify.player.proto;
using ContextPlayerOptions = Connectstate.ContextPlayerOptions;
using PlayOrigin = Connectstate.PlayOrigin;

namespace lib.common
{
    public class ProtoUtils
    {
        private static JObject MapToJson(Dictionary<String, String> map)
        {
            JObject obj = new JObject();
            foreach (String key in map.Keys) obj[key] = map[key];
            return obj;
        }
        
        private static JToken TrackToJson(ContextTrack track, string uriPrefix)
        {
            JObject obj = new JObject();
            if (track.Uid != "") obj["uid"] = track.Uid;
            obj["metadata"] = MapToJson(track.Metadatas);
            if (track.Uri != "")
                obj["uri"] = track.Uri;
            else if (track.Gid != null && uriPrefix != null)
                obj["uri"] = uriPrefix + Encoding.UTF8.GetString(PlayableId.Base62.Encode(track.Gid)).Substring(0, 22);

            return obj;
        }

        private static JObject TrackToJson(ProvidedTrack track)
        {
            JObject obj = new JObject();
            obj["uri"] = track.Uri;
            obj["uid"] = track.Uid;
            obj["metadata"] = MapToJson(track.Metadatas);
            return obj;
        }
        
        public static JObject CraftContextStateCombo(PlayerState ps, List<ContextTrack> tracks)
        {
            JObject context = new JObject();
            context["uri"] = ps.ContextUri;
            context["url"] = ps.ContextUrl;
            context["metadata"] = MapToJson(ps.ContextMetadatas);

            JArray pages = new JArray(1);
            context["pages"] = pages;

            JObject page = new JObject();
            page["page_url"] = "";
            page["next_page_url"] = "";
            JArray tracksJson = new JArray(tracks.Count);
            foreach (ContextTrack t in tracks) tracksJson.Add(TrackToJson(t, PlayableId.InferUriPrefix(ps.ContextUri)));
            page["tracks"] = tracksJson;
            page["metadata"] = MapToJson(ps.ContextMetadatas);
            pages.Add(page);
            
            JObject state = new JObject();
            
            JObject options = new JObject();
            options["shuffling_context"] = ps.Options.ShufflingContext;
            options["repeating_context"] = ps.Options.RepeatingContext;
            options["repeating_track"] = ps.Options.RepeatingTrack;
            state["options"] = options;
            state["skip_to"] = new JObject();
            state["track"] = TrackToJson(ps.Track);
            
            JObject result = new JObject();
            result["context"] = context;
            result["state"] = state;
            return result;
        }
        
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

        public static PlayOrigin JsonToPlayOrigin(JObject obj)
        {
            PlayOrigin builder = new PlayOrigin();

            if (obj.ContainsKey("feature_identifier"))
                builder.FeatureIdentifier = obj["feature_identifier"].ToObject<String>();
            if (obj.ContainsKey("feature_version"))
                builder.FeatureVersion = obj["feature_version"].ToObject<String>();
            if (obj.ContainsKey("view_uri"))
                builder.ViewUri = obj["view_uri"].ToObject<String>();
            if (obj.ContainsKey("external_referrer"))
                builder.ExternalReferrer = obj["external_referrer"].ToObject<String>();
            if (obj.ContainsKey("referrer_identifier"))
                builder.ReferrerIdentifier = obj["referrer_identifier"].ToObject<String>();
            if (obj.ContainsKey("device_identifier"))
                builder.DeviceIdentifier = obj["device_identifier"].ToObject<String>();
            
            return builder;
        }

        public static ContextPlayerOptions JsonToPlayerOptions(JObject obj, ContextPlayerOptions old)
        {
            ContextPlayerOptions builder = old == null ? new ContextPlayerOptions() : old;

            if (obj != null)
            {
                if (obj.ContainsKey("repeating_context"))
                    builder.RepeatingContext = obj["repeating_context"].ToObject<bool>();
                if (obj.ContainsKey("repeating_track"))
                    builder.RepeatingTrack = obj["repeating_track"].ToObject<bool>();
                if (obj.ContainsKey("repeating_context"))
                    builder.RepeatingContext = obj["repeating_context"].ToObject<bool>();
            }

            return builder;
        }

        public static Context JsonToContext(JObject obj)
        {
            Context context = new Context();
            
            if (obj.ContainsKey("uri"))
                context.Uri = obj["uri"].ToObject<String>();
            if (obj.ContainsKey("url"))
                context.Url = obj["url"].ToObject<String>();

            JObject metadata = obj.TryGetValue("metadata", out var value) ? value.ToObject<JObject>() : null;
            if (metadata != null)
            {
                foreach (var property in metadata.Properties())
                    context.Metadatas.Add(property.Name, property.Value.ToObject<String>());
            }

            if (obj.ContainsKey("pages"))
            {
                foreach (JToken elm in obj["pages"].ToObject<JArray>()) 
                    context.Pages.Add(JsonToContextPage(elm as JObject));
            }

            return context;
        }

        public static PlayOrigin ConvertPlayOrigin(spotify.player.proto.PlayOrigin po)
        {
            if (po == null) return null;
            
            PlayOrigin builder = new PlayOrigin();
            
            if (po.FeatureIdentifier != "") 
                builder.FeatureIdentifier = po.FeatureIdentifier;
            if (po.FeatureVersion != "")
                builder.FeatureVersion = po.FeatureVersion;
            if (po.ViewUri != "")
                builder.ViewUri = po.ViewUri;
            if (po.ExternalReferrer != "")
                builder.ExternalReferrer = po.ExternalReferrer;
            if (po.ReferrerIdentifier != "") 
                builder.ReferrerIdentifier = po.ReferrerIdentifier;
            if (po.DeviceIdentifier != "")
                builder.DeviceIdentifier = po.DeviceIdentifier;
            
            if (po.FeatureClasses.Count > 0) 
                foreach (String feature in po.FeatureClasses)
                    builder.FeatureClasses.Add(feature);

            return builder;
        }

        public static int IndexOfTrackByUid(List<ContextTrack> tracks, String uid)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                if (Equals(tracks[i].Uid, uid))
                    return i;
            }

            return -1;
        }

        public static int IndexOfTrackByUri(List<ContextTrack> tracks, String uri)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                if (Equals(tracks[i].Uri, uri))
                    return i;
            }

            return -1;
        }

        public static bool IsQueued(ContextTrack track)
        {
            return bool.Parse(track.Metadatas.TryGetValue("is_queued", out string value) ? value : "false");
        }

        public static void EnrichTrack(ContextTrack subject, ContextTrack track)
        {
            if (track.Uri != "" && Equals(subject.Uri, track.Uri))
                throw new InvalidOperationException(subject.Uri + " is not " + track.Uri);
            
            foreach (String key in track.Metadatas.Keys)
                subject.Metadatas.Add(key, track.Metadatas[key]);
        }

        public static void EnrichTrack(ProvidedTrack subject, ContextTrack track)
        {
            if (track.Uri != "" && Equals(subject.Uri, track.Uri)) 
                throw new InvalidOperationException(subject.Uri + " is not " + track.Uri);
            
            foreach (String key in track.Metadatas.Keys)
                subject.Metadatas.Add(key, track.Metadatas[key]);
        }
        
        public static ProvidedTrack ToProvidedTrack(ContextTrack track, String contextUri)
        {
            if (track == null) return null;
            
            ProvidedTrack providedTrack = new ProvidedTrack();
            providedTrack.Provider = "context";
            if (track.Uid != "") providedTrack.Uid = track.Uid;
            if (track.Uri != "") providedTrack.Uri = track.Uri;
            else if (track.Gid != null)
            {
                String uriPrefix = PlayableId.InferUriPrefix(contextUri);
                providedTrack.Uri = uriPrefix + Encoding.UTF8.GetString(PlayableId.Base62.Encode(track.Gid));
            }
            
            providedTrack.AlbumUri = track.Metadatas.TryGetValue("album_uri", out string value) ? value : "";
            providedTrack.ArtistUri = track.Metadatas.TryGetValue("artist_uri", out value) ? value : "";
            
            foreach (String key in track.Metadatas.Keys) 
                providedTrack.Metadatas.Add(key, track.Metadatas[key]);
            
            return providedTrack;
        }
        
        public static void PutFilesAsMetadata(ProvidedTrack builder, List<AudioFile> files)
        {
            if (files.Count == 0) return;

            JArray formats = new JArray(files.Count);
            foreach (AudioFile file in files) 
                if (file.format != null) formats.Add(file.format);
            
            if (formats.Count > 0) builder.Metadatas.Add("available_file_formats", formats.ToString());
        }

        public static int GetTrackCount(Album album)
        {
            int total = 0;
            foreach (Disc disc in album.Discs) total += disc.Tracks.Count;
            return total;
        }

        public static List<ContextTrack> Join(List<ContextPage> pages)
        {
            if (pages.Count == 0) return new List<ContextTrack>();

            List<ContextTrack> tracks = new List<ContextTrack>();
            foreach (ContextPage page in pages)
                tracks.AddRange(page.Tracks);

            return tracks;
        }

        public static ContextPlayerOptions ConvertPlayerOptions(spotify.player.proto.ContextPlayerOptions options)
        {
            if (options == null) return null;
            
            ContextPlayerOptions builder = new ContextPlayerOptions();
            if (builder.RepeatingContext) builder.RepeatingContext = options.RepeatingContext;
            if (builder.RepeatingTrack) builder.RepeatingTrack = options.RepeatingTrack;
            if (builder.ShufflingContext) builder.ShufflingContext = options.ShufflingContext;

            return builder;
        }

        public static void CopyOverMetadata(ContextTrack from, ContextTrack to)
        {
            foreach (String key in from.Metadatas.Keys) 
                to.Metadatas.Add(key, from.Metadatas[key]);
        }
        
        public static void CopyOverMetadata(ContextTrack from, ProvidedTrack to)
        {
            foreach (String key in from.Metadatas.Keys) 
                to.Metadatas.Add(key, from.Metadatas[key]);
        }

        public static void CopyOverMetadata(JObject obj, PlayerState to)
        {
            foreach (var property in obj.Properties()) to.ContextMetadatas.Add(property.Name, property.Value.ToObject<string>());
        }

        public static void CopyOverMetadata(Context from, PlayerState to)
        {
            foreach (String key in from.Metadatas.Keys) to.ContextMetadatas.Add(key, from.Metadatas[key]);
        }

        public static bool TrackEquals(ContextTrack first, ContextTrack second)
        {
            if (first == null || second == null) return false;
            if (first == second) return true;
            
            if (first.Uri != "" && second.Uri != "") 
                return first.Uri.Equals(second.Uri);
            
            if (first.Gid != null && second.Gid != null) 
                return first.Gid.Equals(second.Gid);
            
            if (first.Uid != "" && second.Uid != "") 
                return first.Uid.Equals(second.Uid);

            return false;
        }

        public static int IndexOfTrack(List<ContextTrack> list, ContextTrack track)
        {
            for (int i = 0; i < list.Count; i++)
                if (TrackEquals(list[i], track)) return i;

            return -1;
        }

        public static bool IsTrack(ProvidedTrack track, Track metadata)
        {
            return track.Uri.Equals(PlayableId.From(metadata).ToSpotifyUri());
        }

        public static bool IsEpisode(ProvidedTrack track, Episode metadata)
        {
            return track.Uri.Equals(PlayableId.From(metadata).ToSpotifyUri());
        }

        public static String ToString(ContextTrack track)
        {
            return String.Format("ContextTrack(uri: {0}, uid: {1}, gid: {2})", track.Uri, track.Uid, track.Uid);
        }

        public static String ToString(ProvidedTrack track)
        {
            return String.Format("ProvidedTrack(uri: {0}, uid: {1}, gid: {2})", track.Uri, track.Uid, track.Uid);
        }

        public static String ToString(Episode metadata)
        {
            return String.Format("Metadata.Episode({0},{1})", Utils.bytesToHex(metadata.Gid),
                PlayableId.From(metadata).ToSpotifyUri());
        }
        
        public static String ToString(Track metadata)
        {
            return String.Format("Metadata.Track({0},{1})", Utils.bytesToHex(metadata.Gid),
                PlayableId.From(metadata).ToSpotifyUri());
        }
    }
}