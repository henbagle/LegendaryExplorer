﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ME3Explorer.Packages
{
    public enum MEGame
    {
        ME1 = 1,
        ME2,
        ME3,
    }

    public enum ArrayType
    {
        Object,
        Name,
        Enum,
        Struct,
        Bool,
        String,
        Float,
        Int,
        Byte,
    }

    public class PropertyInfo
    {
        public Unreal.PropertyReader.Type type;
        public string reference;
    }

    public class ClassInfo
    {
        public Dictionary<string, PropertyInfo> properties;
        public string baseClass;
        //Relative to BIOGame
        public string pccPath;
        public int exportIndex;

        public ClassInfo()
        {
            properties = new Dictionary<string, PropertyInfo>();
        }
    }

    public interface IMEPackage
    {
        bool IsCompressed { get; }
        bool CanReconstruct { get; }
        bool IsModified { get; }
        int ExportCount { get; }
        int ImportCount { get; }
        int ImportOffset { get; }
        IReadOnlyList<IExportEntry> Exports { get; }
        IReadOnlyList<IImportEntry> Imports { get; }
        IReadOnlyList<string> Names { get; }
        MEGame Game { get; }
        string FileName { get; }
        DateTime LastSaved { get; }
        long FileSize { get; }

        //reading
        bool isExport(int index);
        bool isImport(int index);
        bool isName(int index);
        IEntry getEntry(int index);
        IExportEntry getExport(int index);
        IImportEntry getImport(int index);
        int findName(string nameToFind);
        string getClassName(int index);
        string getNameEntry(int index);
        string getObjectClass(int index);
        string getObjectName(int index);

        //editing
        void addName(string name);
        int FindNameOrAdd(string name);
        void addExport(IExportEntry exportEntry);
        void addImport(IImportEntry importEntry);
        /// <summary>
        ///     exposed so that the property import function can restore the namelist after a failure.
        ///     please don't use it anywhere else.
        /// </summary>
        void setNames(List<string> list);

        //saving
        void save();
        void save(string path);

        ObservableCollection<GenericWindow> Tools { get; }
        void RegisterTool(GenericWindow tool);
        void Release(System.Windows.Window wpfWindow = null, System.Windows.Forms.Form winForm = null);
        event EventHandler noLongerOpenInTools;
    }
}