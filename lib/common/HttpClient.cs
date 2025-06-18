using System;
using RestSharp;

namespace lib.common
{
    public class HttpClient
    {
        private RestClient restClient;
        
        // ToDo: Proxy n stuff
        public HttpClient()
        {
            restClient = new RestClient("https://whatthehell-is-restsharp.lol");
        }
        
        public IRestResponse<dynamic> newCall(RestRequest request)
        {
            restClient.BaseUrl = new Uri(request.Resource);
            request.Resource = "";
            return restClient.Execute<dynamic>(request);
        }
    }
}