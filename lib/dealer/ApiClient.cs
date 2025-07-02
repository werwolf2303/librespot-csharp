using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using com.spotify.canvazcache;
using Connectstate;
using lib.common;
using lib.core;
using lib.json;
using lib.mercury;
using lib.metadata;
using log4net;
using log4net.Util;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities;
using ProtoBuf;
using spotify.clienttoken.http.v0;
using spotify.extendedmetadata.proto;
using spotify.metadata.proto;
using spotify.playlist4.proto;
using Artist = spotify.metadata.proto.Artist;
using HttpResponse = EasyHttp.Http.HttpResponse;

namespace lib.dealer
{
    public class ApiClient
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(ApiClient));
        private Session _session;
        private String _baseUrl;
        private String _clientToken;

        public enum RequestMethod
        {
            GET,
            POST,
            PUT
        };

        public ApiClient(Session session)
        {
            _session = session;
            _baseUrl = "https://" + session.GetAPResolver().getRandomSpclient();
        }

        private HttpResponse Send(RequestMethod method, String suffix, Headers headers = null, byte[] body = null,
            String requestBodyType = "application/x-protobuf")
        {
            if (_clientToken == null)
            {
                ClientTokenResponse resp = ClientToken();
                _clientToken = resp.GrantedToken.Token;
                LOGGER.Debug("Updated client token: " + _clientToken);
            }

            if (headers == null)
            {
                headers = new Headers.Builder().Build();
            }
            headers.Add("Authorization", "Bearer " + _session.GetTokens().Get("playlist-read"));
            headers.Add("client-token", _clientToken);

            foreach (String key in headers.Keys) 
                _session.GetClient().Request.RawHeaders.Add(key, headers[key]);
            
            HttpResponse response;
            switch (method)
            {
                default:
                case RequestMethod.GET:
                    response = _session.GetClient().Get(_baseUrl + suffix);
                    break;
                case RequestMethod.POST:
                    response = _session.GetClient().Post(_baseUrl + suffix, body, requestBodyType);
                    break;
                case RequestMethod.PUT:
                    response = _session.GetClient().Put(_baseUrl + suffix, body, requestBodyType);
                    break;
            }
            
            _session.GetClient().Request.RawHeaders.Clear();

            if ((int) response.StatusCode == 503)
            {
                throw new StatusCodeException(response);
            }

            return response;
        }

        public void SendAsync(AsyncRequestResponse asyncRequestResponse, RequestMethod method, String suffix, Headers headers = null, byte[] body = null,
            String requestBodyType = "application/x-protobuf")
        {
            Thread thread = new Thread(() =>
            {
                asyncRequestResponse(Send(method, suffix, headers, body, requestBodyType));
            });
            thread.Name = "ApiClient async request";
            thread.Start();
        }
        
        public void PutConnectState(String connectionId, PutStateRequest proto)
        {
            HttpResponse response = Send(RequestMethod.PUT, "/connect-state/v1/devices/" + _session.GetDeviceId(),
                new Headers.Builder()
                    .Add("X-Spotify-Connection-Id", connectionId).Build(), Utils.ProtoBytes(proto));
            if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
            {
                LOGGER.WarnFormat("PUT state payload too large: {0} bytes uncompressed", Utils.ProtoBytes(proto).Length);
            }else if (response.StatusCode != HttpStatusCode.OK)
            {
                LOGGER.WarnFormat("PUT state returned {0}. (headers: {1})", (int)response.StatusCode, response.RawHeaders);
            }
        }

        public Track GetMetadata4Track(TrackId track)
        {
            HttpResponse resp = Send(RequestMethod.GET, "/metadata/4/track/" + track.HexId());
            StatusCodeException.CheckStatus(resp);
            return Serializer.Deserialize<Track>(resp.ResponseStream);
        }

        public Episode GetMetadata4Episode(EpisodeId episode)
        {
            HttpResponse resp = Send(RequestMethod.GET, "/metadata/4/episode/" + episode.HexId());
            StatusCodeException.CheckStatus(resp);
            return Serializer.Deserialize<Episode>(resp.ResponseStream);
        }

        public Album GetMetadata4Album(AlbumId album)
        {
            HttpResponse resp = Send(RequestMethod.GET, "/metadata/4/album/" + album.HexId());
            StatusCodeException.CheckStatus(resp);
            return Serializer.Deserialize<Album>(resp.ResponseStream);
        }

        public Artist GetMetadata4Artist(ArtistId artist)
        {
            HttpResponse resp = Send(RequestMethod.GET, "/metadata/4/artist/" + artist.HexId());
            StatusCodeException.CheckStatus(resp);
            return Serializer.Deserialize<Artist>(resp.ResponseStream);
        }

        public Show GetMetadata4Show(ShowId show)
        {
            HttpResponse resp = Send(RequestMethod.GET, "/metadata/4/show/" + show.HexId()); 
            StatusCodeException.CheckStatus(resp);
            return Serializer.Deserialize<Show>(resp.ResponseStream);
        }

        public EntityCanvazResponse GetCanvases(EntityCanvazRequest req)
        {
            HttpResponse resp = Send(RequestMethod.POST, "/canvaz-cache/v0/canvases", null, Utils.ProtoBytes(req));
            StatusCodeException.CheckStatus(resp);
            return Serializer.Deserialize<EntityCanvazResponse>(resp.ResponseStream);
        }

        public BatchedExtensionResponse GetExtendedMetadata(BatchedEntityRequest req)
        {
            HttpResponse resp = Send(RequestMethod.POST, "/extended-metadata/v0/extended-metadata", null,
                Utils.ProtoBytes(req));
            StatusCodeException.CheckStatus(resp);
            return Serializer.Deserialize<BatchedExtensionResponse>(resp.ResponseStream);
        }

        public SelectedListContent GetPlaylist(PlaylistId playlistId)
        {
            HttpResponse resp = Send(RequestMethod.GET, "/playlist/v2/playlist/" + playlistId.HexId());
            StatusCodeException.CheckStatus(resp);
            return Serializer.Deserialize<SelectedListContent>(resp.ResponseStream);
        }

        public JObject GetUserProfile(String id, int playlistLimit, int artistLimit)
        {
            StringBuilder url = new StringBuilder();
            url.Append("(/user-profile-view/v3/profile/");
            url.Append(id);

            if (playlistLimit != null || artistLimit != null)
            {
                url.Append("?");

                if (playlistLimit != null)
                {
                    url.Append("playlist_limit=");
                    url.Append(playlistLimit);
                    if (artistLimit != null)
                        url.Append("&");
                }

                if (artistLimit != null)
                {
                    url.Append("artist_limit=");
                    url.Append(artistLimit);
                }
            }
            
            HttpResponse resp = Send(RequestMethod.GET, url.ToString());
            StatusCodeException.CheckStatus(resp);
            return JObject.Parse(resp.RawText);
        }

        public JObject GetUserFollowers(String id)
        {
            HttpResponse resp = Send(RequestMethod.GET, "/user-profile-view/v3/profile/" + id + "/followers");
            StatusCodeException.CheckStatus(resp);
            return JObject.Parse(resp.RawText);
        }

        public JObject GetUserFollowing(String id)
        {
            HttpResponse resp = Send(RequestMethod.GET, "/user-profile-view/v3/profile/" + id + "/following");
            StatusCodeException.CheckStatus(resp);
            return JObject.Parse(resp.RawText);
        }

        public JObject GetRadioForTrack(PlayableId id)
        {
            HttpResponse resp = Send(RequestMethod.GET,
                "/inspiredby-mix/v2/seed_to_playlist/" + id.ToSpotifyUri() + "?response-format=json");
            StatusCodeException.CheckStatus(resp);
            return JObject.Parse(resp.RawText);
        }

        public StationsWrapper GetApolloStation(String context, List<String> prevTracks, int count, bool autoplay)
        {
            StringBuilder prevTracksStr = new StringBuilder();
            for (int i = 0; i < prevTracks.Count; i++)
            {
                if (i != 0) prevTracksStr.Append(",");
                prevTracksStr.Append(prevTracks[i]);
            }
            
            HttpResponse resp = Send(RequestMethod.GET, String.Format("/radio-apollo/v3/stations/{0}?count={1}&prev_tracks={2}&autoplay={3}",
                context, count, prevTracksStr, autoplay));
            StatusCodeException.CheckStatus(resp);
            return new StationsWrapper(JObject.Parse(resp.RawText));
        }

        private ClientTokenResponse ClientToken()
        {
            ClientTokenRequest protoReq = new ClientTokenRequest
            {
                RequestType = ClientTokenRequestType.RequestClientDataRequest,
                ClientData =
                {
                    ClientId = MercuryRequests.KEYMASTER_CLIENT_ID,
                    ClientVersion = Version.versionNumber(),
                    ConnectivitySdkData =
                    {
                        DeviceId = _session.GetDeviceId(),
                        PlatformSpecificData =
                        {
                            Windows =
                            {
                                Something1 = 10,
                                Something3 = 21370,
                                Something4 = 2,
                                Something6 = 9,
                                Something7 = 332,
                                Something8 = 34404,
                                Something10 = true
                            }
                        }
                    }
                }
            };
            
            _session.GetClient().Request.Accept = "application/x-protobuf";
            _session.GetClient().Request.ContentEncoding = "";

            HttpResponse resp = _session.GetClient().Post("https://clienttoken.spotify.com/v1/clienttoken",
                Utils.ProtoBytes(protoReq), "application/x-protobuf");
            
            StatusCodeException.CheckStatus(resp);

            _session.GetClient().Request.Accept = "";
            _session.GetClient().Request.ContentEncoding = "";

            return Serializer.Deserialize<ClientTokenResponse>(resp.ResponseStream);
        }

        public void SetClientToken(String clientToken)
        {
            _clientToken = clientToken;
        }

        public class StatusCodeException : IOException
        {
            public int code;

            internal StatusCodeException(HttpResponse resp) :
                base(String.Format("{0}: {1}", (int)resp.StatusCode, resp.RawText))
            {
                code = (int) resp.StatusCode;
            }

            internal static void CheckStatus(HttpResponse resp)
            {
                if (resp.ResponseStream == null) throw new StatusCodeException(resp);
                if ((int) resp.StatusCode != 200) throw new StatusCodeException(resp);
            }
        }

        public class Headers : Dictionary<String, String>
        {
            private Headers(Dictionary<String, String> headers)
            {
                foreach (String key in headers.Keys) 
                    Add(key, headers[key]);
            }
            
            public class Builder
            {
                private Dictionary<String, String> _headers = new Dictionary<string, string>();

                public Builder Add(String key, String value)
                {
                    _headers.Add(key, value);
                    return this;
                }

                public Headers Build()
                {
                    return new Headers(_headers);
                }
            }
        }
        
        public delegate void AsyncRequestResponse(HttpResponse resp, StatusCodeException optionalException = null);
    }
}