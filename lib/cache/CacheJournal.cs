using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using lib.cache;
using lib.common;

namespace lib.cache
{
    public class CacheJournal : IDisposable
    {
        private static int MAX_CHUNKS_SIZE = 2048;
        private static int MAX_CHUNKS = MAX_CHUNKS_SIZE * 8;
        private static int MAX_HEADER_LENGTH = 1023;
        private static int MAX_ID_LENGTH = 40;
        private static int MAX_HEADERS = 8;
        private static int JOURNAL_ENTRY_SIZE = MAX_ID_LENGTH + MAX_CHUNKS_SIZE + (1 + MAX_HEADER_LENGTH) * MAX_HEADERS;
        private static byte[] ZERO_ARRAY = new byte[JOURNAL_ENTRY_SIZE];
        private static FileStream io;
        private Dictionary<String, Entry> entries = new Dictionary<string, Entry>(1024);

        public void Dispose()
        {
            entries.Clear();
            ZERO_ARRAY = new byte[JOURNAL_ENTRY_SIZE];
            io.Dispose();
        }

        /// <exception cref="IOException"></exception>
        public CacheJournal(String parent)
        {
            String file = Path.Combine(parent, "journal.dat");
            if (!File.Exists(file))
            {
                File.Create(file).Close();
                if (!File.Exists(file))
                {
                    throw new IOException("Failed creating empty cache journal.");
                }
            }

            io = new FileStream(file, FileMode.Open, FileAccess.Read);
        }

        /// <exception cref="IOException"></exception>
        private static bool checkId(FileStream io, int first, byte[] id)
        {
            for (int i = 0; i < id.Length; i++)
            {
                int read = i == 0 ? first : io.ReadByte();
                if (read == 0)
                    return i != 0;

                if (read != id[i])
                    return false;
            }

            return true;
        }

        private static String trimArrayToNullTerminator(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
                if (bytes[i] == 0)
                    return Encoding.ASCII.GetString(bytes, 0, i);
            return Encoding.ASCII.GetString(bytes);
        }

        /// <exception cref="ArgumentException"></exception>
        public bool hasChunk(String streamId, int index)
        {
            if (index < 0 || index > MAX_HEADERS) throw new ArgumentException("Illegal argument");

            Entry entry = find(streamId);
            if (entry == null) throw new JournalException("Couldn't find entry on journal: " + streamId);

            // synchronized (io) {
            return entry.hasChunk(index);
            // }
        }

        /// <exception cref="IOException"></exception>
        public void setChunk(String streamId, int index, bool val)
        {
            if (index < 0 || index > MAX_CHUNKS) throw new ArgumentException("Illegal argument");

            Entry entry = find(streamId);
            if (entry == null) throw new JournalException("Couldn't find entry on journal: " + streamId);

            // synchronized (io) {
            entry.setChunk(index, val);
            // }
        }

        /// <exception cref="IOException"></exception>
        public List<JournalHeader> getHeaders(String streamId)
        {
            Entry entry = find(streamId);
            if (entry == null) throw new JournalException("Couldn't find entry on journal: " + streamId);

            // synchronized (io) {
            return entry.getHeaders();
            // }
        }

        /// <exception cref="IOException"></exception>
        public JournalHeader getHeader(String streamId, int id)
        {
            Entry entry = find(streamId);
            if (entry == null) throw new JournalException("Couldn't find entry on journal: " + streamId);

            // synchronized (io) {
            return entry.getHeader(id);
            // }
        }

        /// <exception cref="IOException"></exception>
        public void setHeader(String streamId, int headerId, byte[] value)
        {
            String strValue = Utils.bytesToHex(value);

            if (strValue.Length > MAX_HEADER_LENGTH) throw new ArgumentException("Illegal argument");
            else if (headerId == 0) throw new ArgumentException("Illegal argument");

            Entry entry = find(streamId);
            if (entry == null) throw new JournalException("Couldn't find entry on journal: " + streamId);

            // synchronized (io) {
            entry.setHeader(headerId, strValue);
            // }
        }

        /// <exception cref="IOException"></exception>
        public void remove(String streamId)
        {
            Entry entry = find(streamId);
            if (entry == null) return;

            // synchronized (io) {
            entry.remove();
            // }

            entries.Remove(streamId);
        }

        /// <exception cref="IOException"></exception>
        public List<string> getEntries()
        {
            List<string> list = new List<string>(1024);
            
            // synchronized (io) {
            io.Seek(0, SeekOrigin.Begin);
            
            int i = 0;
            while (true)
            {
                io.Seek((long)i * JOURNAL_ENTRY_SIZE, SeekOrigin.Begin);

                int first = io.ReadByte();
                if (first == -1) // EOF
                    break;

                if (first == 0)
                {
                    // Empty spot
                    i++;
                    continue;
                }

                byte[] id = new byte[MAX_ID_LENGTH];
                id[0] = (byte)first;
                io.Read(id, 1, MAX_ID_LENGTH - 1);

                String idStr = trimArrayToNullTerminator(id);
                Entry entry = new Entry(idStr, i * JOURNAL_ENTRY_SIZE);
                entries.Add(idStr, entry);
                list.Add(idStr);

                i++;
            }
            // }

            return list;
        }

