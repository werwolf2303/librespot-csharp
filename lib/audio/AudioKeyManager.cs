using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using lib.common;
using lib.core;
using lib.crypto;
using log4net;

namespace lib.audio
{
    public class AudioKeyManager : IPacketsReceiver
    {
        private static byte[] ZERO_SHORT = new [] { (byte) 0, (byte) 0 };
        private static ILog LOGGER = LogManager.GetLogger(typeof(AudioKeyManager));
        private static int AUDIO_KEY_REQUEST_TIMEOUT = 2000;
        private int _seqHolder = 0; 
        private Object _seqHolderLock = new Object();
        private Dictionary<int, Callback> _callbacks = new Dictionary<int, Callback>();
        private Session _session;

        public AudioKeyManager(Session session)
        {
            _session = session;
        }

        public byte[] GetAudioKey(byte[] gid, byte[] fileId)
        {
            return GetAudioKey(gid, fileId, true);
        }

        public byte[] GetAudioKey(byte[] gid, byte[] fileId, bool retry)
        {
            int seq;
            lock (_seqHolderLock)
            {
                seq = _seqHolder;
                _seqHolder++;
            }
            
            MemoryStream outStream = new MemoryStream();
            outStream.Write(fileId, 0, fileId.Length);
            outStream.Write(gid, 0, gid.Length);
            byte[] seqBytes = Utils.toByteArray(seq);
            outStream.Write(seqBytes, 0, seqBytes.Length);
            outStream.Write(ZERO_SHORT, 0, ZERO_SHORT.Length);
            
            _session.Send(Packet.Type.RequestKey, outStream.ToArray());
            
            SyncCallback callback = new SyncCallback();
            _callbacks.Add(seq, callback);

            byte[] key = callback.WaitResponse();
            if (key == null)
            {
                if (retry) return GetAudioKey(gid, fileId, false);
                throw new SyncCallback.AesKeyException(String.Format("Failed fetching audio key! (gid: {0}, fileId: {1})",
                    Utils.bytesToHex(gid), Utils.bytesToHex(fileId)));
            }

            return key;
        }

        public void Dispatch(Packet packet)
        {
            BinaryReader payload = new BinaryReader(new MemoryStream(packet._payload));
            int seq = payload.ReadInt32();

            Callback callback = _callbacks[seq];
            _callbacks.Remove(seq);
            if (callback == null)
            {
                LOGGER.Warn("Couldn't find callback for seq: " + seq);
                return;
            }

            if (packet.Is(Packet.Type.AesKey))
            {
                byte[] key = new byte[16];
                payload.Read(key, 0, key.Length);
                callback.Key(key);
            } else if (packet.Is(Packet.Type.AesKeyError))
            {
                short code = payload.ReadInt16();
                callback.Error(code);
            }
            else
            {
                LOGGER.WarnFormat("Couldn't handle packet, cmd: {0}, length: {1}", packet.GetType(), packet._payload.Length);
            }
        }
        
        private interface Callback
        {
            void Key(byte[] key);
            void Error(short code);
        }
        
        private class SyncCallback : Callback
        {
            private byte[] _reference;
            private Object _referenceLock = new Object();
            
            public void Key(byte[] key)
            {
                lock (_referenceLock)
                {
                    _reference = key;
                    Monitor.PulseAll(_referenceLock);
                }
            }

            public void Error(short code)
            {
                LOGGER.ErrorFormat("Audio key error, code: {0}", code);

                lock (_referenceLock)
                {
                    _reference = null;
                    Monitor.PulseAll(_referenceLock);
                }
            }

            public byte[] WaitResponse()
            {
                lock (_referenceLock)
                { 
                    Monitor.Wait(_referenceLock, AUDIO_KEY_REQUEST_TIMEOUT);
                    return _reference;
                }
            }

            public class AesKeyException : IOException
            {
                public AesKeyException(String message) : base(message)
                {
                }
            }
        }
    }
}