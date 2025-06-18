using System.Collections.Generic;
using spotify.metadata.proto;

namespace lib.audio.format
{
    public interface AudioQualityPicker
    {
        AudioFile getFile(List<AudioFile> files);
    }
}