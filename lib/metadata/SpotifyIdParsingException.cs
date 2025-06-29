using System;

namespace lib.metadata
{
    public class SpotifyIdParsingException : Exception
    {
        public SpotifyIdParsingException(Exception cause) : base(cause.Message, cause)
        {
        }
    }
}