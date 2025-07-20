using System;
using System.Text;
using deps.jorbis.jogg;
using Buffer = deps.jorbis.jogg.Buffer;

namespace deps.jorbis.jorbis
{
    public class Comment
    {
        private static readonly byte[] VorbisBytes = Encoding.UTF8.GetBytes("vorbis");
        private static readonly byte[] VendorBytes = Encoding.UTF8.GetBytes("Xiphophorus libVorbis I 20000508");

        private const int OvEImpl = -130;

        public byte[][] UserComments { get; private set; }
        public int[] CommentLengths { get; private set; }
        public int Comments { get; private set; }
        public byte[] Vendor { get; private set; }

        public void Init()
        {
            UserComments = null;
            Comments = 0;
            Vendor = null;
        }

        public void Add(string comment)
        {
            Add(Encoding.UTF8.GetBytes(comment));
        }

        private void Add(byte[] comment)
        {
            byte[][] foo = new byte[Comments + 2][];
            if (UserComments != null)
            {
                Array.Copy(UserComments, 0, foo, 0, Comments);
            }

            UserComments = foo;

            int[] goo = new int[Comments + 2];
            if (CommentLengths != null)
            {
                Array.Copy(CommentLengths, 0, goo, 0, Comments);
            }

            CommentLengths = goo;

            byte[] bar = new byte[comment.Length + 1];
            Array.Copy(comment, 0, bar, 0, comment.Length);
            UserComments[Comments] = bar;
            CommentLengths[Comments] = comment.Length;
            Comments++;
            UserComments[Comments] = null;
        }

        public void AddTag(string tag, string contents)
        {
            if (string.IsNullOrEmpty(contents))
                contents = "";
            Add(tag + "=" + contents);
        }

        private static bool TagCompare(byte[] s1, byte[] s2, int n)
        {
            int c=0;
            byte u1, u2;
            while(c<n){
                u1=s1[c];
                u2=s2[c];
                if('Z'>=u1&&u1>='A')
                    u1=(byte)(u1-'A'+'a');
                if('Z'>=u2&&u2>='A')
                    u2=(byte)(u2-'A'+'a');
                if(u1!=u2){
                    return false;
                }
                c++;
            }
            return true;
        }

        public string Query(string tag)
        {
            return Query(tag, 0);
        }

        public string Query(string tag, int count)
        {
            int foo = Query(Encoding.UTF8.GetBytes(tag), count);
            if (foo == -1)
                return null;

            byte[] comment = UserComments[foo];
            for (int i = 0; i < CommentLengths[foo]; i++)
            {
                if (comment[i] == '=')
                {
                    return Encoding.UTF8.GetString(comment, i + 1, CommentLengths[foo] - (i + 1));
                }
            }

            return null;
        }

        private int Query(byte[] tag, int count)
        {
            int i = 0;
            int found = 0;
            int fullTagLen = tag.Length + 1;
            byte[] fullTag = new byte[fullTagLen];
            Array.Copy(tag, 0, fullTag, 0, tag.Length);
            fullTag[tag.Length] = (byte)'=';

            for (i = 0; i < Comments; i++)
            {
                if (TagCompare(UserComments[i], fullTag, fullTagLen))
                {
                    if (count == found)
                    {
                        return i;
                    }
                    else
                    {
                        found++;
                    }
                }
            }

            return -1;
        }

        public int Unpack(Buffer opb)
        {
            int vendorlen = opb.Read(32);
            if (vendorlen < 0)
            {
                Clear();
                return -1;
            }

            Vendor = new byte[vendorlen + 1];
            opb.Read(Vendor, vendorlen);

            Comments = opb.Read(32);
            if (Comments < 0)
            {
                Clear();
                return -1;
            }

            UserComments = new byte[Comments + 1][];
            CommentLengths = new int[Comments + 1];

            for (int i = 0; i < Comments; i++)
            {
                int len = opb.Read(32);
                if (len < 0)
                {
                    Clear();
                    return -1;
                }

                CommentLengths[i] = len;
                UserComments[i] = new byte[len + 1];
                opb.Read(UserComments[i], len);
            }

            if (opb.Read(1) != 1)
            {
                Clear();
                return -1;
            }

            return 0;
        }

        public int Pack(Buffer opb)
        {
            opb.Write(0x03, 8);
            opb.Write(VorbisBytes);

            opb.Write(VendorBytes.Length, 32);
            opb.Write(VendorBytes);

            opb.Write(Comments, 32);
            if (Comments != 0)
            {
                for (int i = 0; i < Comments; i++)
                {
                    if (UserComments[i] != null)
                    {
                        opb.Write(CommentLengths[i], 32);
                        opb.Write(UserComments[i]);
                    }
                    else
                    {
                        opb.Write(0, 32);
                    }
                }
            }

            opb.Write(1, 1);
            return 0;
        }

        public int HeaderOut(Packet op)
        {
            Buffer opb = new Buffer();
            opb.WriteInit();

            if (Pack(opb) != 0)
                return OvEImpl;
            
            op.PacketBase = new byte[opb.GetBytes()];
            op.TPacket = 0;
            op.Bytes = opb.GetBytes();
            Array.Copy(opb.BufferData, 0, op.PacketBase, 0, op.Bytes);

            op.BeginningOfStream = 0;
            op.EndOfStream = 0;
            op.GranulePos = 0;
            return 0;
        }

        public void Clear()
        {
            for (int i = 0; i < Comments; i++)
                UserComments[i] = null;
            UserComments = null;
            Vendor = null;
        }

        public string GetVendor()
        {
            return Encoding.UTF8.GetString(Vendor, 0, Vendor.Length - 1);
        }

        public string GetComment(int i)
        {
            if (Comments <= i)
                return null;
            return Encoding.UTF8.GetString(UserComments[i], 0, UserComments[i].Length - 1);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Vendor: ").Append(Encoding.UTF8.GetString(Vendor, 0, Vendor.Length - 1));
            for (int i = 0; i < Comments; i++)
            {
                sb.Append("\nComment: ")
                    .Append(Encoding.UTF8.GetString(UserComments[i], 0, UserComments[i].Length - 1));
            }

            sb.Append("\n");
            return sb.ToString();
        }
    }
}