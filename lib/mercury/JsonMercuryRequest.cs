using System.IO;
using JsonFx.Json;
using lib.json;
using Newtonsoft.Json.Linq;

namespace lib.mercury
{
    public class JsonMercuryRequest<W> where W : JsonWrapper
    {
        public RawMercuryRequest Request;
        private W _wrapperClass;

        internal JsonMercuryRequest(RawMercuryRequest request, W wrapperClass)
        {
            Request = request;
            _wrapperClass = wrapperClass;
        }

        public W Instantiate(MercuryClient.Response resp)
        {
            BinaryReader reader = new BinaryReader(resp.Payload.Stream());
            JObject elm = JObject.Parse(reader.ReadString());
            return (W)_wrapperClass.GetType().GetConstructor(new []{typeof(JObject)}).Invoke(new object[]{elm});
        }
    }
}