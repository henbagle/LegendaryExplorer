﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using Newtonsoft.Json;

namespace ME3ExplorerCore.Unreal
{
    [DebuggerDisplay("NameReference - Name: {Name} Number: {Number} Instanced: {Instanced}")]
    public readonly struct NameReference : IEquatable<NameReference>
    {
        public string Name { get; }
        public int Number { get; }

        public NameReference(string name, int number = 0)
        {
            Name = name;
            Number = number;
        }

        //https://api.unrealengine.com/INT/API/Runtime/Core/UObject/FName/index.html
        [JsonIgnore]
        public string Instanced => Number > 0 ? $"{Name}_{Number - 1}" : Name;

        public static implicit operator NameReference(string s)
        {
            return new NameReference(s);
        }

        public static implicit operator string(NameReference n)
        {
            return n.Name;
        }

        public override string ToString()
        {
            return Name ?? string.Empty;
        }

        public static bool operator ==(NameReference r, string s)
        {
            return string.Equals(s, r.Name, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator !=(NameReference r, string s)
        {
            return !string.Equals(s, r.Name, StringComparison.OrdinalIgnoreCase);
        }


        public static bool operator ==(string s, NameReference r)
        {
            return string.Equals(s, r.Name, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator !=(string s, NameReference r)
        {
            return !string.Equals(s, r.Name, StringComparison.OrdinalIgnoreCase);
        }

        public static NameReference FromInstancedString(string s)
        {
            int num = 0;
            int _Idx = s.LastIndexOf('_');
            if (_Idx > 0)
            {
                string numComponent = s.Substring(_Idx + 1);
                //if there's a leading zero, it's just part of the string
                if (numComponent.Length > 0 && numComponent[0] != '0' && int.TryParse(numComponent, NumberStyles.None, null, out num))
                {
                    s = s.Substring(0, _Idx);
                    num += 1;
                }
            }
            return new NameReference(s, num);
        }

        #region IEquatable
        public static bool operator ==(NameReference n1, NameReference n2)
        {
            return n1.Equals(n2);
        }

        public static bool operator !=(NameReference n1, NameReference n2)
        {
            return !n1.Equals(n2);
        }
        public bool Equals(NameReference other)
        {
            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) && Number == other.Number;
        }

        public override bool Equals(object obj)
        {
            return obj is NameReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Name.GetHashCode() * 397) ^ Number;
            }
        }
        #endregion
    }


    public readonly struct ScriptDelegate : IEquatable<ScriptDelegate>
    {
        public int Object { get; }
        public NameReference FunctionName { get; }

        public ScriptDelegate(int _object, NameReference functionName)
        {
            Object = _object;
            FunctionName = functionName;
        }

        #region IEquatable

        public bool Equals(ScriptDelegate other)
        {
            return Object == other.Object && FunctionName.Equals(other.FunctionName);
        }

        public override bool Equals(object obj)
        {
            return obj is ScriptDelegate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Object * 397) ^ FunctionName.GetHashCode();
            }
        }

        public static bool operator ==(ScriptDelegate left, ScriptDelegate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScriptDelegate left, ScriptDelegate right)
        {
            return !left.Equals(right);
        }

        #endregion

