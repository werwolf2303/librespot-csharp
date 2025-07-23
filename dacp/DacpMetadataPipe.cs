using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using log4net;
using log4net.Util;
using Org.BouncyCastle.Utilities.Encoders;

namespace dacp
{
    public class DacpMetadataPipe : IDisposable
    {
        private static String TYPE_SSNC = "73736e63";
        private static String TYPE_CORE = "636f7265";
        private static String CODE_ASAR = "61736172";
        private static String CODE_ASAL = "6173616c";
        private static String CODE_MINM = "6d696e6d";
        private static String CODE_PVOL = "70766f6c";
        private static String CODE_PRGR = "70726772";
        private static String CODE_PICT = "50494354";
        private static String CODE_PFLS = "70666C73";
        private static ILog LOGGER = LogManager.GetLogger(typeof(DacpMetadataPipe));
        private String _file;
        private FileStream _out;

        public DacpMetadataPipe(String file)
        {
            _file = file;
        }

        private void SafeSend(String type, String code, String payload = null)
        {
            SafeSend(type, code, payload == null ? null : Encoding.UTF8.GetBytes(payload));
        }

        private void SafeSend(String type, String code, byte[] payload)
        {
            try
            {
                Send(type, code, payload);
            }
            catch (IOException ex)
            {
                LOGGER.ErrorExt("Failed sending metadata through pipe!", ex);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Send(String type, String code, byte[] payload)
        {
            if (_out == null) _out = File.OpenWrite(_file);

            if (payload != null && payload.Length > 0)
            {
                byte[] data = Encoding.UTF8.GetBytes(String.Format("<item><type>{0}</type><code>{1}</code><length>{2}</length>\n<data encoding=\"base64\">{3}</data></item>\n", type, code,
                    payload.Length, Base64.ToBase64String(payload)));
                _out.Write(data, 0, data.Length);
            }
            else
            {
                byte[] data = Encoding.UTF8.GetBytes(String.Format("<item><type>{0}</type><code>{1}</code><length>{2}</length></item>\n", type, code));
                _out.Write(data, 0, data.Length);
            }
        }

        public void SendImage(byte[] image)
        {
            if (image == null)
            {
                LOGGER.Warn("No image found in metadata.");
                return;
            }
            
            SafeSend(TYPE_SSNC, CODE_PICT, image);
        }

        public void SendProgress(float currentTime, float duration, float sampleRate)
        {
            SafeSend(TYPE_SSNC, CODE_PRGR,
                String.Format("1/{0:F0}/{1:F0}", currentTime * sampleRate / 1000 + 1,
                    duration * sampleRate / 1000 + 1));
        }

        public void SendTrackInfo(String name, String albumName, String artist)
        {
            SafeSend(TYPE_CORE, CODE_MINM, name);
            SafeSend(TYPE_CORE, CODE_ASAL, albumName);
            SafeSend(TYPE_CORE, CODE_ASAR, artist);
        }

        public void SendVolume(float value)
        {
            float xmlValue;
            if (value == 0) xmlValue = -144.0f;
            else xmlValue = (value - 1) * 30.0f;
            String volData = String.Format("{0:F2},0.00,0.00,0.00", xmlValue);
            SafeSend(TYPE_SSNC, CODE_PVOL, volData);
        }

        public void SendPipeFlush()
        {
            SafeSend(TYPE_CORE, CODE_PFLS);
        }

        public void Dispose()
        {
            _out?.Close();
        }
    }
}