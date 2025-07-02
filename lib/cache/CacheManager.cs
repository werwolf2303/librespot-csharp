using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Linq;
using lib.audio;
using lib.audio.storage;
using lib.common;
using lib.core;
using log4net;

namespace lib.cache
{
public class CacheManager : IDisposable
{
    private static readonly long CLEAN_UP_THRESHOLD = 604800000;
    private static ILog LOGGER = log4net.LogManager.GetLogger(typeof(CacheManager));
    
    private static readonly int HEADER_TIMESTAMP = 254;
    private static readonly int HEADER_HASH = 253;

    private readonly String parent;
    private readonly CacheJournal journal;
    private readonly ConcurrentDictionary<string, Handler> fileHandlers = new ConcurrentDictionary<string, Handler>();

    public CacheManager(Session.Configuration conf)
    {
        if (!conf.CacheEnabled)
        {
            parent = null;
            journal = null;
            return;
        }

        this.parent = conf.CacheDir;
        if (!File.Exists(parent))
        {
            File.Create(parent).Close();
            if (!File.Exists(parent)) new IOException("Couldn't create cache directory!");
        }

        journal = new CacheJournal(parent);

        new Thread(() =>
        {
            try
            {
                List<string> entries = journal.getEntries();
                List<string> entriesToRemove = new List<string>();

                foreach (string id in entries)
                {
                    if (!Exists(parent, id))
                    {
                        entriesToRemove.Add(id);
                    }
                }

                foreach (string id in entriesToRemove)
                {
                    entries.Remove(id);
                    journal.remove(id);
                }

                if (conf.DoCacheCleanUp)
                {
                    List<string> expiredEntries = new List<string>();
                    foreach (string id in entries)
                    {
                        JournalHeader header = journal.getHeader(id, HEADER_TIMESTAMP);
                        if (header == null) continue;

                        var timestamp = new BigInteger(header.value) * 1000;
                        if (Utils.getUnixTimeStampInMilliseconds() - timestamp > CLEAN_UP_THRESHOLD)
                            expiredEntries.Add(id);
                    }

                    foreach (string id in expiredEntries)
                    {
                        Remove(id);
                    }
                }

                LOGGER.Info("There are " + entries.Count + " cached entries.");
            }
            catch (IOException ex)
            {
                LOGGER.Warn("Failed performing maintenance operations.", ex);
            }
        })
        { Name = "cache-maintenance" }.Start();
    }

    private static FileInfo GetCacheFile(String parentDir, string hex)
    {
        string dirName = hex.Substring(0, 2);
        DirectoryInfo subDir = new DirectoryInfo(Path.Combine(parentDir, dirName));
        if (!subDir.Exists)
        {
            File.Create(subDir.FullName).Close();
            if(!subDir.Exists) throw new IOException("Couldn't create cache directory!");
        }
        return new FileInfo(Path.Combine(subDir.FullName, hex));
    }

    private static bool Exists(String parentDir, string hex)
    {
        string dirName = hex.Substring(0, 2);
        DirectoryInfo subDir = new DirectoryInfo(Path.Combine(parentDir, dirName));
        return new FileInfo(Path.Combine(subDir.FullName, hex)).Exists;
    }

    private void Remove(string streamId)
    {
        journal.remove(streamId);

        FileInfo file = GetCacheFile(parent, streamId);
        if (file.Exists)
        {
            try
            {
                file.Delete();
            }
            catch (IOException ex)
            {
                LOGGER.Warn("Couldn't delete cache file: " + file.FullName, ex);
            }
        }

        LOGGER.Debug("Removed " + streamId + " from cache.");
    }

    public void Dispose()
    {
        foreach (Handler handler in fileHandlers.Values.ToList()) 
        {
            handler.Dispose();
        }
        fileHandlers.Clear(); 

        journal?.Dispose();
    }
    
    public Handler GetHandler(string id)
    {
        if (journal == null) return null;

        return fileHandlers.GetOrAdd(id, (key) =>
        {
            FileInfo cacheFile = GetCacheFile(parent, key);
            return new Handler(key, cacheFile, journal, fileHandlers);
        });
    }

    public Handler GetHandler(StreamId streamId)
    {
        return GetHandler(streamId.isEpisode() ? streamId.getEpisodeGid() : streamId.getFileId());
    }

    public class BadChunkHashException : Exception
    {
        public BadChunkHashException(string streamId, byte[] expected, byte[] actual)
            : base(string.Format("Failed verifying chunk hash for {0}, expected: {1}, actual: {2}",
                streamId, Utils.bytesToHex(expected), Utils.bytesToHex(actual)))
        { }
    }

