using System.Collections.Generic;
using System.Net.Sockets;

namespace zeroconf
{
    public class Selector
    {
        private readonly Dictionary<Socket, SelectionKey> _keys = new Dictionary<Socket, SelectionKey>();
        private readonly object _lock = new object();

        /// <summary>
        /// Register a socket with the selector.
        /// </summary>
        public SelectionKey Register(Socket socket)
        {
            lock (_lock)
            {
                if (_keys.ContainsKey(socket))
                    return _keys[socket];

                var key = new SelectionKey(socket);
                _keys[socket] = key;
                return key;
            }
        }

        /// <summary>
        /// Remove a socket from the selector.
        /// </summary>
        public void Deregister(Socket socket)
        {
            lock (_lock)
            {
                _keys.Remove(socket);
            }
        }

        /// <summary>
        /// Select sockets ready for operations.
        /// Blocks for up to timeoutMs milliseconds.
        /// </summary>
        public List<SelectionKey> Select(int timeoutMs = 1000)
        {
            var readyKeys = new List<SelectionKey>();
            List<Socket> readList, writeList;

            lock (_lock)
            {
                readList = new List<Socket>(_keys.Keys);
                writeList = new List<Socket>(); // optional, if you want write-ready detection
            }

            // Non-blocking select
            Socket.Select(readList, writeList, null, timeoutMs * 1000); // timeout in microseconds

            lock (_lock)
            {
                foreach (var sock in readList)
                {
                    if (_keys.TryGetValue(sock, out var key))
                    {
                        key.ReadyOps |= Ops.Read;
                        readyKeys.Add(key);
                    }
                }

                foreach (var sock in writeList)
                {
                    if (_keys.TryGetValue(sock, out var key))
                    {
                        key.ReadyOps |= Ops.Write;
                        if (!readyKeys.Contains(key))
                            readyKeys.Add(key);
                    }
                }
            }

            return readyKeys;
        }

        /// <summary>
        /// Wakeup is a no-op for now, because Select is non-blocking. 
        /// You can implement using a "wakeup socket" if needed.
        /// </summary>
        public void Wakeup()
        {
            // Optional: trigger select to return immediately
        }

        /// <summary>
        /// Returns a SelectionKey for a socket.
        /// </summary>
        public SelectionKey KeyFor(Socket socket)
        {
            lock (_lock)
            {
                _keys.TryGetValue(socket, out var key);
                return key;
            }
        }
    }
}