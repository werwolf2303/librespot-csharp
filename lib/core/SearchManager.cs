using System;
using System.IO;
using System.Text;
using System.Web;
using lib.mercury;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace lib.core
{
    public class SearchManager
    {
        private static String BASE_URL = "hm://searchview/km/4/search/";
        private Session _session;

        public SearchManager(Session session)
        {
            _session = session;
        }

        public JObject Request(SearchRequest request)
        {
            if (request._username.Equals("")) request._username = _session.Username();
            if (request._country.Equals("")) request._country = _session.GetCountryCode();
            if (request._locale.Equals("")) request._locale = _session.GetPreferredLocale();

            MercuryClient.Response resp = _session.GetMercury().SendSync(RawMercuryRequest.Get(request.BuildUrl()));

            if (resp.StatusCode != 200) throw new SearchException(resp.StatusCode);

            return JObject.Load(new JsonTextReader(new StreamReader(resp.Payload.Stream())));
        }

        public class SearchException : IOException
        {
            internal SearchException(int statusCode) : base("Search failed with code " + statusCode + ".")
            {
            }
        }

        public class SearchRequest
        {
            internal String _query;
            internal int _limit = 10;
            internal String _imageSize = "";
            internal String _catalogue = "";
            internal String _country = "";
            internal String _locale = "";
            internal String _username = "";

            public SearchRequest(String query)
            {
                _query = query;

                if (_query.Equals("")) throw new ArgumentException("Search query is empty.");
            }

            internal String BuildUrl()
            {
                StringBuilder url = new StringBuilder(BASE_URL);
                url.Append(HttpUtility.UrlEncode(_query));
                url.Append("?entityVersion=2");
                url.Append("&limit=").Append(_limit);
                url.Append("&imageSize=").Append(HttpUtility.UrlEncode(_imageSize)); 
                url.Append("&catalogue=").Append(HttpUtility.UrlEncode(_catalogue));
                url.Append("&country=").Append(HttpUtility.UrlEncode(_country));
                url.Append("&locale=").Append(HttpUtility.UrlEncode(_locale));
                url.Append("&username=").Append(HttpUtility.UrlEncode(_username));
                return url.ToString();
            }

            public SearchRequest ImageSize(String imageSize)
            {
                _imageSize = imageSize;
                return this;
            }

            public SearchRequest Catalogue(String catalogue)
            {
                _catalogue = catalogue;
                return this;
            }

            public SearchRequest Country(String country)
            {
                _country = country;
                return this;
            }

            public SearchRequest Locale(String locale)
            {
                _locale = locale;
                return this;
            }

            public SearchRequest Username(String username)
            {
                _username = username;
                return this;
            }

            public SearchRequest Limit(int limit)
            {
                _limit = limit;
                return this;
            }
        }
    }
}