﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{
    public static class EntryImporter
    {
        public enum PortingOption
        {
            CloneTreeAsChild,
            AddSingularAsChild,
            ReplaceSingular,
            MergeTreeChildren,
            Cancel,
            CloneAllDependencies
        }

        private static readonly byte[] me1Me2StackDummy =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] me3StackDummy =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] UDKStackDummy =
        {
            0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        };

        private static byte[] GetStackDummy(MEGame game) => game switch
        {
            MEGame.UDK => UDKStackDummy,
            MEGame.ME1 => me1Me2StackDummy,
            MEGame.ME2 => me1Me2StackDummy,
            _ => me3StackDummy
        };

        /// <summary>
        /// Imports <paramref name="sourceEntry"/> (and possibly its children) to <paramref name="destPcc"/> in a manner defined by <paramref name="portingOption"/>
        /// If no <paramref name="relinkMap"/> is provided, method will create one
        /// </summary>
        /// <param name="portingOption"></param>
        /// <param name="sourceEntry"></param>
        /// <param name="destPcc"></param>
        /// <param name="targetLinkEntry">Can be null if cloning as a top-level entry</param>
        /// <param name="shouldRelink"></param>
        /// <param name="newEntry"></param>
        /// <param name="relinkMap"></param>
        /// <param name="importExportDependencies">Import dependencies when relinking. Requires shouldRelink = true. If portingOption is CloneAllDependencies this value is ignored</param>
        /// <returns></returns>
        public static List<EntryStringPair> ImportAndRelinkEntries(PortingOption portingOption, IEntry sourceEntry, IMEPackage destPcc, IEntry targetLinkEntry, bool shouldRelink,
                                                                        out IEntry newEntry, Dictionary<IEntry, IEntry> relinkMap = null
                                                                        , Action<string> errorOccuredCallback = null, bool importExportDependencies = false)
        {
            relinkMap ??= new Dictionary<IEntry, IEntry>();
            IMEPackage sourcePcc = sourceEntry.FileRef;

            if (portingOption == PortingOption.ReplaceSingular)
            {
                //replace data only
                if (sourceEntry is ExportEntry entry)
                {
                    relinkMap.Add(entry, targetLinkEntry);
                    ReplaceExportDataWithAnother(entry, targetLinkEntry as ExportEntry, errorOccuredCallback);
                }
            }

            if (portingOption is PortingOption.MergeTreeChildren or PortingOption.ReplaceSingular)
            {
                newEntry = targetLinkEntry; //Root item is the one we just dropped. Use that as the root.
            }
            else
            {
                int link = targetLinkEntry?.UIndex ?? 0;
                if (sourceEntry is ExportEntry sourceExport)
                {
                    //importing an export (check if it exists first, if it does, just link to it)
                    newEntry = destPcc.FindExport(sourceEntry.InstancedFullPath) ?? ImportExport(destPcc, sourceExport, link, portingOption == PortingOption.CloneAllDependencies, relinkMap, errorOccuredCallback);
                }
                else
                {
                    newEntry = GetOrAddCrossImportOrPackage(sourceEntry.InstancedFullPath, sourcePcc, destPcc,
                                                            forcedLink: sourcePcc.Tree.NumChildrenOf(sourceEntry) == 0 ? link : (int?)null, objectMapping: relinkMap);
                }

            }


            //if this node has children
            if (portingOption is PortingOption.CloneTreeAsChild or PortingOption.MergeTreeChildren or PortingOption.CloneAllDependencies
             && sourcePcc.Tree.NumChildrenOf(sourceEntry) > 0)
            {
                importChildrenOf(sourceEntry, newEntry);
            }

            //for shader porting. For some reason the relinkMap gets cleared during relinking, so make the list here
            var sourceExports = relinkMap.Keys.OfType<ExportEntry>().ToList();

            List<EntryStringPair> relinkResults = null;
            if (shouldRelink)
            {
                relinkResults = Relinker.RelinkAll(relinkMap, importExportDependencies || portingOption == PortingOption.CloneAllDependencies);
            }

            //Port Shaders
            var portingCache = ShaderCacheManipulator.GetLocalShadersForMaterials(sourceExports);
            if (portingCache is not null)
            {
                if (destPcc.Game != sourcePcc.Game)
                {
                    errorOccuredCallback?.Invoke($"You cannot port Materials from {sourcePcc.Game} into {destPcc.Game}");
                }
                else
                {
                    ShaderCacheManipulator.AddShadersToFile(destPcc, portingCache);
                }
            }

            // Reindex - disabled for now as it causes issues
            //Dictionary<string, ExportEntry> itemsToReindex = new Dictionary<string, ExportEntry>();
            //foreach (var v in relinkMap.Values)
            //{
            //    if (v is ExportEntry export && export.indexValue > 0)
            //    {
            //        itemsToReindex[export.FullPath] = export; // Match on full path. Not instanced full path!
            //    }
            //}

            //foreach (var item in itemsToReindex)
            //{
            //    ReindexExportEntriesWithSamePath(item.Value);
            //}

            return relinkResults;

            void importChildrenOf(IEntry sourceNode, IEntry newParent)
            {
                foreach (IEntry node in sourceNode.GetChildren().ToList())
                {
                    if (portingOption == PortingOption.MergeTreeChildren)
                    {
                        //we must check to see if there is an item already matching what we are trying to port.

                        //Todo: We may need to enhance target checking here as fullpath may not be reliable enough. Maybe have to do indexing, or something.
                        IEntry sameObjInTarget = newParent.GetChildren().FirstOrDefault(x => node.InstancedFullPath == x.InstancedFullPath);
                        if (sameObjInTarget != null)
                        {
                            relinkMap[node] = sameObjInTarget;

                            //merge children to this node instead
                            importChildrenOf(node, sameObjInTarget);

                            continue;
                        }
                    }

                    IEntry entry;
                    if (node is ExportEntry exportNode)
                    {
                        entry = ImportExport(destPcc, exportNode, newParent.UIndex, portingOption == PortingOption.CloneAllDependencies, relinkMap, errorOccuredCallback);
                    }
                    else
                    {
                        entry = GetOrAddCrossImportOrPackage(node.InstancedFullPath, sourcePcc, destPcc, objectMapping: relinkMap, forcedLink: newParent.UIndex);
                    }


                    importChildrenOf(node, entry);
                }
            }
        }

        public static void ReindexExportEntriesWithSamePath(ExportEntry entry)
        {
            string prefixToReindex = entry.ParentInstancedFullPath;
            string objectname = entry.ObjectName.Name;

            int index = 1; //we'll start at 1.
            foreach (ExportEntry export in entry.FileRef.Exports)
            {
                //Check object name is the same, the package path count is the same, the package prefix is the same, and the item is not of type Class

                // Could this be optimized somehow?
                if (export.ParentInstancedFullPath == prefixToReindex && !export.IsClass && objectname == export.ObjectName.Name)
                {
                    export.indexValue = index;
                    index++;
                }
            }
        }

        /// <summary>
        /// Imports an export from another package file. Does not perform a relink, if you want to relink, use ImportAndRelinkEntries().
        /// </summary>
        /// <param name="destPackage">Package to import to</param>
        /// <param name="sourceExport">Export object from the other package to import</param>
        /// <param name="link">Local parent node UIndex</param>
        /// <param name="importExportDependencies">Whether to import exports that are referenced in header</param>
        /// <param name="objectMapping"></param>
        /// <returns></returns>
        public static ExportEntry ImportExport(IMEPackage destPackage, ExportEntry sourceExport, int link, bool importExportDependencies = false,
            IDictionary<IEntry, IEntry> objectMapping = null, Action<string> errorOccuredCallback = null)
        {
            byte[] prePropBinary;
            if (sourceExport.HasStack)
            {
                byte[] dummy = GetStackDummy(destPackage.Game);
                prePropBinary = new byte[8 + dummy.Length];
                sourceExport.DataReadOnly[..8].CopyTo(prePropBinary);
                dummy.CopyTo(prePropBinary, 8);
            }
            else
            {
                int start = sourceExport.GetPropertyStart();
                if (start == 16)
                {
                    var sourceSpan = sourceExport.DataReadOnly[..16];
                    int newNameIdx = destPackage.FindNameOrAdd(sourceExport.FileRef.GetNameEntry(MemoryMarshal.Read<int>(sourceSpan[4..])));
                    prePropBinary = sourceSpan.ToArray();
                    MemoryMarshal.Write(prePropBinary.AsSpan(4), ref newNameIdx);
                }
                else
                {
                    prePropBinary = sourceExport.DataReadOnly[..start].ToArray();
                }
            }

            PropertyCollection props = sourceExport.GetProperties();

            //store copy of names list in case something goes wrong
            if (sourceExport.Game != destPackage.Game)
            {
                List<string> names = destPackage.Names.ToList();
                try
                {
                    if (sourceExport.Game != destPackage.Game)
                    {
                        bool removedProperties = false;
                        props = EntryPruner.RemoveIncompatibleProperties(sourceExport.FileRef, props, sourceExport.ClassName, destPackage.Game, ref removedProperties);
                    }
                }
                catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    //restore namelist in event of failure.
                    destPackage.restoreNames(names);
                    errorOccuredCallback?.Invoke($"Error occurred while trying to import {sourceExport.ObjectName.Instanced} : {exception.Message}");
                    throw; //should we throw?
                }
            }


            //takes care of slight header differences between ME1/2 and ME3
            byte[] newHeader = sourceExport.GenerateHeader(destPackage, false); //The header needs relinked or it will be wrong if it has a component map!

            ////for supported classes, this will add any names in binary to the Name table, as well as take care of binary differences for cross-game importing
            ////for unsupported classes, this will just copy over the binary
            ////sometimes converting binary requires altering the properties as well
            ObjectBinary binaryData = ExportBinaryConverter.ConvertPostPropBinary(sourceExport, destPackage.Game, props);

            //Set class.
            IEntry classValue = null;
            switch (sourceExport.Class)
            {
                case ImportEntry sourceClassImport:
                    //The class of the export we are importing is an import. We should attempt to relink this.
                    classValue = GetOrAddCrossImportOrPackage(sourceClassImport.InstancedFullPath, sourceExport.FileRef, destPackage, objectMapping: objectMapping);
                    break;
                case ExportEntry sourceClassExport:
                    if (IsSafeToImportFrom(sourceExport.FileRef.FilePath, destPackage.Game))
                    {
                        classValue = GetOrAddCrossImportOrPackageFromGlobalFile(sourceClassExport.InstancedFullPath, sourceExport.FileRef, destPackage, objectMapping);
                        break;
                    }
                    classValue = destPackage.FindExport(sourceClassExport.InstancedFullPath);
                    if (classValue is null && importExportDependencies)
                    {
                        IEntry classParent = GetOrAddCrossImportOrPackage(sourceClassExport.ParentFullPath, sourceExport.FileRef, destPackage, true, objectMapping);
                        classValue = ImportExport(destPackage, sourceClassExport, classParent?.UIndex ?? 0, true, objectMapping);
                    }
                    break;
            }

            //Set superclass
            IEntry superclass = null;
            switch (sourceExport.SuperClass)
            {
                case ImportEntry sourceSuperClassImport:
                    //The class of the export we are importing is an import. We should attempt to relink this.
                    superclass = GetOrAddCrossImportOrPackage(sourceSuperClassImport.InstancedFullPath, sourceExport.FileRef, destPackage, objectMapping: objectMapping);
                    break;
                case ExportEntry sourceSuperClassExport:
                    if (IsSafeToImportFrom(sourceExport.FileRef.FilePath, destPackage.Game))
                    {
                        superclass = GetOrAddCrossImportOrPackageFromGlobalFile(sourceSuperClassExport.InstancedFullPath, sourceExport.FileRef, destPackage, objectMapping);
                        break;
                    }
                    superclass = destPackage.FindExport(sourceSuperClassExport.InstancedFullPath);
                    if (superclass is null && importExportDependencies)
                    {
                        IEntry superClassParent = GetOrAddCrossImportOrPackage(sourceSuperClassExport.ParentFullPath, sourceExport.FileRef, destPackage,
                            true, objectMapping);
                        superclass = ImportExport(destPackage, sourceSuperClassExport, superClassParent?.UIndex ?? 0, true, objectMapping);
                    }
                    break;
            }

            //Check archetype.
            IEntry archetype = null;
            switch (sourceExport.Archetype)
            {
                case ImportEntry sourceArchetypeImport:
                    archetype = GetOrAddCrossImportOrPackage(sourceArchetypeImport.InstancedFullPath, sourceExport.FileRef, destPackage, objectMapping: objectMapping);
                    break;
                case ExportEntry sourceArchetypeExport:
                    if (IsSafeToImportFrom(sourceExport.FileRef.FilePath, destPackage.Game))
                    {
                        archetype = GetOrAddCrossImportOrPackageFromGlobalFile(sourceArchetypeExport.InstancedFullPath, sourceExport.FileRef, destPackage, objectMapping);
                        break;
                    }
                    archetype = destPackage.FindExport(sourceArchetypeExport.InstancedFullPath);
                    if (archetype is null && importExportDependencies)
                    {
                        IEntry archetypeParent = GetOrAddCrossImportOrPackage(sourceArchetypeExport.ParentInstancedFullPath, sourceExport.FileRef, destPackage,
                                                                              true, objectMapping);
                        archetype = ImportExport(destPackage, sourceArchetypeExport, archetypeParent?.UIndex ?? 0, true, objectMapping);
                    }
                    break;
            }
            
            EndianBitConverter.WriteAsBytes(destPackage.FindNameOrAdd(sourceExport.ObjectName.Name), newHeader.AsSpan(ExportEntry.OFFSET_idxObjectName), destPackage.Endian);
            EndianBitConverter.WriteAsBytes(sourceExport.ObjectName.Number, newHeader.AsSpan(ExportEntry.OFFSET_indexValue), destPackage.Endian);
            EndianBitConverter.WriteAsBytes(link, newHeader.AsSpan(ExportEntry.OFFSET_idxLink), destPackage.Endian);

            var newExport = new ExportEntry(destPackage, newHeader, prePropBinary, props, binaryData, sourceExport.IsClass)
            {
                Class = classValue,
                SuperClass = superclass,
                Archetype = archetype,
            };
            destPackage.AddExport(newExport);
            if (objectMapping != null)
            {
                objectMapping[sourceExport] = newExport;
            }

            return newExport;
        }

        public static bool ReplaceExportDataWithAnother(ExportEntry incomingExport, ExportEntry targetExport, Action<string> errorOccuredCallback = null)
        {

            using var res = new EndianReader(MemoryManager.GetMemoryStream()) { Endian = targetExport.FileRef.Endian };
            if (incomingExport.HasStack)
            {
                res.Writer.Write(incomingExport.DataReadOnly.Slice(0, 8));
                res.Writer.WriteFromBuffer(GetStackDummy(targetExport.Game));
            }
            else
            {
                //int start = incomingExport.GetPropertyStart();
                res.Writer.WriteZeros(incomingExport.GetPropertyStart());
                //res.Writer.Write(new byte[start], 0, start);
            }

            //store copy of names list in case something goes wrong
            List<string> names = targetExport.FileRef.Names.ToList();
            try
            {
                PropertyCollection props = incomingExport.GetProperties();
                ObjectBinary binary = ExportBinaryConverter.ConvertPostPropBinary(incomingExport, targetExport.Game, props);
                props.WriteTo(res.Writer, targetExport.FileRef);
                res.Writer.WriteFromBuffer(binary.ToBytes(targetExport.FileRef));
            }
            catch (Exception exception)
            {
                //restore namelist in event of failure.
                targetExport.FileRef.restoreNames(names);
                errorOccuredCallback?.Invoke($"Error occurred while replacing data in {incomingExport.ObjectName.Instanced} : {exception.Message}");
                return false;
            }
            targetExport.Data = res.ToArray();
            return true;
        }

        /// <summary>
        /// Adds an import from the importingPCC to the destinationPCC with the specified INSTANCED fullname, or returns the existing one if it can be found. 
        /// This will add parent imports and packages as neccesary
        /// </summary>
        /// <param name="importFullNameInstanced">INSTANCED full path of an import from ImportingPCC</param>
        /// <param name="sourcePcc">PCC to import imports from</param>
        /// <param name="destinationPCC">PCC to add imports to</param>
        /// <param name="forcedLink">force this as parent</param>
        /// <param name="importNonPackageExportsToo"></param>
        /// <param name="objectMapping"></param>
        /// <returns></returns>
        public static IEntry GetOrAddCrossImportOrPackage(string importFullNameInstanced, IMEPackage sourcePcc, IMEPackage destinationPCC,
                                                          bool importNonPackageExportsToo = false, IDictionary<IEntry, IEntry> objectMapping = null, int? forcedLink = null)
        {
            if (string.IsNullOrEmpty(importFullNameInstanced))
            {
                return null;
            }

            var foundEntry = destinationPCC.FindEntry(importFullNameInstanced);
            if (foundEntry != null)
            {
                return foundEntry;
            }

            string[] importParts = importFullNameInstanced.Split('.');

            //if importing something into eg. SFXGame.pcc, this will ensure links to SFXGame imports will link up to the proper exports in SFXGame
            if (importParts.Length > 1 && importParts[0].CaseInsensitiveEquals(destinationPCC.FileNameNoExtension))
            {
                foundEntry = destinationPCC.FindEntry(string.Join('.', importParts[1..]));
                if (foundEntry != null)
                {
                    return foundEntry;
                }
            }

            if (forcedLink is int link)
            {
                ImportEntry importingImport = sourcePcc.FindImport(importFullNameInstanced); // this shouldn't be null
                var newImport = new ImportEntry(destinationPCC, link, importingImport.ObjectName)
                {
                    ClassName = importingImport.ClassName,
                    PackageFile = importingImport.PackageFile
                };
                destinationPCC.AddImport(newImport);
                if (objectMapping != null)
                {
                    objectMapping[importingImport] = newImport;
                }

                return newImport;
            }


            //recursively ensure parent exists. when importParts.Length == 1, this will return null
            IEntry parent = GetOrAddCrossImportOrPackage(string.Join('.', importParts[..^1]), sourcePcc, destinationPCC,
                                                         importNonPackageExportsToo, objectMapping);

            var sourceEntry = sourcePcc.FindEntry(importFullNameInstanced); // should this search entries instead? What if an import has an export parent?
            if (sourceEntry is ImportEntry imp) // import not found
            {
                // Code below forces Package objects to be imported as exports instead of imports. However if an object is an import (that works properly) the parent already has to exist upstream.
                // Some BioP for some reason use exports instead of imports when referencing sfxgame content even if they have no export children
                // not sure it has any functional difference
                // Mgamerz 3/21/2021

                //if (imp.ClassName == "Package")
                //{
                //    // Debug. Create package export instead.
                //    return ExportCreator.CreatePackageExport(destinationPCC, imp.ObjectName, parent, null);
                //}
                //else
                {
                    var newImport = new ImportEntry(destinationPCC, parent, imp.ObjectName)
                    {
                        ClassName = imp.ClassName,
                        PackageFile = imp.PackageFile
                    };
                    destinationPCC.AddImport(newImport);
                    if (objectMapping != null)
                    {
                        objectMapping[sourceEntry] = newImport;
                    }

                    return newImport;
                }
            }

            if (sourceEntry is ExportEntry foundMatchingExport)
            {

                if (importNonPackageExportsToo || foundMatchingExport.ClassName == "Package")
                {
                    return ImportExport(destinationPCC, foundMatchingExport, parent?.UIndex ?? 0, importNonPackageExportsToo, objectMapping);
                }
            }

            throw new Exception($"Unable to add {importFullNameInstanced} to file! Could not find it!");
        }

        /// <summary>
        /// Adds an import from the importingPCC to the destinationPCC with the specified importFullName, or returns the existing one if it can be found. 
        /// This will add parent imports and packages as neccesary
        /// </summary>
        /// <param name="importFullNameInstanced">GetFullPath() of an import from ImportingPCC</param>
        /// <param name="sourcePcc">PCC to import imports from</param>
        /// <param name="destinationPCC">PCC to add imports to</param>
        /// <param name="objectMapping"></param>
        /// <returns></returns>
        public static IEntry GetOrAddCrossImportOrPackageFromGlobalFile(string importFullNameInstanced, IMEPackage sourcePcc, IMEPackage destinationPCC, IDictionary<IEntry, IEntry> objectMapping = null,
            Action<EntryStringPair> doubleClickCallback = null)
        {
            string packageName = sourcePcc.FileNameNoExtension;
            if (string.IsNullOrEmpty(importFullNameInstanced))
            {
                return destinationPCC.getEntryOrAddImport(packageName, "Package");
            }

            string localSearchPath = $"{packageName}.{importFullNameInstanced}";

            // cache no longer necessary but left here until we're sure it's no longer necessary
            //see if this import exists locally
            //if (relinkerCache != null)
            //{
            //    if (relinkerCache.destFullPathToEntryMap.TryGetValue(importFullName, out var entry))
            //    {
            //        return entry;
            //    }
            //}
            //else
            //{
            //    foreach (ImportEntry imp in destinationPCC.Imports)
            //    {
            //        if (imp.FullPath == localSearchPath)
            //        {
            //            return imp;
            //        }
            //    }

            //see if this export exists locally in the package, under a class of same name (Engine class in Engine.pcc for example)
            var foundEntry = destinationPCC.FindEntry(localSearchPath);
            if (foundEntry != null)
            {
                return foundEntry;
            }

            // Try the name directly
            foundEntry = destinationPCC.FindEntry(importFullNameInstanced);
            if (foundEntry != null)
            {
                return foundEntry;
            }

            string[] importParts = importFullNameInstanced.Split('.');

            //recursively ensure parent exists
            IEntry parent = GetOrAddCrossImportOrPackageFromGlobalFile(string.Join(".", importParts.Take(importParts.Length - 1)), sourcePcc, destinationPCC, objectMapping, doubleClickCallback);

            ImportEntry matchingSourceImport = sourcePcc.FindImport(importFullNameInstanced);
            if (matchingSourceImport != null)
            {
                var newImport = new ImportEntry(destinationPCC, parent, matchingSourceImport.ObjectName)
                {
                    ClassName = matchingSourceImport.ClassName,
                    PackageFile = matchingSourceImport.PackageFile
                };
                destinationPCC.AddImport(newImport);
                if (objectMapping != null)
                {
                    objectMapping[matchingSourceImport] = newImport;
                }

                return newImport;
            }

            ExportEntry matchingSourceExport = sourcePcc.FindExport(importFullNameInstanced);
            if (matchingSourceExport != null)
            {
                var foundImp = destinationPCC.FindImport(importFullNameInstanced);
                if (foundImp != null) return foundImp;
                var newImport = new ImportEntry(destinationPCC, parent, matchingSourceExport.ObjectName)
                {
                    ClassName = matchingSourceExport.ClassName,
                    PackageFile = "Core" //This should be the file that the Class of this object is in, but I don't think it actually matters
                };
                destinationPCC.AddImport(newImport);
                if (objectMapping != null)
                {
                    objectMapping[matchingSourceExport] = newImport;
                }

                return newImport;
            }

            throw new Exception($"Unable to add {importFullNameInstanced} to file! Could not find it!");
        }

        //SirCxyrtyx: These are not exhaustive lists, just the ones that I'm sure about
        private static readonly string[] me1FilesSafeToImportFrom = { "Core.u", "Engine.u", "GameFramework.u", "PlotManagerMap.u", "BIOC_Base.u" };

        private static readonly string[] me2FilesSafeToImportFrom =
        {
            "Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "PlotManagerMap.pcc", "SFXGame.pcc", "Startup_INT.pcc"
        };

        private static readonly string[] me3FilesSafeToImportFrom =
        {
            //Class libary: These files contain ME3's standard library of classes, structs, enums... Also a few assets
            "Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc",
            //Assets: these files contain assets common enough that they are always loaded into memory
            "Startup.pcc", "GesturesConfig.pcc", "BIOG_Humanoid_MASTER_MTR_R.pcc", "BIOG_HMM_HED_PROMorph.pcc"
        };

        //TODO: make LE lists more exhaustive
        private static readonly string[] le1FilesSafeToImportFrom =
        {
            "Core.pcc", "Engine.pcc", "GFxUI.pcc", "PlotManagerMap.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc", "Startup_INT.pcc", "BIOC_Materials.pcc"
        };

        private static readonly string[] le2FilesSafeToImportFrom =
        {
            "Core.pcc", "Engine.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "PlotManagerMap.pcc", "SFXGame.pcc", "Startup_INT.pcc"
        };

        private static readonly string[] le3FilesSafeToImportFrom =
        {
            //Class libary: These files contain ME3's standard library of classes, structs, enums... Also a few assets
            // Note: You must use MELoadedFiles for Startup.pcc as it exists in METR Patch and is not used by game! (and is also wrong file)
            "Core.pcc", "Engine.pcc", "Startup.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc",
        };

        public static bool IsSafeToImportFrom(string path, MEGame game)
        {
            string fileName = Path.GetFileName(path);
            return FilesSafeToImportFrom(game).Any(f => fileName == f);
        }

        public static string[] FilesSafeToImportFrom(MEGame game) =>
            game switch
            {
                MEGame.ME1 => me1FilesSafeToImportFrom,
                MEGame.ME2 => me2FilesSafeToImportFrom,
                MEGame.ME3 => me3FilesSafeToImportFrom,
                MEGame.LE1 => le1FilesSafeToImportFrom,
                MEGame.LE2 => le2FilesSafeToImportFrom,
                MEGame.LE3 => le3FilesSafeToImportFrom,
                MEGame.UDK => Array.Empty<string>(),
                _ => throw new Exception($"Cannot lookup safe files for {game}")
            };


        public static bool CanImport(string className, MEGame game) => CanImport(GlobalUnrealObjectInfo.GetClassOrStructInfo(game, className), game);

        public static bool CanImport(ClassInfo classInfo, MEGame game) => classInfo != null && IsSafeToImportFrom(classInfo.pccPath, game);

        public static byte[] CreateStack(MEGame game, int stateNodeUIndex)
        {
            using var ms = MemoryManager.GetMemoryStream();
            ms.WriteInt32(stateNodeUIndex);
            ms.WriteInt32(stateNodeUIndex);
            ms.WriteFromBuffer(GetStackDummy(game));
            return ms.ToArray();
        }

        /// <summary>
        /// Attempts to resolve the import by looking at associated files that are loaded before this one. This method does not use a global file cache, the passed in cache may have items added to it.
        /// </summary>
        /// <param name="entry">The import to resolve</param>
        /// <param name="localCache">Package cache if you wish to keep packages held open, for example if you're resolving many imports</param>
        /// <param name="localization">Three letter localization code, all upper case. Defaults to INT.</param>
        /// <param name="clipRootLevelPackage">Add an additional attempt to resolve an import by not clipping the first part off of the package as a filename.</param>
        /// <returns></returns>
        public static ExportEntry ResolveImport(ImportEntry entry, PackageCache localCache = null, string localization = "INT", bool clipRootLevelPackage = true)
        {
            return ResolveImport(entry, null, localCache, localization, clipRootLevelPackage);
        }

        /// <summary>
        /// Attempts to resolve the import by looking at associated files that are loaded before this one, and by looking at globally loaded files.
        /// </summary>
        /// <param name="entry">The import to resolve</param>
        /// <param name="globalCache">Package cache that contains global files like SFXGame, Startup, etc. The cache will not be modified but can be used to reduce disk I/O.</param>
        /// <param name="lookupCache">Package cache if you wish to keep packages held open, for example if you're resolving many imports</param>
        /// <param name="localization">Three letter localization code, all upper case. Defaults to INT.</param>
        /// <returns></returns>
        public static ExportEntry ResolveImport(ImportEntry entry, PackageCache globalCache, PackageCache lookupCache, string localization = "INT", bool clipRootLevelPackage = true)
        {
            var entryFullPath = entry.InstancedFullPath;


            string containingDirectory = Path.GetDirectoryName(entry.FileRef.FilePath);
            var filesToCheck = new List<string>();
            CaseInsensitiveDictionary<string> gameFiles = MELoadedFiles.GetFilesLoadedInGame(entry.Game);

            string upkOrPcc = entry.Game == MEGame.ME1 ? ".upk" : ".pcc";
            // Check if there is package that has this name. This works for things like resolving SFXPawn_Banshee
            bool addPackageFile = gameFiles.TryGetValue(entry.ObjectName + upkOrPcc, out var efxPath) && !filesToCheck.Contains(efxPath);

            // Let's see if there is same-named top level package folder file. This will resolve class imports from SFXGame, Engine, etc.
            IEntry p = entry.Parent;
            if (p != null)
            {
                while (p.Parent != null)
                {
                    p = p.Parent;
                }

                if (p.ClassName == "Package")
                {
                    if (gameFiles.TryGetValue($"{p.ObjectName}{upkOrPcc}", out var efPath) && !filesToCheck.Contains(efxPath))
                    {
                        filesToCheck.Add(Path.GetFileName(efPath));
                    }
                    else if (entry.Game == MEGame.ME1)
                    {
                        if (gameFiles.TryGetValue(p.ObjectName + ".u", out var path) && !filesToCheck.Contains(efxPath))
                        {
                            filesToCheck.Add(Path.GetFileName(path));
                        }
                    }
                }
            }

            //add related files that will be loaded at the same time (eg. for BioD_Nor_310, check BioD_Nor_310_LOC_INT, BioD_Nor, and BioP_Nor)
            filesToCheck.AddRange(GetPossibleAssociatedFiles(entry.FileRef, localization));

            if (addPackageFile)
            {
                filesToCheck.Add(Path.GetFileName(efxPath));
            }

            //if (entry.Game == MEGame.ME3)
            //{
            //    // Look in BIOP_MP_Common. This is not a 'safe' file but it is always loaded in MP mode and will be commonly referenced by MP files
            //    if (gameFiles.TryGetValue("BIOP_MP_COMMON.pcc", out var efPath))
            //    {
            //        filesToCheck.Add(Path.GetFileName(efPath));
            //    }
            //}


            //add base definition files that are always loaded (Core, Engine, etc.)
            foreach (var fileName in FilesSafeToImportFrom(entry.Game))
            {
                if (gameFiles.TryGetValue(fileName, out var efPath))
                {
                    filesToCheck.Add(Path.GetFileName(efPath));
                }
            }

            //add startup files (always loaded)
            IEnumerable<string> startups;
            if (entry.Game.IsGame2() || entry.Game is MEGame.LE1)
            {
                startups = gameFiles.Keys.Where(x => x.Contains("Startup_", StringComparison.InvariantCultureIgnoreCase) && x.Contains($"_{localization}", StringComparison.InvariantCultureIgnoreCase)); //me2 this will unfortunately include the main startup file
            }
            else
            {
                startups = gameFiles.Keys.Where(x => x.Contains("Startup_", StringComparison.InvariantCultureIgnoreCase)); //me2 this will unfortunately include the main startup file
            }

            foreach (var fileName in filesToCheck.Concat(startups.Select(x => Path.GetFileName(gameFiles[x]))))
            {
                if (gameFiles.TryGetValue(fileName, out var fullgamepath) && File.Exists(fullgamepath))
                {
                    var export = containsImportedExport(fullgamepath, !clipRootLevelPackage);
                    if (export != null)
                    {
                        return export;
                    }
                }

                //Try local.
                var localPath = Path.Combine(containingDirectory, fileName);
                if (!localPath.Equals(fullgamepath, StringComparison.InvariantCultureIgnoreCase) && File.Exists(localPath))
                {
                    var export = containsImportedExport(localPath, !clipRootLevelPackage);
                    if (export != null)
                    {
                        return export;
                    }
                }
            }
            return null;

            //Perform check and lookup
            ExportEntry containsImportedExport(string packagePath, bool tryWithoutClipping = false)
            {
                //Debug.WriteLine($"Checking file {packagePath} for {entryFullPath}");
                IMEPackage package = null;
                if (globalCache != null)
                {
                    package = globalCache.GetCachedPackage(packagePath, false);
                }

                package ??= lookupCache != null ? lookupCache.GetCachedPackage(packagePath) : MEPackageHandler.OpenMEPackage(packagePath, forceLoadFromDisk: true);

                var packName = Path.GetFileNameWithoutExtension(packagePath);
                var packageParts = entryFullPath.Split('.').ToList();

                // Coded a bit weird for optimization on allocations
                string entryClippedPath = null;
                if (packageParts.Count > 1 && packName == packageParts[0])
                {
                    packageParts.RemoveAt(0);
                    entryClippedPath = string.Join(".", packageParts);
                }
                else if (packName == packageParts[0])
                {
                    //it's literally the file itself (an imported package like SFXGame)
                    return package.Exports.FirstOrDefault(x => x.idxLink == 0); //this will be at top of the tree
                }

                if (tryWithoutClipping && entryClippedPath != null)
                {
                    return package.FindExport(entryClippedPath) ?? package.FindExport(entryFullPath);
                }
                return package.FindExport(entryClippedPath ?? entryFullPath);
            }
        }
        public static List<string> GetPossibleAssociatedFiles(IMEPackage package, string localization = "INT", bool includeNonBioPRelated = true)
        {
            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(package.FilePath).ToLower();
            var associatedFiles = new List<string>();
            string bioFileExt = package.Game == MEGame.ME1 ? ".sfm" : ".pcc";
            if (includeNonBioPRelated)
            {
                associatedFiles.Add($"{filenameWithoutExtension}_LOC_{localization}{bioFileExt}"); //todo: support users setting preferred language of game files
            }
            var isBioXfile = filenameWithoutExtension.Length > 5 && filenameWithoutExtension.StartsWith("bio") && filenameWithoutExtension[4] == '_';
            if (isBioXfile)
            {
                // Do not include extensions in the results of this, they will be appended in resulting file
                string bioXNextFileLookup(string filenameWithoutExtensionX)
                {
                    //Lookup parents
                    var bioType = filenameWithoutExtensionX[3];
                    string[] parts = filenameWithoutExtensionX.Split('_');
                    if (parts.Length >= 2) //BioA_Nor_WowThatsAlot310
                    {
                        var levelName = parts[1];
                        switch (bioType)
                        {
                            case 'a' when parts.Length > 2:
                                return $"bioa_{levelName}";
                            case 'd' when parts.Length > 2:
                                return $"biod_{levelName}";
                            case 's' when parts.Length > 2:
                                return $"bios_{levelName}"; //BioS has no subfiles as far as I know but we'll just put this here anyways.
                            case 'a' when parts.Length == 2:
                            case 'd' when parts.Length == 2:
                            case 's' when parts.Length == 2:
                                return $"biop_{levelName}";
                        }
                    }

                    return null;
                }

                string nextfile = bioXNextFileLookup(filenameWithoutExtension);
                while (nextfile != null)
                {
                    if (includeNonBioPRelated)
                    {
                        associatedFiles.Add($"{nextfile}{bioFileExt}");
                        associatedFiles.Add($"{nextfile}_LOC_{localization}{bioFileExt}"); //todo: support users setting preferred language of game files
                    }
                    else if (nextfile.Length > 3 && nextfile[3] == 'p')
                    {
                        associatedFiles.Add($"{nextfile}{bioFileExt}");
                    }
                    nextfile = bioXNextFileLookup(nextfile.ToLower());
                }
            }

            if (package.Game == MEGame.ME3 && filenameWithoutExtension.Contains("MP", StringComparison.OrdinalIgnoreCase) && !filenameWithoutExtension.CaseInsensitiveEquals("BIOP_MP_COMMON"))
            {
                associatedFiles.Add("BIOP_MP_COMMON.pcc");
            }

            return associatedFiles;
        }

        public static IEntry EnsureClassIsInFile(IMEPackage pcc, string className, string gamePathOverride = null, Action<List<EntryStringPair>> RelinkResultsAvailable = null)
        {
            //check to see class is already in file
            foreach (ImportEntry import in pcc.Imports)
            {
                if (import.IsClass && import.ObjectName == className)
                {
                    return import;
                }
            }
            foreach (ExportEntry export in pcc.Exports)
            {
                if (export.IsClass && export.ObjectName == className)
                {
                    return export;
                }
            }

            ClassInfo info = GlobalUnrealObjectInfo.GetClassOrStructInfo(pcc.Game, className);

            //backup some package state so we can undo changes if something goes wrong
            int exportCount = pcc.ExportCount;
            int importCount = pcc.ImportCount;
            List<string> nameListBackup = pcc.Names.ToList();
            try
            {
                Stream loadStream = null;
                if (pcc.Game is MEGame.ME3 && info.pccPath.StartsWith("DLC_TestPatch"))
                {
                    string fileName = Path.GetFileName(info.pccPath);
                    string testPatchSfarPath = ME3Directory.TestPatchSFARPath;
                    if (testPatchSfarPath is null)
                    {
                        return null;
                    }
                    var patchSFAR = new DLCPackage(testPatchSfarPath);
                    int fileIdx = patchSFAR.FindFileEntry(fileName);
                    if (fileIdx == -1)
                    {
                        return null;
                    }

                    MemoryStream sfarEntry = patchSFAR.DecompressEntry(fileIdx);
                    using IMEPackage patchPcc = MEPackageHandler.OpenMEPackageFromStream(sfarEntry.SeekBegin());
                    if (patchPcc.TryGetUExport(info.exportIndex, out ExportEntry export) && export.IsClass && export.ObjectName == className)
                    {
                        string packageName = export.ParentName;
                        if (IsSafeToImportFrom($"{packageName}.pcc", MEGame.ME3))
                        {
                            return pcc.getEntryOrAddImport($"{packageName}.{className}");
                        }
                        else
                        {
                            loadStream = sfarEntry.SeekBegin();
                        }
                    }
                }

                if (loadStream is null)
                {
                    if (IsSafeToImportFrom(info.pccPath, pcc.Game))
                    {
                        string package = Path.GetFileNameWithoutExtension(info.pccPath);
                        return pcc.getEntryOrAddImport($"{package}.{className}");
                    }

                    //It's a class that's defined locally in every file that uses it.
                    if (info.pccPath == GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName)
                    {
                        loadStream = LegendaryExplorerCoreUtilities.GetCustomAppResourceStream(pcc.Game);
                        //string resourceFilePath = App.CustomResourceFilePath(pcc.Game);
                        //if (File.Exists(resourceFilePath))
                        //{
                        //    sourceFilePath = resourceFilePath;
                        //}
                    }
                    else
                    {
                        string testPath = Path.Combine(MEDirectories.GetBioGamePath(pcc.Game, gamePathOverride), info.pccPath);
                        if (File.Exists(testPath))
                        {
                            loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(testPath);
                        }
                        else if (pcc.Game == MEGame.ME1)
                        {
                            testPath = Path.Combine(gamePathOverride ?? ME1Directory.DefaultGamePath, info.pccPath);
                            if (File.Exists(testPath))
                            {
                                loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(testPath);
                            }
                        }
                    }

                    if (loadStream is null)
                    {
                        //can't find file to import from. This may occur if user does not have game or neccesary dlc installed 
                        return null;
                    }
                }

                using IMEPackage sourcePackage = MEPackageHandler.OpenMEPackageFromStream(loadStream);

                if (!sourcePackage.IsUExport(info.exportIndex))
                {
                    return null; //not sure how this would happen
                }

                ExportEntry sourceClassExport = sourcePackage.GetUExport(info.exportIndex);

                if (sourceClassExport.ObjectName != className)
                {
                    return null;
                }

                //Will make sure that, if the class is in a package, that package will exist in pcc
                IEntry parent = EntryImporter.GetOrAddCrossImportOrPackage(sourceClassExport.ParentFullPath, sourcePackage, pcc);

                var relinkResults = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceClassExport, pcc, parent, true, out IEntry result);
                if (relinkResults?.Count > 0)
                {
                    RelinkResultsAvailable?.Invoke(relinkResults);
                }
                return result;
            }
            catch (Exception)
            {
                //remove added entries
                var entriesToRemove = new List<IEntry>();
                for (int i = exportCount; i < pcc.Exports.Count; i++)
                {
                    entriesToRemove.Add(pcc.Exports[i]);
                }
                for (int i = importCount; i < pcc.Imports.Count; i++)
                {
                    entriesToRemove.Add(pcc.Imports[i]);
                }
                EntryPruner.TrashEntries(pcc, entriesToRemove);
                pcc.restoreNames(nameListBackup);
                return null;
            }
        }

        /// <summary>
        /// Gets a list of things the specified export references
        /// </summary>
        /// <param name="export">The export to check</param>
        /// <param name="includeLink">If the link should be included, which can sometimes pull in way more stuff than you want</param>
        /// <returns></returns>
        public static List<IEntry> GetAllReferencesOfExport(ExportEntry export, bool includeLink = false)
        {
            List<IEntry> referencedItems = new List<IEntry>();
            RecursiveGetDependencies(export, referencedItems, includeLink);
            return referencedItems.Distinct().ToList();
        }

        private static void AddEntryReference(int referenceIdx, IMEPackage package, List<IEntry> referencedItems)
        {
            if (package.TryGetEntry(referenceIdx, out var reference) && !referencedItems.Contains(reference))
            {
                referencedItems.Add(reference);
            }
        }

        private static void RecursiveGetDependencies(ExportEntry relinkingExport, List<IEntry> referencedItems, bool includeLink)
        {
            List<ExportEntry> localExportReferences = new List<ExportEntry>();

            // Compiles list of items local to this entry
            void AddReferenceLocal(int entryUIndex)
            {
                if (relinkingExport.FileRef.TryGetUExport(entryUIndex, out var exp) && !referencedItems.Any(x => x.UIndex == entryUIndex))
                {
                    // Add objects that we have not referenced yet.
                    localExportReferences.Add(exp);
                }
                // Global add
                AddEntryReference(entryUIndex, relinkingExport.FileRef, referencedItems);
            }

            if (includeLink && relinkingExport.Parent != null)
            {
                AddReferenceLocal(relinkingExport.Parent.UIndex);
            }

            // Pre-props binary
            byte[] prePropBinary = relinkingExport.GetPrePropBinary();

            //Relink stack
            if (relinkingExport.HasStack)
            {
                int uIndex = BitConverter.ToInt32(prePropBinary, 0);
                AddReferenceLocal(uIndex);

                uIndex = BitConverter.ToInt32(prePropBinary, 4);
                AddReferenceLocal(uIndex);
            }
            //Relink Component's TemplateOwnerClass
            else if (relinkingExport.TemplateOwnerClassIdx is var toci && toci >= 0)
            {

                int uIndex = BitConverter.ToInt32(prePropBinary, toci);
                AddReferenceLocal(uIndex);
            }

            // Metadata
            if (relinkingExport.SuperClass != null)
                AddReferenceLocal(relinkingExport.idxSuperClass);
            if (relinkingExport.Archetype != null)
                AddReferenceLocal(relinkingExport.idxArchetype);
            if (relinkingExport.Class != null)
                AddReferenceLocal(relinkingExport.idxClass);

            // Properties
            var props = relinkingExport.GetProperties();
            foreach (var prop in props)
            {
                RecursiveGetPropDependencies(prop, AddReferenceLocal);
            }

            // Binary
            var bin = ObjectBinary.From(relinkingExport);
            if (bin != null)
            {
                var binUIndexes = bin.GetUIndexes(relinkingExport.Game);
                foreach (var binUIndex in binUIndexes)
                {
                    AddReferenceLocal(binUIndex.Item1);
                }
            }

            // We have now collected all local references
            // We should reach out and see if we need to index others.
            foreach (var v in localExportReferences)
            {
                RecursiveGetDependencies(v, referencedItems, true);
            }
        }

        private static void RecursiveGetPropDependencies(Property prop, Action<int> addReference)
        {
            if (prop is ObjectProperty op)
            {
                addReference(op.Value);
            }
            else if (prop is StructProperty sp)
            {
                foreach (var p in sp.Properties)
                {
                    RecursiveGetPropDependencies(p, addReference);
                }
            }
            else if (prop is ArrayProperty<StructProperty> asp)
            {
                foreach (var p in asp.Properties)
                {
                    RecursiveGetPropDependencies(p, addReference);
                }
            }
            else if (prop is ArrayProperty<ObjectProperty> aop)
            {
                foreach (var p in aop)
                {
                    addReference(p.Value);
                }
            }
            else if (prop is DelegateProperty dp)
            {
                addReference(dp.Value.Object);
            }
        }
    }
}