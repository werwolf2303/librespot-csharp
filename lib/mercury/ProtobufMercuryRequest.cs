using ProtoBuf;

namespace lib.mercury
{
    public class ProtobufMercuryRequest
    {
        internal RawMercuryRequest Request;

        internal ProtobufMercuryRequest(RawMercuryRequest request)
        {
            Request = request;
        }
    }
}