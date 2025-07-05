using System;
using System.IO;
using System.Text;
using lib.json;
using Newtonsoft.Json.Linq;

namespace lib.mercury
{
    public class JsonMercuryRequest<W> where W : JsonWrapper
    {
        public RawMercuryRequest Request;
        private Type _wrapperClass;

        internal JsonMercuryRequest(RawMercuryRequest request, Type wrapperClass)
        {
            Request = request;
            _wrapperClass = wrapperClass;
        }

        public W Instantiate(MercuryClient.Response resp)
        {
            var sb = new StringBuilder();
            var buffer = new byte[4096];
            int readCount;
            Stream stream = resp.Payload.Stream();
            while ((readCount = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                var s = Encoding.UTF8.GetString(buffer, 0, readCount);
                sb.Append(s);
            }
            JObject elm = JObject.Parse(sb.ToString());
            return (W)_wrapperClass.GetConstructor(new []{typeof(JObject)}).Invoke(new object[]{elm});
        }
    }
}