        public static ScriptDelegate Empty = new ScriptDelegate(0, "None");
    }

    public enum PropertyType
    {
        Unknown = -1,
        None = 0,
        StructProperty = 1,
        IntProperty = 2,
        FloatProperty = 3,
        ObjectProperty = 4,
        NameProperty = 5,
        BoolProperty = 6,
        ByteProperty = 7,
        ArrayProperty = 8,
        StrProperty = 9,
        StringRefProperty = 10,
        DelegateProperty = 11,
        BioMask4Property = 12,
        InterfaceProperty = 13,
        ComponentProperty = 14
    }

    public static class UPropertyExtensions
    {
        public static void WritePropHeader(this EndianWriter stream, IMEPackage pcc, NameReference propName, PropertyType type, int size, int staticArrayIndex)
        {
            stream.WriteNameReference(propName, pcc);
            stream.WriteNameReference(type.ToString(), pcc);
            stream.WriteInt32(size);
            stream.WriteInt32(staticArrayIndex);
        }

        public static void WriteNoneProperty(this EndianWriter stream, IMEPackage pcc)
        {
            //Debug.WriteLine("Writing none property at 0x" + stream.Position.ToString("X6"));
            stream.WriteNameReference("None", pcc);
        }

        public static void WriteStructProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, NameReference structName, Stream value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing struct property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.StructProperty, (int)value.Length, staticArrayIndex);
            stream.WriteNameReference(structName, pcc);
            stream.BaseStream.WriteStream(value);
        }

        public static void WriteStructProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, NameReference structName, Func<Stream> func, int staticArrayIndex)
        {
            stream.WriteStructProperty(pcc, propName, structName, func(), staticArrayIndex);
        }

        public static void WriteIntProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, int value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing int property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.IntProperty, 4, staticArrayIndex);
            stream.WriteInt32(value);
        }

        public static void WriteFloatProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, float value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing float property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.FloatProperty, 4, staticArrayIndex);
            stream.WriteFloat(value);
        }

        public static void WriteObjectProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, int value, int staticArrayIndex, PropertyType propType = PropertyType.ObjectProperty)
        {
            //Debug.WriteLine("Writing bool property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, propType, 4, staticArrayIndex);
            stream.WriteInt32(value);
        }

        public static void WriteNameProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, NameReference value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing name property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.NameProperty, 8, staticArrayIndex);
            stream.WriteNameReference(value, pcc);
        }

        public static void WriteBoolProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, bool value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing bool property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.BoolProperty, 0, staticArrayIndex);
            if (pcc.Game >= MEGame.ME3)
            {
                stream.WriteBoolByte(value);
            }
            else
            {
                stream.WriteBoolInt(value);
            }
        }

        public static void WriteByteProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, byte value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing byte property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));
            stream.WritePropHeader(pcc, propName, PropertyType.ByteProperty, 1, staticArrayIndex);
            if (pcc.Game >= MEGame.ME3)
            {
                stream.WriteNameReference("None", pcc);
            }
            stream.WriteByte(value);
        }

        public static void WriteEnumProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, NameReference enumName, NameReference enumValue, int staticArrayIndex)
        {
            stream.WritePropHeader(pcc, propName, PropertyType.ByteProperty, 8, staticArrayIndex);
            if (pcc.Game >= MEGame.ME3)
            {
                stream.WriteNameReference(enumName, pcc);
            }
            stream.WriteNameReference(enumValue, pcc);
        }

        public static void WriteArrayProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, int count, Stream value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing array property " + propName + ", count: " + count + " at 0x" + stream.Position.ToString("X6")+", length: "+value.Length);
            stream.WritePropHeader(pcc, propName, PropertyType.ArrayProperty, 4 + (int)value.Length, staticArrayIndex);
            stream.WriteInt32(count);
            stream.BaseStream.WriteStream(value);
        }

        public static void WriteArrayProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, int count, Func<Stream> func, int staticArrayIndex)
        {
            stream.WriteArrayProperty(pcc, propName, count, func(), staticArrayIndex);
        }

        public static void WriteStringProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, string value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing string property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));
            int strLen = value.Length == 0 ? 0 : value.Length + 1;
            if (pcc.Game == MEGame.ME3)
            {
                stream.WritePropHeader(pcc, propName, PropertyType.StrProperty, (strLen * 2) + 4, staticArrayIndex);
                stream.WriteUnrealStringUnicode(value);
            }
            else
            {
                stream.WritePropHeader(pcc, propName, PropertyType.StrProperty, strLen + 4, staticArrayIndex);
                stream.WriteUnrealStringASCII(value);
            }
        }

        public static void WriteStringRefProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, int value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing stringref property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.StringRefProperty, 4, staticArrayIndex);
            stream.WriteInt32(value);
        }

        public static void WriteDelegateProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, ScriptDelegate value, int staticArrayIndex)
        {
            stream.WritePropHeader(pcc, propName, PropertyType.DelegateProperty, 12, staticArrayIndex);
            stream.WriteInt32(value.Object);
            stream.WriteNameReference(value.FunctionName, pcc);
        }

        public static StructProperty ToGuidStructProp(this Guid guid, NameReference propName) => CommonStructs.GuidProp(guid, propName);
    }
}
