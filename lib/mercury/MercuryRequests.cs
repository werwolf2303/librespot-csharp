using System;
using lib.json;

namespace lib.mercury
{
    public class MercuryRequests
    {
        public static String KEYMASTER_CLIENT_ID = "65b708073fc0480ea92a077233ca87bd";

        private MercuryRequests() {
        }
        
        public static JsonMercuryRequest<StationsWrapper> GetStationFor(String context) {
            return new JsonMercuryRequest<StationsWrapper>(RawMercuryRequest.Get("hm://radio-apollo/v3/stations/" + context), typeof(StationsWrapper));
        }
        
        public static RawMercuryRequest AutoplayQuery(String context) {
            return RawMercuryRequest.Get("hm://autoplay-enabled/query?uri=" + context);
        }

        public static JsonMercuryRequest<ResolvedContextWrapper> ResolveContext(String uri) {
            return new JsonMercuryRequest<ResolvedContextWrapper>(RawMercuryRequest.Get(String.Format("hm://context-resolve/v1/%s", uri)), typeof(ResolvedContextWrapper));
        }
        
        public static JsonMercuryRequest<GenericJson> RequestToken(String deviceId, String scope) {
            return new JsonMercuryRequest<GenericJson>(RawMercuryRequest.Get(String.Format("hm://keymaster/token/authenticated?scope=%s&client_id=%s&device_id=%s", scope, KEYMASTER_CLIENT_ID, deviceId)), typeof(GenericJson));
        }
    }
}