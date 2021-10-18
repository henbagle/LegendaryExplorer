﻿using System;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class TextureMovie : ObjectBinary
    {
        public bool IsExternal;

        public int DataSize;
        public int DataOffset;
        public byte[] EmbeddedData;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (!sc.Game.IsGame3())
            {
                var empty = new byte[0];
                sc.SerializeBulkData(ref empty);
            }
            sc.Serialize(ref IsExternal);
            if (sc.IsSaving && !IsExternal)
            {
                DataOffset = sc.FileOffset + 12;
                DataSize = EmbeddedData.Length;
            }
            sc.Serialize(ref DataSize);
            sc.Serialize(ref DataSize);
            sc.Serialize(ref DataOffset);
            if (!IsExternal)
            {
                sc.Serialize(ref EmbeddedData, DataSize);
            }
        }

        public static TextureMovie Create()
        {
            return new()
            {
                EmbeddedData = Array.Empty<byte>()
            };
        }
    }
}
