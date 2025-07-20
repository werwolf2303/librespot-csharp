using System;

namespace deps.jorbis.jorbis
{
    public class JOrbisException : Exception
    {
        public JOrbisException() : base()
        {
        }

        public JOrbisException(String s) : base("JOrbis: " + s)
        {
        }
    }
}