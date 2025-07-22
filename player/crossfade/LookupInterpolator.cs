using System;
using lib.common;
using Newtonsoft.Json.Linq;

namespace player.crossfade
{
    public class LookupInterpolator : IGainInterpolator
    {
        private float[] _tx;
        private float[] _ty;

        internal LookupInterpolator(float[] x, float[] y)
        {
            _tx = x;
            _ty = y;
        }

        internal static LookupInterpolator FromJson(JArray curve)
        {
            float[] x = new float[curve.Count];
            float[] y = new float[curve.Count];
            for (int i = 0; i < curve.Count; i++)
            {
                JObject obj = curve[i] as JObject;
                x[i] = obj["x"].ToObject<float>();
                y[i] = obj["y"].ToObject<float>();
            }
            
            return new LookupInterpolator(x, y);
        }

        public override string ToString()
        {
            return JToken.FromObject(this).ToString();
        }

        public float Interpolate(float ix)
        {
            if (ix >= _tx[_tx.Length - 1]) return _ty[_ty.Length - 1];
            else if (ix <= _tx[0]) return _ty[0];

            for (int i = 0; i < _tx.Length - 1; i++)
            {
                if (ix >= _tx[i] && ix <= _tx[i + 1])
                {
                    float o_low = _ty[i];
                    float i_low = _tx[i];
                    float i_delta = _tx[i + 1] - _tx[i];
                    float o_delta = _ty[i + 1] - _ty[i];

                    if (o_delta == 0) return o_low;
                    else return o_low + ((ix - i_low) * o_delta) / i_delta;
                }
            }
            
            throw new InvalidOperationException("Could not interpolate! " + JToken.FromObject(this));
        }

        public float Last()
        {
            return _ty[_tx.Length - 1];
        }
    }
}