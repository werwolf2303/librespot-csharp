using Newtonsoft.Json.Linq;

namespace lib.json
{
    public class JsonWrapper
    {
        public JObject Obj;

        public JsonWrapper(JObject obj)
        {
            Obj = obj;
        }

        public override string ToString()
        {
            return Obj.ToString();
        }
    }
}