        /// <exception cref="IOException"></exception>
        private Entry find(String id)
        {
            if (id.Length > MAX_ID_LENGTH) throw new ArgumentException("Illegal argument");

            Entry entry = entries[id];
            if (entry != null) return entry;

            byte[] idBytes = Encoding.ASCII.GetBytes(id);
            
            // synchronized (io) {
            io.Seek(0, SeekOrigin.Begin);

            int i = 0;
            while (true)
            {
                io.Seek((long)i * JOURNAL_ENTRY_SIZE, SeekOrigin.Begin);

                int first = io.ReadByte();
                if (first == -1) // EOF
                    return null;

                if (first == 0)
                {
                    // Empty spot
                    i++;
                    continue;
                }

                if (checkId(io, first, idBytes))
                {
                    entry = new Entry(id, i * JOURNAL_ENTRY_SIZE);
                    entries.Add(id, entry);
                    return entry;
                }

                i++;
            }
            // }
        }

        /// <exception cref="IOException"></exception>
        public void createIfNeeded(String id)
        {
            if (find(id) != null) return;

            // synchronized (io) {
            io.Seek(0, SeekOrigin.Begin);

            int i = 0;
            while (true)
            {
                io.Seek((long)i * JOURNAL_ENTRY_SIZE, SeekOrigin.Begin);

                int first = io.ReadByte();
                if (first == 0 || first == -1)
                {
                    // First empty spot or EOF
                    Entry entry = new Entry(id, i * JOURNAL_ENTRY_SIZE);
                    entry.writeId();
                    entries.Add(id, entry);
                    return;
                }

                i++;
            }
            // }
        }

        /// <exception cref="IOException"></exception>
        public void close()
        {
            // synchronized (io) {
            io.Close();
            // }
        }

        /// <exception cref="IOException"></exception>
        private class JournalException : Exception
        {
            public JournalException(string message)
                : base(message)
            {
            }
        }

        private class Entry
        {
            private String id;
            private int offset;

            public Entry(String id, int offset)
            {
                this.id = id;
                this.offset = offset;
            }

            /// <exception cref="IOException"></exception>
            public void writeId()
            {
                io.Seek(offset, SeekOrigin.Begin);
                var bytes = Encoding.ASCII.GetBytes(id);
                io.Write(bytes, 0, bytes.Length);
                io.Write(ZERO_ARRAY, 0, JOURNAL_ENTRY_SIZE - id.Length);
            }

            /// <exception cref="IOException"></exception>
            public void remove()
            {
                io.Seek(offset, SeekOrigin.Begin);
                io.WriteByte(0);
            }

            /// <exception cref="IOException"></exception>
            private int findHeader(int headerId)
            {
                for (int i = 0; i < MAX_HEADERS; i++)
                {
                    io.Seek(offset + MAX_ID_LENGTH + MAX_CHUNKS_SIZE + i * (MAX_HEADER_LENGTH + 1), SeekOrigin.Begin);
                    if ((io.ReadByte() & 0xFF) == headerId)
                        return i;
                }

                return -1;
            }

            /// <exception cref="IOException"></exception>
            public void setHeader(int id, String value)
            {
                int index = findHeader(id);
                if (index == -1)
                {
                    for (int i = 0; i < MAX_HEADERS; i++)
                    {
                        io.Seek(offset + MAX_ID_LENGTH + MAX_CHUNKS_SIZE + i * (MAX_HEADER_LENGTH + 1), SeekOrigin.Begin);
                        if (io.ReadByte() == 0)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index == -1) throw new Exception("Illegal state");
                }

                io.Seek(offset + MAX_ID_LENGTH + MAX_CHUNKS_SIZE + (long)index * (MAX_HEADER_LENGTH + 1), SeekOrigin.Begin);
                io.WriteByte(BitConverter.GetBytes(id)[0]);
                var bytes = Encoding.ASCII.GetBytes(value);
                io.Write(bytes, (int) io.Position, bytes.Length);
            }

            /// <exception cref="IOException"></exception>
            public List<JournalHeader> getHeaders()
            {
                List<JournalHeader> list = new List<JournalHeader>(MAX_HEADERS);
                for (int i = 0; i < MAX_HEADERS; i++)
                {
                    io.Seek(offset + MAX_ID_LENGTH + MAX_CHUNKS_SIZE + i * (MAX_HEADER_LENGTH + 1), SeekOrigin.Begin);
                    int headerId;
                    if ((headerId = io.ReadByte()) == 0)
                        continue;

                    byte[] read = new byte[MAX_HEADER_LENGTH];
                    io.Read(read, (int) io.Position, MAX_HEADER_LENGTH);

                    list.Add(new JournalHeader((byte)headerId, trimArrayToNullTerminator(read)));
                }

                return list;
            }

            /// <exception cref="IOException"></exception>
            public JournalHeader getHeader(int id)
            {
                int index = findHeader(id);
                if (index == -1) return null;

                io.Seek(offset + MAX_ID_LENGTH + MAX_CHUNKS_SIZE + (long)index * (MAX_HEADER_LENGTH + 1) + 1, SeekOrigin.Begin);
                byte[] read = new byte[MAX_HEADER_LENGTH];
                io.Read(read, (int) io.Position, MAX_HEADER_LENGTH);

                return new JournalHeader(id, trimArrayToNullTerminator(read));
            }

            /// <exception cref="IOException"></exception>
            public void setChunk(int index, bool val)
            {
                int pos = offset + MAX_ID_LENGTH + (index / 8);
                io.Seek(pos, SeekOrigin.Begin);
                int read = io.ReadByte();
                if (val) read |= (1 << (index % 8));
                else read &= ~(1 << (index % 8));
                io.Seek(pos, SeekOrigin.Begin);
                io.WriteByte(BitConverter.GetBytes(read)[0]);
            }

            /// <exception cref="IOException"></exception>
            public bool hasChunk(int index)
            {
                io.Seek(offset + MAX_ID_LENGTH + (index / 8), SeekOrigin.Begin);
                return ((io.ReadByte() >> index % 8) & 1) == 1;
            }
        }
    }
}