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
        private Shannon sendCipher;
        private Shannon recvCipher;
        private int sendNonce;
        private int recvNonce;
        private object sendLock = new object();
        private object recvLock = new object();

        public CipherPair(byte[] sendKey, byte[] recvKey)
        {
            sendCipher = new Shannon();
            sendCipher.Key(sendKey);
            sendNonce = 0;

            recvCipher = new Shannon();
            recvCipher.Key(recvKey);
            recvNonce = 0;
        }

        public void SendEncoded(BinaryWriter outStream, byte cmd, byte[] payload)
        {
            lock (sendLock)
            {
                sendCipher.Nonce(Utils.toByteArray(sendNonce));
                sendNonce++;
                
                Console.WriteLine(Enum.Parse(typeof(Packet.Type), cmd.ToString()));
                Console.WriteLine("Len: " + payload.Length);

                MemoryStream buffer = new MemoryStream(1 + 2 + payload.Length);
                BinaryWriter bufferWriter = new BinaryWriter(buffer);
                bufferWriter.Write(cmd);
                bufferWriter.WriteBigEndian((short)payload.Length);
                bufferWriter.Write(payload);
                
                byte[] encryptedBuffer = buffer.ToArray();
                sendCipher.Encrypt(encryptedBuffer);
                
                byte[] mac = new byte[4];
                sendCipher.Finish(mac);
                
                outStream.Write(encryptedBuffer);
                outStream.Write(mac);
                outStream.Flush();
            }
        }

        public Packet ReceiveEncoded(BinaryReader reader)
        {
            lock (recvLock)
            {
                recvCipher.Nonce(Utils.toByteArray(recvNonce));
                recvNonce++;
                
                byte[] headerBytes = new byte[3];
                reader.ReadFully(headerBytes);
                recvCipher.Decrypt(headerBytes);

                byte cmd = headerBytes[0];
                int payloadLength = (headerBytes[1] << 8) | (headerBytes[2] & 0xFF);

                byte[] payloadBytes = new byte[payloadLength];
                reader.ReadFully(payloadBytes);
                recvCipher.Decrypt(payloadBytes);

                byte[] mac = new byte[4];
                reader.ReadFully(mac);
                
                byte[] expectedMac = new byte[4];
                recvCipher.Finish(expectedMac);
                if (!Arrays.AreEqual(mac, expectedMac))
                    throw new GeneralSecurityException("MACs don't match!");

                return new Packet(cmd, payloadBytes);
            }
        }
    }
}