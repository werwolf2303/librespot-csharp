using System;
using System.IO;
using decoder_api;

namespace lib.audio
{
    public abstract class AbsChunkedInputStream : Stream, HaltListener
    {
        private static int PRELOAD_AHEAD = 3;
        private static int PRELOAD_CHUNK_RETRIES = 2;
        private static int MAX_CHUNK_TRIES = 128;
        // private final Object waitLock = new Object();
        private int[] retries;
        private bool retryOnChunkError;
        private volatile int waitForChunk = -1;
        private volatile ChunkException ChunkException;
        private int pos = 0;
        private int mark = 0;
        private volatile bool closed = false;
        private int decodedLength = 0;

        protected AbsChunkedInputStream(bool retryOnChunkError)
        {
            //retries = new int[chunks()];
            this.retryOnChunkError = retryOnChunkError;
        }

        public bool isClosed()
        {
            return closed;
        }

        protected abstract byte[][] buffer();
        public abstract int size();

        public override void Close()
        {
            closed = true;
            
            //synchronized (waitLock) {
            //waitLock.notifyAll();
            //}
        }

        public void streamReadHalted(int chunk, long time)
        {
            
        }

        public void streamReadResumed(int chunk, long time)
        {
            throw new NotImplementedException();
        }
    }
    
    public class ChunkException : IOException {
        
        public ChunkException(String message): base(message) {} 
        public ChunkException fromStreamError(short streamError) { 
            return new ChunkException("Failed due to stream error, code: " + streamError); 
        }
    }
}