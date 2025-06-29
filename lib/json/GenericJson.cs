using Newtonsoft.Json.Linq;

namespace lib.json
{
    public class GenericJson : JsonWrapper
    {
        public GenericJson(JObject obj) : base(obj)
        {
        }
    }
}