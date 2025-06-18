using System;
using lib.common;
using spotify.metadata.proto;

namespace lib.audio
{
    public class StreamId
    {
        private byte[] fileId;
        private byte[] episodeGid;

        public StreamId(AudioFile file)
        {
            this.fileId = file.FileId;
            this.episodeGid = null;
        }

        public StreamId(Episode episode)
        {
            this.fileId = null;
            this.episodeGid = episode.Gid;
        }

        public String getFileId()
        {
            if (this.fileId == null) throw new Exception("Not a file!");
            return Utils.bytesToHex(fileId);
        }

        public bool isEpisode()
        {
            return episodeGid != null;
        }

        public String getEpisodeGid()
        {
            if (this.episodeGid == null) throw new Exception("Not an episode!");
            return Utils.bytesToHex(this.episodeGid);
        }
    }
}