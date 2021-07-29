﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.AssetDatabase
{
    /*
     * READ THIS BEFORE MODIFYING DATABASE CLASSES!
     * BinaryPack does not work with ValueTuples, and it requires classes to have a parameterless constructor!
     * That is why all the records have seemingly useless contructors.
     */

    /// <summary>
    /// Database of all found records generated from AssetDatabase
    /// </summary>
    public class AssetDB
    {
        public MEGame meGame { get; set; }
        public string GenerationDate { get; set; }
        public string DataBaseversion { get; set; }

        public List<FileNameDirKeyPair> FileList { get; set; } = new();
        public List<string> ContentDir { get; set; } = new();

        public List<ClassRecord> ClassRecords { get; set; } = new();

        public List<MaterialRecord> Materials { get; set; } = new();

        public List<AnimationRecord> Animations { get; set; } = new();

        public List<MeshRecord> Meshes { get; set; } = new();

        public List<ParticleSysRecord> Particles { get; set; } = new();

        public List<TextureRecord> Textures { get; set; } = new();

        public List<GUIElement> GUIElements { get; set; } = new();

        public List<Conversation> Conversations { get; set; } = new();

        public List<ConvoLine> Lines { get; set; } = new();

        public PlotUsageDB PlotUsages { get; set; } = new();

        public AssetDB(MEGame meGame, string GenerationDate, string DataBaseversion, IEnumerable<FileNameDirKeyPair> FileList, IEnumerable<string> ContentDir)
        {
            this.meGame = meGame;
            this.GenerationDate = GenerationDate;
            this.DataBaseversion = DataBaseversion;
            this.FileList.AddRange(FileList);
            this.ContentDir.AddRange(ContentDir);
        }

        public AssetDB()
        { }

        public void Clear()
        {
            GenerationDate = null;
            FileList.Clear();
            ContentDir.Clear();
            ClearRecords();
        }

        public void ClearRecords()
        {
            ClassRecords.Clear();
            Animations.Clear();
            Materials.Clear();
            Meshes.Clear();
            Particles.Clear();
            Textures.Clear();
            GUIElements.Clear();
            Conversations.Clear();
            Lines.Clear();
            PlotUsages.ClearRecords();
        }

        public void AddRecords(AssetDB from)
        {
            ClassRecords.AddRange(from.ClassRecords);
            Animations.AddRange(from.Animations);
            Materials.AddRange(from.Materials);
            Meshes.AddRange(from.Meshes);
            Particles.AddRange(from.Particles);
            Textures.AddRange(from.Textures);
            GUIElements.AddRange(from.GUIElements);
            Conversations.AddRange(from.Conversations);
            Lines.AddRange(from.Lines);
            PlotUsages.AddRecords(from.PlotUsages);
        }

    }
    public sealed record FileNameDirKeyPair(string FileName, int DirectoryKey) { public FileNameDirKeyPair() : this(default, default) { } }

    public class PlotUsageDB
    {
        public List<PlotRecord> Bools { get; set; } = new();
        public List<PlotRecord> Ints { get; set; } = new();
        public List<PlotRecord> Floats { get; set; } = new();
        public List<PlotRecord> Conditionals { get; set; } = new();
        public List<PlotRecord> Transitions { get; set; } = new();
        public PlotUsageDB()
        {

        }

        public void ClearRecords()
        {
            Bools.Clear();
            Ints.Clear();
            Floats.Clear();
            Conditionals.Clear();
            Transitions.Clear();
        }

        public void AddRecords(PlotUsageDB fromDb)
        {
            Bools.AddRange(fromDb.Bools);
            Ints.AddRange(fromDb.Ints);
            Floats.AddRange(fromDb.Floats);
            Conditionals.AddRange(fromDb.Conditionals);
            Transitions.AddRange(fromDb.Transitions);
        }

        public bool Any()
        {
            return Bools.Any() || Ints.Any() || Floats.Any() || Conditionals.Any() || Transitions.Any();
        }
    }

    public class ClassRecord
    {
        public string Class { get; set; }

        public string Definition_package { get; set; }

        public int Definition_UID { get; set; }

        public string SuperClass { get; set; }

        public bool IsModOnly { get; set; }

        public HashSet<PropertyRecord> PropertyRecords { get; set; } = new();

        public List<ClassUsage> Usages { get; set; } = new();

        public ClassRecord(string Class, string Definition_package, int Definition_UID, string SuperClass)
        {
            this.Class = Class;
            this.Definition_package = Definition_package;
            this.Definition_UID = Definition_UID;
            this.SuperClass = SuperClass;
        }

        public ClassRecord()
        { }
    }
    public sealed record PropertyRecord(string Property, string Type) { public PropertyRecord() : this(default, default) { } }


    public class ClassUsage
    {

        public int FileKey { get; set; }

        public int UIndex { get; set; }

        public bool IsDefault { get; set; }

        public bool IsMod { get; set; }

        public ClassUsage(int FileKey, int uIndex, bool IsDefault, bool IsMod)
        {
            this.FileKey = FileKey;
            this.UIndex = uIndex;
            this.IsDefault = IsDefault;
            this.IsMod = IsMod;
        }
        public ClassUsage()
        { }
    }

    public class MaterialRecord
    {

        public string MaterialName { get; set; }

        public string ParentPackage { get; set; }

        public bool IsDLCOnly { get; set; }

        public List<MatUsage> Usages { get; set; } = new();

        public List<MatSetting> MatSettings { get; set; } = new();

        public MaterialRecord(string MaterialName, string ParentPackage, bool IsDLCOnly, IEnumerable<MatSetting> MatSettings)
        {
            this.MaterialName = MaterialName;
            this.ParentPackage = ParentPackage;
            this.IsDLCOnly = IsDLCOnly;
            this.MatSettings.AddRange(MatSettings);
        }

        public MaterialRecord()
        { }
    }

    public sealed record MatUsage(int FileKey, int UIndex, bool IsInDLC) { public MatUsage() : this(default, default, default) { } }
    public sealed record MatSetting(string Name, string Parm1, string Parm2) { public MatSetting() : this(default, default, default) { } }


    public class AnimationRecord
    {

        public string AnimSequence { get; set; }

        public string SeqName { get; set; }

        public string AnimData { get; set; }

        public float Length { get; set; }

        public int Frames { get; set; }

        public string Compression { get; set; }

        public string KeyFormat { get; set; }

        public bool IsAmbPerf { get; set; }

        public bool IsModOnly { get; set; }

        public List<AnimUsage> Usages { get; set; } = new();

        public AnimationRecord(string AnimSequence, string SeqName, string AnimData, float Length, int Frames, string Compression, string KeyFormat, bool IsAmbPerf, bool IsModOnly)
        {
            this.AnimSequence = AnimSequence;
            this.SeqName = SeqName;
            this.AnimData = AnimData;
            this.Length = Length;
            this.Frames = Frames;
            this.Compression = Compression;
            this.KeyFormat = KeyFormat;
            this.IsAmbPerf = IsAmbPerf;
            this.IsModOnly = IsModOnly;
        }

        public AnimationRecord()
        { }
    }

    public sealed record AnimUsage(int FileKey, int UIndex, bool IsInMod)
    {
        public AnimUsage() : this(default, default, default)
        {

        }
    }


    public class MeshRecord
    {

        public string MeshName { get; set; }

        public bool IsSkeleton { get; set; }

        public int BoneCount { get; set; }

        public bool IsModOnly { get; set; }

        public List<MeshUsage> Usages { get; set; } = new();

        public MeshRecord(string MeshName, bool IsSkeleton, bool IsModOnly, int BoneCount)
        {
            this.MeshName = MeshName;
            this.IsSkeleton = IsSkeleton;
            this.BoneCount = BoneCount;
            this.IsModOnly = IsModOnly;
        }

        public MeshRecord()
        { }
    }
    public sealed record MeshUsage(int FileKey, int UIndex, bool IsInMod) { public MeshUsage() : this(default, default, default) { } }


    public class ParticleSysRecord
    {
        public enum VFXClass
        {
            ParticleSystem,
            RvrClientEffect,
            BioVFXTemplate
        }


        public string PSName { get; set; }

        public string ParentPackage { get; set; }

        public bool IsDLCOnly { get; set; }

        public bool IsModOnly { get; set; }

        public int EffectCount { get; set; }

        public VFXClass VFXType { get; set; }

        public List<ParticleSysUsage> Usages { get; set; } = new();

        public ParticleSysRecord(string PSName, string ParentPackage, bool IsDLCOnly, bool IsModOnly, int EffectCount, VFXClass VFXType)
        {
            this.PSName = PSName;
            this.ParentPackage = ParentPackage;
            this.IsDLCOnly = IsDLCOnly;
            this.IsModOnly = IsModOnly;
            this.EffectCount = EffectCount;
            this.VFXType = VFXType;
        }

        public ParticleSysRecord()
        { }
    }
    public sealed record ParticleSysUsage(int FileKey, int UIndex, bool IsInDLC, bool IsInMod) { public ParticleSysUsage() : this(default, default, default, default) { } }


    public class TextureRecord
    {

        public string TextureName { get; set; }

        public string ParentPackage { get; set; }

        public bool IsDLCOnly { get; set; }

        public bool IsModOnly { get; set; }

        public string CFormat { get; set; }

        public string TexGrp { get; set; }

        public int SizeX { get; set; }

        public int SizeY { get; set; }

        public string CRC { get; set; }

        public List<TextureUsage> Usages { get; set; } = new();

        public TextureRecord(string TextureName, string ParentPackage, bool IsDLCOnly, bool IsModOnly, string CFormat, string TexGrp, int SizeX, int SizeY, string CRC)
        {
            this.TextureName = TextureName;
            this.ParentPackage = ParentPackage;
            this.IsDLCOnly = IsDLCOnly;
            this.IsModOnly = IsModOnly;
            this.CFormat = CFormat;
            this.TexGrp = TexGrp;
            this.SizeX = SizeX;
            this.SizeY = SizeY;
            this.CRC = CRC;
        }

        public TextureRecord()
        { }
    }
    public sealed record TextureUsage(int FileKey, int UIndex, bool IsInDLC, bool IsInMod) { public TextureUsage() : this(default, default, default, default) { } }


    public class GUIElement
    {

        public string GUIName { get; set; }

        public int DataSize { get; set; }

        public bool IsModOnly { get; set; }

        public List<GUIUsage> Usages { get; set; } = new(); //File reference then export

        public GUIElement(string GUIName, int DataSize, bool IsModOnly)
        {
            this.GUIName = GUIName;
            this.DataSize = DataSize;
            this.IsModOnly = IsModOnly;
        }

        public GUIElement()
        { }
    }
    public sealed record GUIUsage(int FileKey, int UIndex, bool IsInMod) { public GUIUsage() : this(default, default, default) { } }


    public class Conversation
    {

        public string ConvName { get; set; }

        public bool IsAmbient { get; set; }

        public FileKeyExportPair ConvFile { get; set; } //file, export
        public Conversation(string ConvName, bool IsAmbient, FileKeyExportPair ConvFile)
        {
            this.ConvName = ConvName;
            this.IsAmbient = IsAmbient;
            this.ConvFile = ConvFile;
        }

        public Conversation()
        { }
    }
    public sealed record FileKeyExportPair(int File, int ExportUIndex) { public FileKeyExportPair() : this(default, default) { } }

    public class ConvoLine
    {

        public int StrRef { get; set; }

        public string Speaker { get; set; }

        public string Line { get; set; }

        public string Convo { get; set; }

        public ConvoLine(int StrRef, string Speaker, string Convo)
        {
            this.StrRef = StrRef;
            this.Speaker = Speaker;
            this.Convo = Convo;
        }

        public ConvoLine()
        { }
    }

    public enum PlotRecordType
    {
        Bool,
        Int,
        Float,
        Conditional,
        Transition
    }

    public class PlotRecord
    {
        public PlotRecordType ElementType { get; set; }

        public int ElementID { get; set; }

        public List<PlotUsage> SetBy { get; set; } = new();
        public List<PlotUsage> ReadBy { get; set; } = new();

        public PlotRecord(PlotRecordType type, int id)
        {
            this.ElementType = type;
            this.ElementID = id;
        }

        public PlotRecord()
        { }
    }

    public class PlotUsage
    {

        public int FileKey { get; set; }

        public int UIndex { get; set; }

        public bool IsMod { get; set; }

        public PlotUsage(int FileKey, int uIndex, bool IsMod)
        {
            this.FileKey = FileKey;
            this.UIndex = uIndex;
            this.IsMod = IsMod;
        }
        public PlotUsage()
        { }
    }
}
