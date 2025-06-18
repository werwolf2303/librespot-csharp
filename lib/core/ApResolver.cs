using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using lib.common;
using log4net;
using RestSharp;
using RestSharp.Deserializers;

namespace lib.core
{
    public class ApResolver
    {
        private static String BASE_URL = "http://apresolve.spotify.com/";
        private static ILog LOGGER = LogManager.GetLogger(typeof(ApResolver));
        
        private HttpClient client;
        private Dictionary<String, List<String>> pool = new Dictionary<String, List<String>>(3);
        private volatile bool poolReady = false;

        public ApResolver(HttpClient client)
        {
            this.client = client;
            fillPool();
        }

        private void fillPool()
        {
            request("accesspoint", "dealer", "spclient");
        }

        private void refreshPool()
        {
            poolReady = false;
            pool.Clear();
            fillPool();
        }

        private static List<String> getUrls(JsonObject body, String type)
        {
            JsonArray aps = body[type] as JsonArray;
            List<String> list = new List<String>(aps.Count);
            foreach (String ap in aps)
            {
                list.Add(ap);
            }
            return list;
        }

        private void request(params String[] types)
        {
            if (types.Length == 0) throw new ArgumentException("Illegal argument");
            
            StringBuilder url = new StringBuilder(BASE_URL + "?");
            for (int i = 0; i < types.Length; i++)
            {
                if (i != 0) url.Append("&");
                url.Append("type=").Append(types[i]);
            }
            
            RestRequest request = new RestRequest(url.ToString(), Method.GET);
            IRestResponse<dynamic> response = client.newCall(request);
            Dictionary<String, List<String>> map = new Dictionary<String, List<String>>();
            foreach (String type in types)
                map.Add(type, getUrls(SimpleJson.DeserializeObject<JsonObject>(response.Content), type));

            // synchronized (pool) {
            foreach (KeyValuePair<String, List<String>> pair in map)
            {
                pool.Add(pair.Key, pair.Value);
            }
            poolReady = true;
            // pool.notifyAll();
            // }
            
            LOGGER.Info("Loaded aps into pool: " + Utils.ConvertApsToString(pool));
        }

        private void waitForPool()
        {
            if (!poolReady)
            {
                //synchronized (pool) {
                //    try {
                //        pool.wait();
                //    } catch (InterruptedException ex) {
                //        throw new IllegalStateException(ex);
                //    }
                //}
            }
        }

        private String getRandomOf(String type)
        {
            waitForPool();

            List<String> urls = pool[type];
            if (urls == null || urls.Count == 0) throw new Exception("Illegal state");
            return urls[new Random().Next(0, urls.Count - 1)];
        }

        public String getRandomDealer()
        {
            return getRandomOf("dealer");
        }

        public String getRandomSpclient()
        {
            return getRandomOf("spclient");
        }

        public String getRandomAccesspoint()
        {
            return getRandomOf("accesspoint");
        }
    }
}