    public class Handler : IDisposable
    {
        private readonly string streamId;
        private readonly FileStream io; 
        private readonly CacheJournal journal;
        private readonly ConcurrentDictionary<string, Handler> fileHandlers;
        private bool updatedTimestamp;
        private readonly object _lock = new object(); 

        public Handler(string streamId, FileInfo file, CacheJournal journal, ConcurrentDictionary<string, Handler> fileHandlers)
        {
            this.streamId = streamId;
            this.journal = journal;
            this.fileHandlers = fileHandlers;

            if (!file.Exists)
            {
                try
                {
                    file.Create().Dispose();
                }
                catch (IOException ex)
                {
                    throw new IOException("Couldn't create cache file!", ex);
                }
            }
            this.io = new FileStream(file.FullName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            journal.createIfNeeded(streamId);
        }

        private void UpdateTimestamp()
        {
            if (updatedTimestamp) return;

            try
            {
                byte[] timestampBytes = new BigInteger(Utils.getUnixTimeStampInMilliseconds()).ToByteArray().Reverse().ToArray();
                journal.setHeader(streamId, HEADER_TIMESTAMP, timestampBytes);
                updatedTimestamp = true;
            }
            catch (IOException ex)
            {
                LOGGER.Warn("Failed updating timestamp for " + streamId, ex);
            }
        }

        public void SetHeader(int id, byte[] value)
        {
            try
            {
                journal.setHeader(streamId, id, value);
            }
            finally
            {
                UpdateTimestamp();
            }
        }

        public List<JournalHeader> GetAllHeaders()
        {
            return journal.getHeaders(streamId);
        }

        public byte[] GetHeader(byte id)
        {
            JournalHeader header = journal.getHeader(streamId, id);
            return header == null ? null : header.value;
        }

        public bool HasChunk(int index)
        {
            UpdateTimestamp();

            lock (_lock)
            {
                if (io.Length < (long)(index + 1) * ChannelManager.CHUNK_SIZE)
                    return false;
            }

            return journal.hasChunk(streamId, index);
        }

        public void ReadChunk(int index, IGeneralWriteableStream stream)
        {
            stream.WriteChunk(ReadChunk(index), index, true);
        }

        public byte[] ReadChunk(int index)
        {
            UpdateTimestamp();

            byte[] buffer = new byte[ChannelManager.CHUNK_SIZE];

            lock (_lock)
            {
                io.Seek((long)index * ChannelManager.CHUNK_SIZE, SeekOrigin.Begin);

                int read = io.Read(buffer, 0, buffer.Length);
                if (read != buffer.Length)
                    throw new IOException(string.Format("Couldn't read full chunk, read: {0}, needed: {1}", read, buffer.Length));

                if (index == 0)
                {
                    JournalHeader header = journal.getHeader(streamId, HEADER_HASH);
                    if (header != null)
                    {
                        try
                        {
                            using (MD5 md5 = MD5.Create())
                            {
                                byte[] hash = md5.ComputeHash(buffer);
                                if (!hash.SequenceEqual(header.value)) 
                                {
                                    journal.setChunk(streamId, index, false);
                                    throw new BadChunkHashException(streamId, header.value, hash);
                                }
                            }
                        }
                        catch (Exception ex) 
                        {
                            LOGGER.Error("Failed initializing MD5 digest.", ex);
                        }
                    }
                }

                return buffer;
            }
        }

        public void WriteChunk(byte[] buffer, int index)
        {
            lock (_lock)
            {
                io.Seek((long)index * ChannelManager.CHUNK_SIZE, SeekOrigin.Begin);
                io.Write(buffer, 0, buffer.Length);
                io.Flush(); // Ensure data is written to disk
            }

            try
            {
                journal.setChunk(streamId, index, true);

                if (index == 0)
                {
                    try
                    {
                        using (MD5 md5 = MD5.Create())
                        {
                            byte[] hash = md5.ComputeHash(buffer);
                            journal.setHeader(streamId, HEADER_HASH, hash);
                        }
                    }
                    catch (Exception ex)
                    {
                        LOGGER.Error("Failed initializing MD5 digest.", ex);
                    }
                }
            }
            finally
            {
                UpdateTimestamp();
            }
        }

        public void Dispose()
        {
            fileHandlers.TryRemove(streamId, out _);

            lock (_lock)
            {
                io.Dispose(); 
            }
        }
    }
}
}