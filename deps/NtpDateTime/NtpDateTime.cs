using System;
using System.Net;
using System.Net.Sockets;

namespace deps.NtpDateTime
{
    public class NtpDateTime
    {
        private static DateTime Now()
        {
            byte[] array = new byte[48];
            array[0] = 27;
            IPAddress[] addressList = Dns.GetHostEntry("time.windows.com").AddressList;
            IPEndPoint remoteEP = new IPEndPoint(addressList[0], 123);
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect(remoteEP);
                socket.ReceiveTimeout = 3000;
                socket.Send(array);
                socket.Receive(array);
            }
            ulong x = BitConverter.ToUInt32(array, 40);
            ulong x2 = BitConverter.ToUInt32(array, 44);
            x = SwapEndianness(x);
            x2 = SwapEndianness(x2);
            ulong num = x * 1000 + x2 * 1000 / 4294967296uL;
            return new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((long)num).ToLocalTime();
        }

        internal static DateTime Now(bool computerDateOnException)
        {
            try
            {
                return Now();
            }
            catch (Exception)
            {
                if (computerDateOnException)
                {
                    return DateTime.Now;
                }

                throw;
            }
        }

        private static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0xFF) << 24) + ((x & 0xFF00) << 8) + ((x & 0xFF0000) >> 8) + ((x & 0xFF000000u) >> 24));
        }
    }
}