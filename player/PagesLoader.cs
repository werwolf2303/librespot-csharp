using System;
using System.Collections.Generic;
using System.IO;
using lib.common;
using lib.core;
using lib.mercury;
using lib.metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using spotify.player.proto;

namespace player
{
    public class PagesLoader
    {
        private List<ContextPage> _pages;
        private Session _session;
        private String _resolveUrl = null;
        private int _currentPage = -1;

        private PagesLoader(Session session)
        {
            _session = session;
            _pages = new List<ContextPage>();
        }

        public static PagesLoader From(Session session, String context)
        {
            PagesLoader loader = new PagesLoader(session);
            loader._resolveUrl = context;
            return loader;
        }

        public static PagesLoader From(Session session, Context context)
        {
            List<ContextPage> pages = context.Pages;
            if (pages.Count == 0) return From(session, context.Uri);

            PagesLoader loader = new PagesLoader(session);
            loader.PutFirstPages(pages, PlayableId.InferUriPrefix(context.Uri));
            return loader;
        }

        private static void SanitizeTracks(List<ContextTrack> tracks, String uriPrefix)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                ContextTrack track = tracks[i];
                if (track.Uri != "" || track.Gid == null) continue;

                track.Uri = uriPrefix + PlayableId.Base62.Encode(track.Gid);
                tracks[i] = track;
            }
        }

        private List<ContextTrack> FetchTracks(String url)
        {
            MercuryClient.Response resp = _session.GetMercury().SendSync(new RawMercuryRequest.Builder
            {
                Uri = url,
                Method = "GET",
            }.Build());

            StreamReader reader = new StreamReader(resp.Payload.Stream());
            
            return ProtoUtils.JsonToContextTracks(JObject.Parse(reader.ReadToEnd())["tracks"].ToObject<JArray>());
        }

        private List<ContextTrack> ResolvePage(ContextPage page)
        {
            if (page.Tracks.Count > 0)
            {
                return new List<ContextTrack>(page.Tracks);
            }

            if (page.PageUrl != "")
            {
                return FetchTracks(page.PageUrl);
            }

            if (page.Loading)
            {
                throw new Exception("Unsupported Operation! What does loading even mean?");
            }

            throw new Exception("Cannot load page, not enough information!");
        }

        private List<ContextTrack> GetPage(int index)
        {
            if (index == -1) throw new Exception("You must call NextPage() first!");

            if (index == 0 && _pages.Count == 0 && _resolveUrl != null)
                _pages.AddRange(_session.GetMercury().SendSync(
                    MercuryRequests.ResolveContext(_resolveUrl)).Pages());

            _resolveUrl = null;
            
            if (index < _pages.Count)
            { 
                ContextPage page = _pages[index];
                List<ContextTrack> tracks = ResolvePage(page);
                page.PageUrl = "";
                page.Tracks.Clear();
                page.Tracks.AddRange(tracks);
                _pages[index] = page;
                return tracks;
            }
            
            if (index > _pages.Count) throw new IndexOutOfRangeException();
            
            ContextPage prev = _pages[index - 1];
            if (prev.NextPageUrl == "") throw new Exception("Illegal state");
            
            String nextPageUrl = prev.NextPageUrl;
            _pages[index - 1].NextPageUrl = "";

            List<ContextTrack> pageTracks = FetchTracks(nextPageUrl);
            ContextPage newPage = new ContextPage();
            newPage.Tracks.AddRange(pageTracks);
            _pages.Add(newPage);

            return pageTracks;
        }

        internal List<ContextTrack> CurrentPage()
        {
            return GetPage(_currentPage);
        }

        internal bool NextPage()
        {
            try
            {
                GetPage(_currentPage + 1);
                _currentPage++;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        internal void PutFirstPages(List<ContextPage> pages, String contextUri)
        {
            if (_currentPage != -1 || _pages.Count != 0) throw new Exception("Illegal state");
            foreach (ContextPage page in _pages)
            {
                List<ContextTrack> tracks = new List<ContextTrack>(page.Tracks);
                SanitizeTracks(tracks, contextUri == null ? null : PlayableId.InferUriPrefix(contextUri));
                page.Tracks.Clear();
                page.Tracks.AddRange(tracks);
                pages.Add(page);
            }
        }

        internal void PutFirstPage(List<ContextTrack> tracks, String contextUri)
        {
            if (_currentPage != -1 || _pages.Count != 0) throw new Exception("Illegal state");
            SanitizeTracks(tracks, contextUri == null ? null : PlayableId.InferUriPrefix(contextUri));
            ContextPage newPage = new ContextPage();
            newPage.Tracks.AddRange(tracks);
            _pages.Add(newPage);
        }
    }
}