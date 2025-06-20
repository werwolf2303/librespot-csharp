using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using lib.common;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;

namespace lib.crypto
{
    public class CipherPair
    {
        private readonly Shannon sendCipher;
        private readonly Shannon recvCipher;
        private int sendNonce;
        private int recvNonce;
        private readonly object sendLock = new object();
        private readonly object recvLock = new object();

        public CipherPair(byte[] sendKey, byte[] recvKey)
        {
            sendCipher = new Shannon();
            sendCipher.key(sendKey);
            sendNonce = 0;

            recvCipher = new Shannon();
            recvCipher.key(recvKey);
            recvNonce = 0;
        }

        public void SendEncoded(BinaryWriter outStream, byte cmd, byte[] payload)
        {
            lock (sendLock)
            {
                sendCipher.nonce(Utils.toByteArray(Interlocked.Increment(ref sendNonce) - 1));
                
                byte[] buffer = new byte[1 + 2 + payload.Length];
                buffer[0] = cmd;
                buffer[1] = (byte)(payload.Length >> 8);
                buffer[2] = (byte)(payload.Length & 0xFF);
                Array.Copy(payload, 0, buffer, 3, payload.Length);

                sendCipher.encrypt(buffer);

                byte[] mac = new byte[4];
                sendCipher.finish(mac);

                outStream.Write(buffer, 0, buffer.Length);
                outStream.Write(mac, 0, mac.Length);
                outStream.Flush();
            }
        }

        public Packet ReceiveEncoded(BinaryReader reader)
        {
            lock (recvLock)
            {
                recvCipher.nonce(Utils.toByteArray(Interlocked.Increment(ref recvNonce) - 1));

                byte[] headerBytes = reader.ReadBytes(3);
                if (headerBytes.Length < 3)
                    throw new EndOfStreamException("Stream ended prematurely while reading header");

                recvCipher.decrypt(headerBytes);

                byte cmd = headerBytes[0];
                int payloadLength = (headerBytes[1] << 8) | headerBytes[2];

                byte[] payloadBytes = reader.ReadBytes(payloadLength);
                if (payloadBytes.Length < payloadLength)
                    throw new EndOfStreamException("Stream ended prematurely while reading payload");

                recvCipher.decrypt(payloadBytes);

                byte[] mac = reader.ReadBytes(4);
                if (mac.Length < 4)
                    throw new EndOfStreamException("Stream ended prematurely while reading MAC");

                byte[] expectedMac = new byte[4];
                recvCipher.finish(expectedMac);

                if (!Arrays.AreEqual(mac, expectedMac))
                    throw new InvalidOperationException("MACs don't match!");

                return new Packet(cmd, payloadBytes);
            }
        }
    }
}