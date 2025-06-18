using System;
using System.IO;
using System.Net.Sockets;
using lib.common;

namespace lib.crypto
{
    public class CipherPair
    {
        private Shannon sendCipher;
        private Shannon recvCipher;
        private int sendNonce;
        private int recvNonce;

        public CipherPair(byte[] sendKey, byte[] recvKey)
        {
            sendCipher = new Shannon();
            sendCipher.key(sendKey);
            sendNonce = 0;

            recvCipher = new Shannon();
            recvCipher.key(recvKey);
            recvNonce = 0;
        }

        /// <exception cref="IOException"/>
        public void sendEncoded(NetworkStream stream, byte cmd, byte[] payload)
        {
            //synchronized (sendCipher) {
            sendCipher.nonce(Utils.toByteArray(sendNonce));
            sendNonce += 1;

            var buffer = new byte[1 + 2 + payload.Length];
            using (MemoryStream ms = new MemoryStream(buffer))
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                // Write the command byte
                bw.Write(cmd);

                // Write the length of the payload as a short (2 bytes)
                // Ensure the length is cast to a short if it's not already
                bw.Write((short)payload.Length);

                // Write the payload byte array
                bw.Write(payload);
            }

            sendCipher.encrypt(buffer);

            byte[] mac = new byte[4];
            sendCipher.finish(mac);

            stream.Write(buffer, 0, buffer.Length);
            stream.Write(mac, 0, mac.Length);
            stream.Flush();
            //}
        }

        /// <exception cref="GeneralSecurityException"></exception>
        public Packet receiveEncoded(NetworkStream stream)
        {
            //synchronized (recvCipher) {
            recvCipher.nonce(Utils.toByteArray(recvNonce));
            recvNonce += 1;

            byte[] headerBytes = new byte[3];
            stream.Read(headerBytes, 0, headerBytes.Length);
            recvCipher.decrypt(headerBytes);

            byte cmd = headerBytes[0];
            short payloadLength = (short)((headerBytes[1] << 8) | (headerBytes[2] & 0xFF));

            byte[] payloadBytes = new byte[payloadLength];
            stream.Read(payloadBytes, 0, payloadBytes.Length);
            recvCipher.decrypt(payloadBytes);

            byte[] mac = new byte[4];
            stream.Read(mac, 0, mac.Length);

            byte[] expectedMac = new byte[4];
            recvCipher.finish(expectedMac);
            if (mac != expectedMac) throw new Exception("MACs don't match!");

            return new Packet(cmd, payloadBytes);
            //}
        }
    }
}