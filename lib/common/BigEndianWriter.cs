using System;
using System.IO;

public static class BigEndianWriter
{
    public static void WriteBigEndian(this BinaryWriter writer, short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    public static void WriteBigEndian(this BinaryWriter writer, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    public static void WriteBigEndian(this BinaryWriter writer, long value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    public static void WriteBigEndian(this BinaryWriter writer, ushort value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    public static void WriteBigEndian(this BinaryWriter writer, uint value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    public static void WriteBigEndian(this BinaryWriter writer, ulong value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    public static void WriteBigEndian(this BinaryWriter writer, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    public static void WriteBigEndian(this BinaryWriter writer, double value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    public static void Clear(this BinaryWriter writer)
    {
        writer.BaseStream.Position = 0;
        writer.BaseStream.SetLength(0);
    }
}