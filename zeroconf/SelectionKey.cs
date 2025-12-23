using System;
using System.Net.Sockets;

namespace zeroconf
{
    [Flags]
    public enum Ops
    {
        None = 0,
        Read = 1,
        Write = 2,
        Accept = 4,
        Connect = 8
    }

    public class SelectionKey
    {
        public Socket Socket { get; }
        public Ops ReadyOps { get; set; }

        public SelectionKey(Socket socket)
        {
            Socket = socket;
            ReadyOps = Ops.None;
        }
    }
}