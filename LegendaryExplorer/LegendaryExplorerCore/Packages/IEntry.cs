﻿using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Packages
{
    public interface IEntry
    {
        bool EntryHasPendingChanges { get; set; } //used to signal that this entry has uncommited changes
        bool HeaderChanged { get; set; }
        int indexValue { get; set; }
        int UIndex { get; }
        /// <summary>
        /// Get generates the header, Set deserializes all the header values from the provided byte array
        /// </summary>
        byte[] Header { get; set; }
        byte[] GenerateHeader();
        void SetHeaderValuesFromByteArray(byte[] value);
        IMEPackage FileRef { get; }
        MEGame Game { get; }
        int idxLink { get; set; }
        string ClassName { get; }
        string FullPath { get; }
        string InstancedFullPath { get; }
        string ObjectNameString { get; set; }
        NameReference ObjectName { get; set; }
        string ParentFullPath { get; }
        string ParentInstancedFullPath { get; }
        string ParentName { get; }
        bool HasParent { get; }
        IEntry Parent { get; set; }
        IEntry Clone(bool incrementIndex);
    }
}