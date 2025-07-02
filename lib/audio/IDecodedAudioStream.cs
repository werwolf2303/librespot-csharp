using System;
using lib.audio.format;

namespace lib.audio
{
    public interface IDecodedAudioStream
    {
        AbsChunkedInputStream Stream();

        SuperAudioFormat Codec();

        String Describe();

        int DecryptTimeMS();
    }
}