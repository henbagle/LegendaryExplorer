﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.TLK
{
    public sealed class TLKBitArray
    {
        public readonly int Length;
        private readonly int[] intArray;

        private TLKBitArray(int bytesLength)
        {
            Length = bytesLength * 8;
            int arrLen = Math.DivRem(bytesLength, sizeof(int), out int remainder);
            if (remainder > 0)
            {
                ++arrLen;
            }
            intArray = new int[arrLen];
        }

        public TLKBitArray(Stream reader, int bytesLength) : this(bytesLength)
        {
            //reinterpret the intArray as a byte array, then slice it to the bytesLength (since it might not be a multiple of four)
            reader.Read(MemoryMarshal.AsBytes(intArray.AsSpan())[..bytesLength]);
        }

        public TLKBitArray(byte[] bytes) : this(bytes.Length)
        {
            bytes.AsSpan().CopyTo(MemoryMarshal.AsBytes(intArray.AsSpan()));
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Get(int i)=> (intArray[i >> 5] & (1 << i)) != 0;
        
    }
}
