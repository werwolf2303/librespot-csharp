using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using lib.common;
using lib.core;
using lib.crypto;
using log4net;

namespace lib.audio.storage
{
    public class ChannelManager : IDisposable, IPacketsReceiver
    {
        public static int CHUNK_SIZE = 128 * 1024;
        private static ILog LOGGER = LogManager.GetLogger(typeof(ChannelManager));
        private Dictionary<short, Channel> _channels = new Dictionary<short, Channel>();
        private int _seqHolder = 0;
        private Object _seqHolderLock = new Object();
        private QueuedTaskScheduler _queuedTaskScheduler =
            new QueuedTaskScheduler(new NameThreadFactory((action => "channel-queue-" + action.GetHashCode())),
                "channel-queue");
        private Session _session;

        public ChannelManager(Session session)
        {
            _session = session;
        }

        internal void requestChunk(byte[] fileId, int index, AudioFile file)
        {
            int start = index * CHUNK_SIZE / 4;
            int end = (index + 1) * CHUNK_SIZE / 4;

            Channel channel = new Channel(this, file, index);
            _channels.Add(channel.Id, channel);
            
            MemoryStream bytes = new MemoryStream();
            BinaryWriter outWriter = new BinaryWriter(bytes);
            
            outWriter.Write(channel.Id);
            outWriter.Write(0x00000000);
            outWriter.Write(0x00000000);
            outWriter.Write(0x00004e20);
            outWriter.Write(0x00030d40);
            outWriter.Write(fileId);
            outWriter.Write(start);
            outWriter.Write(end);
            
            _session.Send(Packet.Type.StreamChunk, bytes.ToArray());
        }

        public void Dispatch(Packet packet)
        {
            BinaryReader payload = new BinaryReader(new MemoryStream(packet._payload));
            if (packet.Is(Packet.Type.StreamChunkRes))
            {
                short id = payload.ReadInt16();
                Channel channel = _channels[id];
                if (channel == null)
                {
                    LOGGER.WarnFormat("Couldn't find channel, id: {0}, receibed {1}", id, packet._payload.Length);
                    return;
                }

                channel.AddToQueue(payload.BaseStream);
            }
            else if (packet.Is(Packet.Type.ChannelError))
            {
                short id = payload.ReadInt16();
                Channel channel = _channels[id];
                if (channel == null) {
                    LOGGER.WarnFormat("Dropping channel error, id: {0}, code: {1}", id, payload.ReadInt16());
                    return;
                }

                channel.StreamError(payload.ReadInt16());
            }
            else
            {
                LOGGER.WarnFormat("Couldn't handle packet, cmd: {0}, payload: {1}", packet.GetType(), Utils.bytesToHex(packet._payload));
            }
        }

        public void Dispose()
        {
            _queuedTaskScheduler.Dispose();
        }

        public class Channel
        {
            public short Id;
            private BlockingCollection<Stream> _queue = new BlockingCollection<Stream>();
            private AudioFile _file;
            private int _chunkIndex;
            private MemoryStream _buffer = new MemoryStream(CHUNK_SIZE);
            private volatile bool header = true;
            private ChannelManager _channelManager;
                
            internal Channel(ChannelManager manager, AudioFile file, int chunkIndex)
            {
                _file = file;
                _chunkIndex = chunkIndex;
                _channelManager = manager;
                lock (_channelManager._seqHolderLock)
                {
                    Id = (short) _channelManager._seqHolder;
                    _channelManager._seqHolder++;
                }
                
                _channelManager._queuedTaskScheduler.PQueueTask(new Task(o =>
                {
                    new Handler(this).Run();
                }, ""));
            }

            private bool Handle(BinaryReader payload)
            {
                if (payload.BaseStream.Length - payload.BaseStream.Position == 0)
                {
                    if (!header)
                    {
                        lock (_buffer)
                        {
                            _file.WriteChunk(_buffer.ToArray(), _chunkIndex, false);
                            return true;
                        }
                    }

                    LOGGER.Debug("Received empty chunk, skipping.");
                    return false;
                }

                if (header)
                {
                    short length;
                    while (payload.BaseStream.Length - payload.BaseStream.Position > 0 &&
                           (length = payload.ReadInt16()) > 0)
                    {
                        byte headerId = payload.ReadByte();
                        byte[] headerData = new byte[length - 1];
                        payload.ReadFully(headerData);
                        _file.WriteHeader(headerId, headerData, false);
                    }

                    header = false;
                }
                else
                {
                    byte[] bytes = new byte[payload.BaseStream.Length - payload.BaseStream.Position];
                    payload.ReadFully(bytes);
                    lock (_buffer)
                    {
                        _buffer.Write(bytes, 0, bytes.Length);
                    }
                }

                return false;
            }

            internal void AddToQueue(Stream payload)
            {
                _queue.Add(payload);
            }

            public void StreamError(short code)
            {
                _file.StreamError(_chunkIndex, code);
            }
            
            private class Handler
            {
                private Channel _channel;

                public Handler(Channel channel)
                {
                    _channel = channel;
                }
                
                public void Run()
                {
                    LOGGER.Debug("ChannelManager.Handler is starting");

                    while (true)
                    {
                        try
                        {
                            if (_channel.Handle(new BinaryReader(_channel._queue.Take())))
                            {
                                _channel._channelManager._channels.Remove(_channel.Id);
                                break;
                            }
                        }
                        catch (IOException ex)
                        {
                            LOGGER.Error("Failed handling packet!", ex);
                        }
                    }
                }
            }
        }
    }
}