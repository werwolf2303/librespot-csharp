using System;
using System.IO;

public static class BinaryReaderExtensions
{
    public static int ReadFully(this BinaryReader reader, byte[] buffer)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));

        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = reader.Read(buffer, offset, buffer.Length - offset);
            if (read == 0)
            {
                throw new EndOfStreamException("Stream ended before reading expected bytes");
            }
            offset += read;
        }

        return offset;
    }
}