/*
 * C# Stream Helpers
 *
 * Copyright (C) 2015-2018 Pawel Kolodziejski
 * Copyright (C) 2019 ME3Explorer
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Memory;

namespace ME3ExplorerCore.Helpers
{
    public static class StreamHelpers
    {

        public static MemoryStream ReadToMemoryStream(this Stream stream, long size)
        {
            var memory = MemoryManager.GetMemoryStream((int)size);

            var left = size;
            var data = new byte[4096];
            while (left > 0)
            {
                var block = (int)(Math.Min(left, 4096));
                stream.Read(data, 0, block);
                memory.Write(data, 0, block);
                left -= block;
            }

            memory.Seek(0, SeekOrigin.Begin);
            return memory;
        }


        public static byte[] ReadToBuffer(this Stream stream, int count)
        {
            var buffer = new byte[count];
            if (stream.Read(buffer, 0, count) != count)
                throw new Exception("Stream read error!");
            return buffer;
        }

        public static byte[] ReadToBuffer(this Stream stream, uint count)
        {
            return stream.ReadToBuffer((int)count);
        }

        public static byte[] ReadToBuffer(this Stream stream, long count)
        {
            return stream.ReadToBuffer((int)count);
        }

        public static void WriteFromBuffer(this Stream stream, byte[] buffer)
        {
            stream.Write(buffer, 0, buffer.Length);
        }

        public static Guid ReadGuid(this EndianReader stream)
        {
            var a = stream.ReadInt32();
            var b = stream.ReadInt16();
            var c = stream.ReadInt16();
            var d = stream.ReadBytes(8);

            return new Guid(a, b, c, d);
        }

        public static Guid ReadGuid(this Stream stream)
        {
            var a = stream.ReadInt32();
            var b = stream.ReadInt16();
            var c = stream.ReadInt16();
            var d = stream.ReadToBuffer(8);

            return new Guid(a, b, c, d);
        }

        public static void WriteGuid(this Stream stream, Guid value)
        {
            var data = value.ToByteArray();

            Debug.Assert(data.Length == 16);

            stream.WriteInt32(BitConverter.ToInt32(data, 0));
            stream.WriteInt16(BitConverter.ToInt16(data, 4));
            stream.WriteInt16(BitConverter.ToInt16(data, 6));
            stream.Write(data, 8, 8);
        }

        public static void WriteGuid(this EndianWriter stream, Guid value)
        {
            var data = value.ToByteArray();

            Debug.Assert(data.Length == 16);

            stream.WriteInt32(BitConverter.ToInt32(data, 0));
            stream.WriteInt16(BitConverter.ToInt16(data, 4));
            stream.WriteInt16(BitConverter.ToInt16(data, 6));
            stream.Write(data, 8, 8);
        }


        public static void WriteToFile(this MemoryStream stream, string outfile)
        {
            long oldPos = stream.Position;
            stream.Position = 0;
            using (FileStream file = new FileStream(outfile, FileMode.Create, System.IO.FileAccess.Write))
                stream.CopyTo(file);
            stream.Position = oldPos;
        }

        public static void WriteToFile(this EndianReader stream, string outfile)
        {
            long oldPos = stream.Position;
            stream.Position = 0;
            using (FileStream file = new FileStream(outfile, FileMode.Create, System.IO.FileAccess.Write))
                stream.BaseStream.CopyTo(file);
            stream.Position = oldPos;
        }

        public static void WriteFromStream(this Stream stream, Stream inputStream, int count)
        {
            var buffer = new byte[0x10000];
            do
            {
                int readed = inputStream.Read(buffer, 0, Math.Min(buffer.Length, count));
                if (readed > 0)
                    stream.Write(buffer, 0, readed);
                else
                    break;
                count -= readed;
            } while (count != 0);
        }

        public static void WriteFromStream(this Stream stream, Stream inputStream, uint count)
        {
            WriteFromStream(stream, inputStream, (int)count);
        }

        public static void WriteFromStream(this Stream stream, Stream inputStream, long count)
        {
            WriteFromStream(stream, inputStream, (int)count);
        }

        public static string ReadStringASCII(this Stream stream, int count)
        {
            byte[] buffer = stream.ReadToBuffer(count);
            return Encoding.ASCII.GetString(buffer);
        }

        public static string ReadStringASCIINull(this Stream stream)
        {
            string str = "";
            for (; ; )
            {
                char c = (char)stream.ReadByte();
                if (c == 0)
                    break;
                str += c;
            }
            return str;
        }

        public static string ReadStringASCIINull(this Stream stream, int count)
        {
            return stream.ReadStringASCII(count).Trim('\0');
        }

        public static string ReadStringUnicode(this Stream stream, int count)
        {
            var buffer = stream.ReadToBuffer(count);
            return Encoding.Unicode.GetString(buffer);
        }

        public static string ReadStringUnicodeNull(this Stream stream, int count)
        {
            return stream.ReadStringUnicode(count).Trim('\0');
        }

        public static void WriteStringASCII(this Stream stream, string str)
        {
            stream.Write(Encoding.ASCII.GetBytes(str), 0, Encoding.ASCII.GetByteCount(str));
        }

        public static void WriteStringASCIINull(this Stream stream, string str)
        {
            stream.WriteStringASCII(str + "\0");
        }

        public static void WriteStringASCII(this EndianWriter stream, string str)
        {
            stream.Write(Encoding.ASCII.GetBytes(str), 0, Encoding.ASCII.GetByteCount(str));
        }

        public static void WriteStringASCIINull(this EndianWriter stream, string str)
        {
            stream.WriteStringASCII(str + "\0");
        }

        public static void WriteStringUnicode(this Stream stream, string str)
        {
            stream.Write(Encoding.Unicode.GetBytes(str), 0, Encoding.Unicode.GetByteCount(str));
        }

        public static void WriteStringUnicodeNull(this Stream stream, string str)
        {
            stream.WriteStringUnicode(str + "\0");
        }

        public static void WriteStringUnicode(this EndianWriter stream, string str)
        {
            stream.Write(Encoding.Unicode.GetBytes(str), 0, Encoding.Unicode.GetByteCount(str));
        }

        public static void WriteStringUnicodeNull(this EndianWriter stream, string str)
        {
            stream.WriteStringUnicode(str + "\0");
        }

        public static ulong ReadUInt64(this Stream stream)
        {
            var buffer = new byte[sizeof(ulong)];
            if (stream.Read(buffer, 0, sizeof(ulong)) != sizeof(ulong))
                throw new Exception();
            return BitConverter.ToUInt64(buffer, 0);
        }

        public static void WriteUInt64(this Stream stream, ulong data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(ulong));
        }

        public static long ReadInt64(this Stream stream)
        {
            var buffer = new byte[sizeof(long)];
            if (stream.Read(buffer, 0, sizeof(long)) != sizeof(long))
                throw new Exception();
            return BitConverter.ToInt64(buffer, 0);
        }

        public static void WriteInt64(this Stream stream, long data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(long));
        }

        public static uint ReadUInt32(this Stream stream)
        {
            var buffer = new byte[sizeof(uint)];
            if (stream.Read(buffer, 0, sizeof(uint)) != sizeof(uint))
                throw new Exception();
            return BitConverter.ToUInt32(buffer, 0);
        }

        public static void WriteUInt32(this Stream stream, uint data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(uint));
        }

        public static int ReadInt32(this Stream stream)
        {
            var buffer = new byte[sizeof(int)];
            if (stream.Read(buffer, 0, sizeof(int)) != sizeof(int))
                throw new Exception();
            return BitConverter.ToInt32(buffer, 0);
        }

        public static void WriteInt32(this Stream stream, int data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(int));
        }

        /// <summary>
        /// Writes the stream to file from the beginning. This should only be used on streams that support seeking. The position is restored after the file has been written.
        /// </summary>
        /// <param name="stream">Stream to write from</param>
        /// <param name="outfile">File to write to</param>
        public static void WriteToFile(this Stream stream, string outfile)
        {
            long oldPos = stream.Position;
            stream.Position = 0;
            using (FileStream file = new FileStream(outfile, FileMode.Create, System.IO.FileAccess.Write))
                stream.CopyTo(file);
            stream.Position = oldPos;
        }

        public static ushort ReadUInt16(this Stream stream)
        {
            var buffer = new byte[sizeof(ushort)];
            if (stream.Read(buffer, 0, sizeof(ushort)) != sizeof(ushort))
                throw new Exception();
            return BitConverter.ToUInt16(buffer, 0);
        }

        public static void WriteUInt16(this Stream stream, ushort data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(ushort));
        }

        public static short ReadInt16(this Stream stream)
        {
            var buffer = new byte[sizeof(short)];
            if (stream.Read(buffer, 0, sizeof(short)) != sizeof(short))
                throw new Exception();
            return BitConverter.ToInt16(buffer, 0);
        }

        public static void WriteInt16(this Stream stream, short data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(short));
        }

        public static float ReadFloat16(this Stream stream)
        {
            var buffer = new byte[sizeof(ushort)];
            if (stream.Read(buffer, 0, sizeof(ushort)) != sizeof(ushort))
                throw new Exception();
            ushort u = BitConverter.ToUInt16(buffer, 0);
            int sign = (u >> 15) & 0x00000001;
            int exp = (u >> 10) & 0x0000001F;
            int mant = u & 0x000003FF;
            switch (exp)
            {
                case 0:
                    return 0f;
                case 31:
                    return 65504f;
            }
            exp += (127 - 15);
            int i = (sign << 31) | (exp << 23) | (mant << 13);
            return BitConverter.ToSingle(BitConverter.GetBytes(i), 0);
        }

        public static float ReadFloat(this Stream stream)
        {
            var buffer = new byte[sizeof(float)];
            if (stream.Read(buffer, 0, sizeof(float)) != sizeof(float))
                throw new Exception();
            return BitConverter.ToSingle(buffer, 0);
        }

        public static void WriteFloat(this Stream stream, float data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(float));
        }

        public static double ReadDouble(this Stream stream)
        {
            var buffer = new byte[sizeof(double)];
            if (stream.Read(buffer, 0, sizeof(double)) != sizeof(double))
                throw new Exception();
            return BitConverter.ToDouble(buffer, 0);
        }

        public static void WriteDouble(this Stream stream, double data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(double));
        }

        private const int DefaultBufferSize = 8 * 1024;


        /// <summary>
        ///     Reads the given stream up to the end, returning the data as a byte
        ///     array.
        /// </summary>
        /// <param name="input">The stream to read from</param>
        /// <exception cref="ArgumentNullException">input is null</exception>
        /// <exception cref="IOException">An error occurs while reading from the stream</exception>
        /// <returns>The data read from the stream</returns>
        public static byte[] ReadFully(this Stream input)
        {
            return ReadFully(input, DefaultBufferSize);
        }

        /// <summary>
        ///     Reads the given stream up to the end, returning the data as a byte
        ///     array, using the given buffer size.
        /// </summary>
        /// <param name="input">The stream to read from</param>
        /// <param name="bufferSize">The size of buffer to use when reading</param>
        /// <exception cref="ArgumentNullException">input is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">bufferSize is less than 1</exception>
        /// <exception cref="IOException">An error occurs while reading from the stream</exception>
        /// <returns>The data read from the stream</returns>
        public static byte[] ReadFully(this Stream input, int bufferSize)
        {
            if (bufferSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }
            return ReadFully(input, new byte[bufferSize]);
        }

        /// <summary>
        ///     Reads the given stream up to the end, returning the data as a byte
        ///     array, using the given buffer for transferring data. Note that the
        ///     current contents of the buffer is ignored, so the buffer needn't
        ///     be cleared beforehand.
        /// </summary>
        /// <param name="input">The stream to read from</param>
        /// <param name="buffer">The buffer to use to transfer data</param>
        /// <exception cref="ArgumentNullException">input is null</exception>
        /// <exception cref="ArgumentNullException">buffer is null</exception>
        /// <exception cref="IOException">An error occurs while reading from the stream</exception>
        /// <returns>The data read from the stream</returns>
        public static byte[] ReadFully(this Stream input, IBuffer buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            return ReadFully(input, buffer.Bytes);
        }

        /// <summary>
        ///     Reads the given stream up to the end, returning the data as a byte
        ///     array, using the given buffer for transferring data. Note that the
        ///     current contents of the buffer is ignored, so the buffer needn't
        ///     be cleared beforehand.
        /// </summary>
        /// <param name="input">The stream to read from</param>
        /// <param name="buffer">The buffer to use to transfer data</param>
        /// <exception cref="ArgumentNullException">input is null</exception>
        /// <exception cref="ArgumentNullException">buffer is null</exception>
        /// <exception cref="ArgumentException">buffer is a zero-length array</exception>
        /// <exception cref="IOException">An error occurs while reading from the stream</exception>
        /// <returns>The data read from the stream</returns>
        public static byte[] ReadFully(this Stream input, byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            if (buffer.Length == 0)
            {
                throw new ArgumentException("Buffer has length of 0");
            }
            // We could do all our own work here, but using MemoryStream is easier
            // and likely to be just as efficient.
            using (var tempStream = new MemoryStream()) // do not use memory manager as GetBuffer() should not be used with it
            {
                Copy(input, tempStream, buffer);
                // No need to copy the buffer if it's the right size
                if (tempStream.Length == tempStream.GetBuffer().Length)
                {
                    return tempStream.GetBuffer();
                }
                // Okay, make a copy that's the right size
                return tempStream.ToArray();
            }
        }

        /// <summary>
        ///     Copies all the data from one stream into another, using the given
        ///     buffer for transferring data. Note that the current contents of
        ///     the buffer is ignored, so the buffer needn't be cleared beforehand.
        /// </summary>
        /// <param name="input">The stream to read from</param>
        /// <param name="output">The stream to write to</param>
        /// <param name="buffer">The buffer to use to transfer data</param>
        /// <exception cref="ArgumentNullException">input is null</exception>
        /// <exception cref="ArgumentNullException">output is null</exception>
        /// <exception cref="ArgumentNullException">buffer is null</exception>
        /// <exception cref="ArgumentException">buffer is a zero-length array</exception>
        /// <exception cref="IOException">An error occurs while reading or writing</exception>
        public static void Copy(this Stream input, Stream output, byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (buffer.Length == 0)
            {
                throw new ArgumentException("Buffer has length of 0");
            }

            int read;

            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
            }
        }

        public static bool ReadBoolByte(this Stream stream)
        {
            return stream.ReadByte() > 0;
        }

        public static void WriteBoolByte(this Stream stream, bool data)
        {
            stream.WriteByte((byte)(data ? 1 : 0));
        }

        public static bool ReadBoolInt(this Stream stream)
        {
            return stream.ReadUInt32() > 0;
        }

        public static void WriteBoolInt(this Stream stream, bool data)
        {
            stream.WriteInt32(data ? 1 : 0);
        }

        public static void WriteZeros(this Stream stream, uint count)
        {
            for (int i = 0; i < count; i++)
                stream.WriteByte(0);
        }

        public static void WriteZeros(this Stream stream, int count)
        {
            WriteZeros(stream, (uint)count);
        }

        public static Stream SeekBegin(this Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public static Stream SeekEnd(this Stream stream)
        {
            stream.Seek(0, SeekOrigin.End);
            return stream;
        }

        public static Stream JumpTo(this Stream stream, int offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            return stream;
        }

        public static Stream JumpTo(this Stream stream, uint offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            return stream;
        }

        public static Stream JumpTo(this Stream stream, long offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            return stream;
        }

        public static Stream Skip(this Stream stream, int count)
        {
            stream.Seek(count, SeekOrigin.Current);
            return stream;
        }

        public static Stream Skip(this Stream stream, uint count)
        {
            stream.Seek(count, SeekOrigin.Current);
            return stream;
        }

        public static Stream Skip(this Stream stream, long count)
        {
            stream.Seek(count, SeekOrigin.Current);
            return stream;
        }

        public static Stream SkipByte(this Stream stream)
        {
            stream.Seek(1, SeekOrigin.Current);
            return stream;
        }

        public static Stream SkipInt16(this Stream stream)
        {
            stream.Seek(2, SeekOrigin.Current);
            return stream;
        }

        public static Stream SkipInt32(this Stream stream)
        {
            stream.Seek(4, SeekOrigin.Current);
            return stream;
        }

        public static Stream SkipInt64(this Stream stream)
        {
            stream.Seek(8, SeekOrigin.Current);
            return stream;
        }

        public static Stream SkipString(this Stream stream, bool unicode)
        {
            return unicode ? stream.SkipStringUnicode() : stream.SkipStringASCII();
        }

        public static Stream SkipStringUnicode(this Stream stream)
        {
            stream.Skip(stream.ReadInt32() * -2);
            return stream;
        }

        public static Stream SkipStringASCII(this Stream stream)
        {
            stream.Skip(stream.ReadInt32());
            return stream;
        }
    }
}
