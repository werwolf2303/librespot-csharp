using System;
using System.IO;
using System.Text;
using System.Web;
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

        public class SearchException : IOException
        {
            internal SearchException(int statusCode) : base("Search failed with code " + statusCode + ".")
            {
            }
        }

        public class SearchRequest
        {
            private String _query;
            private int _limit = 10;
            private String _imageSize = "";
            private String _catalogue = "";
            private String _country = "";
            private String _locale = "";
            private String _username = "";

            public SearchRequest(String query)
            {
                _query = query;

                if (_query.Equals("")) throw new ArgumentException("Search query is empty.");
            }

            private String BuildUrl()
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