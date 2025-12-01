using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using com.spotify.canvazcache;
using Connectstate;
using deps.HttpSharp;
using lib.common;
using lib.core;
using lib.json;
using lib.mercury;
using lib.metadata;
using log4net;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using spotify.clienttoken.data.v0;
using spotify.clienttoken.http.v0;
using spotify.extendedmetadata.proto;
using spotify.metadata.proto;
using spotify.playlist4.proto;
using Artist = spotify.metadata.proto.Artist;

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
            PUT,
            OPTIONS
        };

        public ApiClient(Session session)
        {
            _session = session;
            _baseUrl = "https://" + session.GetAPResolver().getRandomSpclient();
        }

        public HttpResponse Send(RequestMethod method, String suffix, Headers headers = null, byte[] body = null,
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

            HttpRequest request;
            switch (method)
            {
                default:
                case RequestMethod.GET:
                    request = new HttpRequest(_baseUrl + suffix, HttpMethod.Get);
                    break;
                case RequestMethod.POST:
                    request = new HttpRequest(_baseUrl + suffix, HttpMethod.Post);
                    request.SetData(body);
                    request.ContentEncoding = requestBodyType;
                    break;
                case RequestMethod.PUT:
                    request = new HttpRequest(_baseUrl + suffix, HttpMethod.Put);
                    request.SetData(body);
                    request.ContentEncoding = requestBodyType;
                    break;
                case RequestMethod.OPTIONS:
                    request = new HttpRequest(_baseUrl + suffix, HttpMethod.Options);
                    break;
            }

            request.Authorization = "Bearer " + _session.GetTokens().Get();
            headers.Add("client-token", _clientToken);
            
            request.ContentType = "application/x-protobuf";
            
            foreach (String key in headers.Keys) 
                request.ExtraHeaders.Add(key, headers[key]);

            HttpResponse response = _session.GetClient().NewCall(request);

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
                LOGGER.WarnFormat("PUT state returned {0}. (headers: {1})", (int)response.StatusCode, response.Headers);
            }
        }

        public Track GetMetadata4Track(TrackId track)
        {
            BatchedExtensionResponse response = GetExtendedMetadata(ExtensionKind.TrackV4, track);
            
            CheckExtendedMetadataResponse(response);
            
            return Serializer.Deserialize<Track>(new MemoryStream(response.ExtendedMetadatas[0].ExtensionDatas[0].ExtensionData.Value));
        }

        public Episode GetMetadata4Episode(EpisodeId episode)
        {
            BatchedExtensionResponse response = GetExtendedMetadata(ExtensionKind.EpisodeV4, episode);
            
            CheckExtendedMetadataResponse(response);
            
            return Serializer.Deserialize<Episode>(new MemoryStream(response.ExtendedMetadatas[0].ExtensionDatas[0].ExtensionData.Value));

        }

        public Album GetMetadata4Album(AlbumId album)
        {
            BatchedExtensionResponse response = GetExtendedMetadata(ExtensionKind.AlbumV4, album);
            
            CheckExtendedMetadataResponse(response);
            
            return Serializer.Deserialize<Album>(new MemoryStream(response.ExtendedMetadatas[0].ExtensionDatas[0].ExtensionData.Value));

        }

        public Artist GetMetadata4Artist(ArtistId artist)
        {
            BatchedExtensionResponse response = GetExtendedMetadata(ExtensionKind.ArtistV4, artist);
            
            CheckExtendedMetadataResponse(response);
            
            return Serializer.Deserialize<Artist>(new MemoryStream(response.ExtendedMetadatas[0].ExtensionDatas[0].ExtensionData.Value));
        }

        public Show GetMetadata4Show(ShowId show)
        {
            BatchedExtensionResponse response = GetExtendedMetadata(ExtensionKind.ShowV4, show);
            
            CheckExtendedMetadataResponse(response);
            
            return Serializer.Deserialize<Show>(new MemoryStream(response.ExtendedMetadatas[0].ExtensionDatas[0].ExtensionData.Value));
        }

        public EntityCanvazResponse GetCanvases(EntityCanvazRequest req)
        {
            HttpResponse resp = Send(RequestMethod.POST, "/canvaz-cache/v0/canvases", null, Utils.ProtoBytes(req));
            StatusCodeException.CheckStatus(resp);
            return Serializer.Deserialize<EntityCanvazResponse>(resp.GetResponseStream());
        }

        public void CheckExtendedMetadataResponse(BatchedExtensionResponse response) {
            if (response.ExtendedMetadatas.Count == 0)
                throw new IOException("No metadata in BatchedExtensionResponse");

            if (response.ExtendedMetadatas[0].ExtensionDatas.Count == 0)
                throw new IOException("No metadata in ExtendedMetadata in BatchedExtensionResponse");

            if (response.ExtendedMetadatas[0].ExtensionDatas[0].Header.StatusCode != 200)
                throw new IOException("Bad status code for metadata: " + response.ExtendedMetadatas[0].ExtensionDatas[0].Header.StatusCode);
        }
        
        public BatchedExtensionResponse GetExtendedMetadata(BatchedEntityRequest req)
        {
            HttpResponse resp = Send(RequestMethod.POST, "/extended-metadata/v0/extended-metadata", null,
                Utils.ProtoBytes(req));
            StatusCodeException.CheckStatus(resp);
            return Serializer.Deserialize<BatchedExtensionResponse>(resp.GetResponseStream());
        }

        public BatchedExtensionResponse GetExtendedMetadata(ExtensionKind extensionKind, String spotifyUri)
        {
            HttpResponse resp = Send(RequestMethod.POST, "/extended-metadata/v0/extended-metadata", null, Utils.ProtoBytes(
                new BatchedEntityRequest
                {
                    EntityRequests =
                    {
                        new EntityRequest
                        {
                            EntityUri = spotifyUri,
                            Queries =
                            {
                                new ExtensionQuery
                                {
                                    ExtensionKind = extensionKind
                                }
                            }
                        }
                    }
                }));
            
            StatusCodeException.CheckStatus(resp);

            byte[] response = resp.GetResponseBytes();
            
            if (response.Length == 0)
                throw new IOException("Empty response for extended metadata request");
            
            return Serializer.Deserialize<BatchedExtensionResponse>(new MemoryStream(response));
        }
        
        public BatchedExtensionResponse GetExtendedMetadata(ExtensionKind extensionKind, SpotifyId spotifyId)
        {
            return GetExtendedMetadata(extensionKind, spotifyId.ToSpotifyUri());
        }

        public BatchedExtensionResponse GetExtendedMetadata(ExtensionKind extensionKind, PlayableId playableId)
        {
            return GetExtendedMetadata(extensionKind, playableId.ToSpotifyUri());
        }

        public SelectedListContent GetPlaylist(PlaylistId playlistId)
        {
            HttpResponse resp = Send(RequestMethod.GET, "/playlist/v2/playlist/" + playlistId.HexId());
            StatusCodeException.CheckStatus(resp);
            return Serializer.Deserialize<SelectedListContent>(resp.GetResponseStream());
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
            return JObject.Parse(resp.GetResponseString());
        }

        public JObject GetUserFollowers(String id)
        {
            HttpResponse resp = Send(RequestMethod.GET, "/user-profile-view/v3/profile/" + id + "/followers");
            StatusCodeException.CheckStatus(resp);
            return JObject.Parse(resp.GetResponseString());
        }

        public JObject GetUserFollowing(String id)
        {
            HttpResponse resp = Send(RequestMethod.GET, "/user-profile-view/v3/profile/" + id + "/following");
            StatusCodeException.CheckStatus(resp);
            return JObject.Parse(resp.GetResponseString());
        }

        public JObject GetRadioForTrack(PlayableId id)
        {
            HttpResponse resp = Send(RequestMethod.GET,
                "/inspiredby-mix/v2/seed_to_playlist/" + id.ToSpotifyUri() + "?response-format=json");
            StatusCodeException.CheckStatus(resp);
            return JObject.Parse(resp.GetResponseString());
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
            return new StationsWrapper(JObject.Parse(resp.GetResponseString()));
        }

        private ClientTokenResponse ClientToken()
        {
            ClientTokenRequest protoReq = new ClientTokenRequest
            {
                RequestType = ClientTokenRequestType.RequestClientDataRequest,
                ClientData = new ClientDataRequest
                {
                    ClientId = MercuryRequests.KEYMASTER_CLIENT_ID,
                    ClientVersion = Version.versionNumber(),
                    ConnectivitySdkData = new ConnectivitySdkData
                    {
                        DeviceId = _session.GetDeviceId(),
                        PlatformSpecificData = new PlatformSpecificData
                        {
                            Windows = new NativeWindowsData
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

            byte[] protoBytes = Utils.ProtoBytes(protoReq);
            
            HttpWebRequest request = WebRequest.CreateHttp("https://clienttoken.spotify.com/v1/clienttoken");
            request.Method = "POST";
            request.Accept = "application/x-protobuf";
            request.Headers.Add("Content-Encoding", "application/x-protobuf");
            request.ContentLength = protoBytes.Length;
            
            var stream = request.GetRequestStream();
            stream.Write(protoBytes, 0, protoBytes.Length);
            
            var response = (HttpWebResponse)request.GetResponse();
            
            return Serializer.Deserialize<ClientTokenResponse>(response.GetResponseStream());
        }

        public void SetClientToken(String clientToken)
        {
            _clientToken = clientToken;
        }

        public class StatusCodeException : IOException
        {
            public int code;

            internal StatusCodeException(HttpResponse resp) :
                base(String.Format("{0}: {1}", (int)resp.StatusCode, resp.GetResponseString()))
            {
                code = (int)resp.StatusCode;
            }

            internal static void CheckStatus(HttpResponse resp)
            {
                if (resp.GetResponseStream() == null) throw new StatusCodeException(resp);
                if ((int)resp.StatusCode != 200) throw new StatusCodeException(resp);
            }
        }

        public delegate void AsyncRequestResponse(HttpResponse resp, StatusCodeException optionalException = null);
    }
}