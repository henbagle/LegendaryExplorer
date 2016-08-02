using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gibbed.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using System.Diagnostics;
using ME3Explorer.Unreal;
using System.Windows;

namespace ME3Explorer.Packages
{
    public class ME3Package : MEPackage, IMEPackage
    {
        public MEGame Game { get { return MEGame.ME3; } }

        static int headerSize = 0x8E;

        public bool IsModified {
            get
            {
                return exports.Any(entry => entry.hasChanged == true) || imports.Any(entry => entry.hasChanged == true || namesAdded > 0);
            }
        }
        public bool CanReconstruct { get { return !exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"); } }

        private int idxOffsets { get { if ((flags & 8) != 0) return 24 + nameSize; else return 20 + nameSize; } } // usually = 34
        private int NameCount
        {
            get { return BitConverter.ToInt32(header, idxOffsets); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 68, sizeof(int));
            }
        }
        public int NameOffset
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 4); }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 4, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 100, sizeof(int));
            }
        }
        public int ExportCount
        {
            get { return BitConverter.ToInt32(header, idxOffsets + 8); }
            private set
            {
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 8, sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 64, sizeof(int));
            }
        }
        private int ExportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 12); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 12, sizeof(int)); } }
        public int ImportCount { get { return BitConverter.ToInt32(header, idxOffsets + 16); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 16, sizeof(int)); } }
        public int ImportOffset { get { return BitConverter.ToInt32(header, idxOffsets + 20); } private set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 20, sizeof(int)); } }
        private int FreeZoneStart { get { return BitConverter.ToInt32(header, idxOffsets + 24); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 24, sizeof(uint)); } }
        private int FreeZoneEnd { get { return BitConverter.ToInt32(header, idxOffsets + 28); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, idxOffsets + 28, sizeof(uint)); } }
        
        private List<ME3ImportEntry> imports;
        private List<ME3ExportEntry> exports;

        public IReadOnlyList<IExportEntry> Exports
        {
            get
            {
                return exports;
            }
        }

        public IReadOnlyList<IImportEntry> Imports
        {
            get
            {
                return imports;
            }
        }

        static bool isInitialized;
        public static Func<string, ME3Package> Initialize()
        {
            if (isInitialized)
            {
                throw new Exception(nameof(ME3Package) + " can only be initialized once");
            }
            else
            {
                isInitialized = true;
                return f => new ME3Package(f);
            }
        }

        /// <summary>
        ///     PCCObject class constructor. It also loads namelist, importlist, exportinfo, and exportdata from pcc file
        /// </summary>
        /// <param name="pccFilePath">full path + file name of desired pcc file.</param>
        private ME3Package(string pccFilePath)
        {
            FileName = Path.GetFullPath(pccFilePath);
            lastSaved = null;
            MemoryStream listsStream;
            names = new List<string>();
            imports = new List<ME3ImportEntry>();
            exports = new List<ME3ExportEntry>();
            using (FileStream pccStream = File.OpenRead(FileName))
            {
                header = pccStream.ReadBytes(headerSize);
                if (magic != ZBlock.magic &&
                    magic.Swap() != ZBlock.magic)
                {
                    throw new FormatException("not a pcc file");
                }

                if (lowVers != 684 && highVers != 194)
                {
                    throw new FormatException("unsupported version");
                }
                
                if (IsCompressed)
                {
                    listsStream = CompressionHelper.DecompressME3(pccStream);

                    //correcting the header
                    listsStream.Seek(0, SeekOrigin.Begin);
                    listsStream.Read(header, 0, header.Length);
                }
                else
                {
                    listsStream = new MemoryStream();
                    pccStream.Seek(0, SeekOrigin.Begin);
                    pccStream.CopyTo(listsStream);
                }
            }

            // fill names list
            listsStream.Seek(NameOffset, SeekOrigin.Begin);
            for (int i = 0; i < NameCount; i++)
            {
                long currOffset = listsStream.Position;
                int strLength = listsStream.ReadValueS32();
                string str = listsStream.ReadString(strLength * -2, true, Encoding.Unicode);
                names.Add(str);
            }

            // fill import list
            listsStream.Seek(ImportOffset, SeekOrigin.Begin);
            byte[] buffer = new byte[ImportEntry.byteSize];
            for (int i = 0; i < ImportCount; i++)
            {

                long offset = listsStream.Position;
                ME3ImportEntry e = new ME3ImportEntry(this, listsStream);
                imports.Add(e);
            }

            // fill export list
            listsStream.Seek(ExportOffset, SeekOrigin.Begin);
            for (int i = 0; i < ExportCount; i++)
            {
                uint expInfoOffset = (uint)listsStream.Position;

                listsStream.Seek(44, SeekOrigin.Current);
                int count = listsStream.ReadValueS32();
                listsStream.Seek(-48, SeekOrigin.Current);

                int expInfoSize = 68 + (count * 4);
                buffer = new byte[expInfoSize];

                listsStream.Read(buffer, 0, buffer.Length);
                ME3ExportEntry e = new ME3ExportEntry(this, buffer, expInfoOffset);
                long headerEnd = listsStream.Position;

                buffer = new byte[e.DataSize];
                listsStream.Seek(e.DataOffset, SeekOrigin.Begin);
                listsStream.Read(buffer, 0, buffer.Length);
                e.Data = buffer;
                e.hasChanged = false;
                exports.Add(e);
                listsStream.Seek(headerEnd, SeekOrigin.Begin);
            }
        }

        /// <summary>
        ///     save PCC to same file by reconstruction if possible, append if not
        /// </summary>
        public void save()
        {
            save(FileName);
        }

        /// <summary>
        ///     save PCC by reconstruction if possible, append if not
        /// </summary>
        /// <param name="path">full path + file name.</param>
        public void save(string path)
        {
            if (CanReconstruct)
            {
                saveByReconstructing(path);
            }
            else
            {
                appendSave(path);
            }
        }

        /// <summary>
        ///     save PCCObject to file by reconstruction from data
        /// </summary>
        /// <param name="path">full path + file name.</param>
        public void saveByReconstructing(string path)
        {
            saveByReconstructing(path, false);
        }

        /// <summary>
        ///     save PCCObject to file by reconstruction from data
        /// </summary>
        /// <param name="path">full path + file name.</param>
        /// <param name="compress">true if you want a zlib compressed pcc file.</param>
        public void saveByReconstructing(string path, bool compress)
        {
            try
            {
                this.IsCompressed = false;
                MemoryStream m = new MemoryStream();
                m.WriteBytes(header);
                //name table
                NameOffset = (int)m.Position;
                NameCount = names.Count;
                foreach (string s in names)
                {
                    string text = s;
                    if (!text.EndsWith("\0"))
                    {
                        text += "\0";
                    }
                    m.Write(BitConverter.GetBytes(-text.Length), 0, 4);
                    foreach (char c in text)
                    {
                        m.WriteByte((byte)c);
                        m.WriteByte(0);
                    }
                }
                //import table
                ImportOffset = (int)m.Position;
                ImportCount = imports.Count;
                foreach (ME3ImportEntry e in imports)
                {
                    m.WriteBytes(e.header);
                }
                //export table
                ExportOffset = (int)m.Position;
                ExportCount = exports.Count;
                for (int i = 0; i < exports.Count; i++)
                {
                    ME3ExportEntry e = exports[i];
                    e.headerOffset = (uint)m.Position;
                    m.WriteBytes(e.header);
                }
                //freezone
                int FreeZoneSize = FreeZoneEnd - FreeZoneStart;
                FreeZoneStart = (int)m.Position;
                m.Write(new byte[FreeZoneSize], 0, FreeZoneSize);
                FreeZoneEnd = expDataBegOffset = (int)m.Position;
                //export data
                for (int i = 0; i < exports.Count; i++)
                {
                    ME3ExportEntry e = exports[i];
                    e.DataOffset = (int)m.Position;
                    e.DataSize = e.Data.Length;
                    m.WriteBytes(e.Data);
                    long pos = m.Position;
                    m.Seek(e.headerOffset + 32, SeekOrigin.Begin);
                    m.Write(BitConverter.GetBytes(e.DataSize), 0, 4);
                    m.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                    m.Seek(pos, SeekOrigin.Begin);
                }
                //update header
                m.Seek(0, SeekOrigin.Begin);
                m.WriteBytes(header);

                if (compress)
                {
                    CompressionHelper.CompressAndSave(m, path);
                }
                else
                {
                    File.WriteAllBytes(path, m.ToArray());
                }
                AfterSave();
            }
            catch (Exception ex)
            {
                MessageBox.Show("PCC Save error:\n" + ex.Message);
            }
        }

        /// <summary>
        /// This method is an alternate way of saving PCCs
        /// Instead of reconstructing the PCC from the data taken, it instead copies across the existing
        /// data, appends the name list and import list, appends changed and new exports, and then appends the export list.
        /// Changed exports with the same datasize or smaller are updaed in place.
        /// </summary>
        /// <param name="newFileName">The filename to write to</param>
        public void appendSave(string newFileName)
        {
            IEnumerable<ME3ExportEntry> replaceExports;
            IEnumerable<ME3ExportEntry> appendExports;

            int lastDataOffset;
            int max;
            if (IsAppend)
            {
                replaceExports = exports.Where(export => export.hasChanged && export.DataOffset < NameOffset && export.DataSize <= export.OriginalDataSize);
                appendExports = exports.Where(export => export.DataOffset > NameOffset || (export.hasChanged && export.DataSize > export.OriginalDataSize));
                max = exports.Where(exp => exp.DataOffset < NameOffset).Max(e => e.DataOffset);
            }
            else
            {
                IEnumerable<ME3ExportEntry> changedExports;
                changedExports = exports.Where(export => export.hasChanged);
                replaceExports = changedExports.Where(export => export.DataSize <= export.OriginalDataSize);
                appendExports = changedExports.Except(replaceExports);
                max = exports.Max(maxExport => maxExport.DataOffset);
            }

            ME3ExportEntry lastExport = exports.Find(export => export.DataOffset == max);
            lastDataOffset = lastExport.DataOffset + lastExport.DataSize;

            byte[] oldPCC = new byte[lastDataOffset];
            if (IsCompressed)
            {
                oldPCC = CompressionHelper.Decompress(FileName).Take(lastDataOffset).ToArray();
                IsCompressed = false;
            }
            else
            {
                using (FileStream oldPccStream = new FileStream(this.FileName, FileMode.Open))
                {
                    //Read the original data up to the last export
                    oldPccStream.Read(oldPCC, 0, lastDataOffset);
                }
            }
            //Start writing the new file
            using (FileStream newPCCStream = new FileStream(newFileName, FileMode.Create))
            {
                newPCCStream.Seek(0, SeekOrigin.Begin);
                //Write the original file up til the last original export (note that this leaves in all the original exports)
                newPCCStream.Write(oldPCC, 0, lastDataOffset);

                //write the in-place export updates
                foreach (ME3ExportEntry export in replaceExports)
                {
                    newPCCStream.Seek(export.DataOffset, SeekOrigin.Begin);
                    export.DataSize = export.Data.Length;
                    newPCCStream.WriteBytes(export.Data);
                }

                newPCCStream.Seek(lastDataOffset, SeekOrigin.Begin);
                //Set the new nameoffset and namecounts
                NameOffset = (int)newPCCStream.Position;
                NameCount = names.Count;
                //Write out the namelist
                foreach (string name in names)
                {
                    newPCCStream.WriteValueS32(-(name.Length + 1));
                    newPCCStream.WriteString(name + "\0", (uint)(name.Length + 1) * 2, Encoding.Unicode);
                }

                //Write the import list
                ImportOffset = (int)newPCCStream.Position;
                ImportCount = imports.Count;
                foreach (ME3ImportEntry import in imports)
                {
                    newPCCStream.WriteBytes(import.header);
                }

                //Append the new data
                foreach (ME3ExportEntry export in appendExports)
                {
                    export.DataOffset = (int)newPCCStream.Position;
                    export.DataSize = export.Data.Length;
                    newPCCStream.WriteBytes(export.Data);
                }

                //Write the export list
                ExportOffset = (int)newPCCStream.Position;
                ExportCount = exports.Count;
                foreach (ME3ExportEntry export in exports)
                {
                    newPCCStream.WriteBytes(export.header);
                }

                IsAppend = true;

                //write the updated header
                newPCCStream.Seek(0, SeekOrigin.Begin);
                newPCCStream.WriteBytes(header);
            }
            AfterSave();
        }

        private void AfterSave()
        {
            lastSaved = DateTime.Now;
            foreach (var export in exports)
            {
                export.hasChanged = false;
            }
            foreach (var import in imports)
            {
                import.hasChanged = false;
            }
            namesAdded = 0;
        }

        public string getObjectName(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1].ObjectName;
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1].ObjectName;
            return "";
        }

        public string getObjectClass(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1].ClassName;
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1].ClassName;
            return "";
        }

        public string getClassName(int index)
        {
            string s = "";
            if (index > 0)
            {
                s = names[exports[index - 1].idxObjectName];
            }
            if (index < 0)
            {
                s = names[imports[index * -1 - 1].idxObjectName];
            }
            if (index == 0)
            {
                s = "Class";
            }
            return s;
        }

        /// <summary>
        ///     gets Export or Import entry
        /// </summary>
        /// <param name="index">unreal index</param>
        public IEntry getEntry(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1];
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1];
            return null;
        }

        public bool isImport(int index)
        {
            return (index >= 0 && index < imports.Count);
        }
        public bool isExport(int index)
        {
            return (index >= 0 && index < exports.Count);
        }

        public void addImport(IImportEntry importEntry)
        {
            if (importEntry is ME3ImportEntry)
            {
                addImport(importEntry as ME3ImportEntry);
            }
            else
            {
                throw new FormatException("Cannot add import to an ME3 package that is not from ME3");
            }
        }

        public void addImport(ME3ImportEntry importEntry)
        {
            if (importEntry.FileRef != this)
                throw new Exception("you cannot add a new import entry from another pcc file, it has invalid references!");

            imports.Add(importEntry);
            ImportCount = imports.Count;
        }

        public void addExport(IExportEntry exportEntry)
        {
            if (exportEntry is ME3ExportEntry)
            {
                addExport(exportEntry as ME3ExportEntry);
            }
            else
            {
                throw new FormatException("Cannot add export to an ME3 package that is not from ME3");
            }
        }

        public void addExport(ME3ExportEntry exportEntry)
        {
            if (exportEntry.FileRef != this)
                throw new Exception("you cannot add a new export entry from another pcc file, it has invalid references!");

            exportEntry.hasChanged = true;

            exports.Add(exportEntry);
            ExportCount = exports.Count;
        }

        public IExportEntry getExport(int index)
        {
            return exports[index];
        }

        public IImportEntry getImport(int index)
        {
            return imports[index];
        }
        
        public void setNames(List<string> list)
        {
            names = list;
        }
    }
}