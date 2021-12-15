﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorerCore.Packages
{
    public static class MEPackageHandler
    {
        /// <summary>
        /// Global override for shared cache. Set to false to disable usage of the cache and always force loading packages.
        /// </summary>
        public static bool GlobalSharedCacheEnabled = true;

        static readonly ConcurrentDictionary<string, IMEPackage> openPackages = new(StringComparer.OrdinalIgnoreCase);
        public static readonly ObservableCollection<IMEPackage> packagesInTools = new();

        // Package loading for UDK 2014/2015
        static Func<string, UDKPackage> UDKConstructorDelegate;
        static Func<Stream, string, UDKPackage> UDKStreamConstructorDelegate;

        // Package loading for ME games
        static Func<string, MEGame, MEPackage> MEBlankPackageCreatorDelegate;
        static Func<Stream, string, bool, Func<ExportEntry, bool>, MEPackage> MEStreamConstructorDelegate;

        public static void Initialize()
        {
            UDKConstructorDelegate = UDKPackage.RegisterBlankPackageCreator();
            UDKStreamConstructorDelegate = UDKPackage.RegisterStreamLoader();
            MEBlankPackageCreatorDelegate = MEPackage.RegisterBlankPackageCreator();
            MEStreamConstructorDelegate = MEPackage.RegisterStreamLoader();
        }

        public static IReadOnlyList<string> GetOpenPackages() => openPackages.Select(x => x.Key).ToList();

        /// <summary>
        /// Opens a package from a stream. Ensure the position is correctly set to the start of the package.
        /// </summary>
        /// <param name="inStream"></param>
        /// <param name="associatedFilePath"></param>
        /// <param name="useSharedPackageCache"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public static IMEPackage OpenMEPackageFromStream(Stream inStream, string associatedFilePath = null, bool useSharedPackageCache = false, IPackageUser user = null, bool quickLoad = false)
        {
            IMEPackage package;
            if (associatedFilePath == null || !useSharedPackageCache || !GlobalSharedCacheEnabled || quickLoad)
            {
                package = LoadPackage(inStream, associatedFilePath, false, quickLoad);
            }
            else
            {
                package = openPackages.GetOrAdd(associatedFilePath, fpath =>
                {
                    Debug.WriteLine($"Adding package to package cache (Stream): {associatedFilePath}");
                    return LoadPackage(inStream, associatedFilePath, true);
                });
            }

            if (user != null)
            {
                package.RegisterTool(user);
                addToPackagesInTools(package);
            }
            else
            {
                package.RegisterUse();
            }
            return package;
        }


        /// <summary>
        /// You should only use this if you know what you're doing! This will forcibly add a package to the open packages cache. Only used when package cache is enabled.
        /// </summary>
        public static void ForcePackageIntoCache(IMEPackage package)
        {
            if (GlobalSharedCacheEnabled)
            {
                Debug.WriteLine($@"Forcing package into cache: {package.FilePath}");
                if (package is UnrealPackageFile upf && upf.RefCount < 1)
                {
                    // Package will immediately be dropped on first dispose
                    Debugger.Break();
                }
                var pathToFile = package.FilePath;
                if (File.Exists(pathToFile))
                {
                    pathToFile = Path.GetFullPath(pathToFile); //STANDARDIZE INPUT IF FILE EXISTS (it might be a memory file!)
                }
                openPackages[pathToFile] = package;
            }
            else
            {
                Debug.WriteLine("Global Package Cache is disabled, cannot force packages into cache");
            }
        }


        /// <summary>
        /// Opens an already open package, registering it for use in a tool.
        /// </summary>
        /// <param name="package"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public static IMEPackage OpenMEPackage(IMEPackage package, IPackageUser user = null)
        {
            if (user != null)
            {
                package.RegisterTool(user);
                addToPackagesInTools(package);
            }
            else
            {
                package.RegisterUse();
            }
            return package;
        }

        /// <summary>
        /// Opens a Mass Effect package file. By default, this call will attempt to return an existing open (non-disposed) package at the same path if it is opened twice. Use the forceLoadFromDisk parameter to ignore this behavior.
        /// </summary>
        /// <param name="pathToFile">Path to the file to open</param>
        /// <param name="user">????</param>
        /// <param name="forceLoadFromDisk">If the package being opened should skip the shared package cache and forcibly load from disk. </param>
        /// <param name="quickLoad">Only load the header. Meant for when you just need to get info about a package without caring about the contents.</param>
        /// <param name="diskIOSyncLock"></param>
        /// <returns></returns>
        public static IMEPackage OpenMEPackage(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false, bool quickLoad = false, object diskIOSyncLock = null)
        {
            //Debug.WriteLine($"Opening package {pathToFile}");
            if (File.Exists(pathToFile))
            {
                pathToFile = Path.GetFullPath(pathToFile); //STANDARDIZE INPUT IF FILE EXISTS (it might be a memory file!)
            }

            IMEPackage package;
            if (forceLoadFromDisk || !GlobalSharedCacheEnabled || quickLoad) //Quick loaded packages cannot be cached
            {
                if (quickLoad)
                {
                    // Quickload: Don't read entire file.
                    if (diskIOSyncLock != null)
                    {
                        lock (diskIOSyncLock)
                        {
                            using var fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read);
                            package = LoadPackage(fs, pathToFile, false, true);
                        }
                    }
                    else
                    {
                        using var fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read);
                        package = LoadPackage(fs, pathToFile, false, true);
                    }

                }
                else
                {
                    // Reading and operating on memory is faster than seeking on disk
                    if (diskIOSyncLock != null)
                    {
                        MemoryStream ms;
                        lock (diskIOSyncLock)
                        {
                            ms = ReadAllFileBytesIntoMemoryStream(pathToFile);
                        }
                        var p = LoadPackage(ms, pathToFile, true);
                        ms.Dispose();
                        return p;
                    }
                    else
                    {
                        return LoadPackage(ReadAllFileBytesIntoMemoryStream(pathToFile), pathToFile, true);
                    }

                }

            }
            else
            {
                package = openPackages.GetOrAdd(pathToFile, fpath =>
                {
                    // Reading and operating on memory is faster than seeking on disk
                    if (diskIOSyncLock != null)
                    {
                        MemoryStream ms;
                        lock (diskIOSyncLock)
                        {
                            ms = ReadAllFileBytesIntoMemoryStream(fpath);
                        }
                        var p = LoadPackage(ms, fpath, true);
                        ms.Dispose();
                        return p;
                    }
                    else
                    {
                        using var fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read);
                        return LoadPackage(fs, fpath, true);
                    }
                });
            }



            if (user != null)
            {
                package.RegisterTool(user);
                addToPackagesInTools(package);
            }
            else
            {
                package.RegisterUse();
            }
            return package;
        }

        /// <summary>
        /// Partially opens an ME package file. Name, Export, and Import tables will be fully read, but export data will only be loaded for <see cref="ExportEntry"/>s that match <paramref name="exportPredicate"/>.
        /// Attempting to access the Data for any other <see cref="ExportEntry"/> will cause a <see cref="NullReferenceException"/>. Use with caution in performance critical situations only!
        /// The file is loaded from disk, and does not participate in package sharing.
        /// </summary>
        /// <param name="pathToFile"></param>
        /// <param name="exportPredicate"></param>
        /// <returns></returns>
        public static IMEPackage UnsafePartialLoad(string pathToFile, Func<ExportEntry, bool> exportPredicate)
        {
            Debug.WriteLine($"Partially loading package {pathToFile}");
            using var fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read);
            return LoadPackage(fs, pathToFile, false, false, exportPredicate);
        }

        /// <summary>
        /// Essentially just <code>new MemoryStream(File.ReadAllBytes(<paramref name="filePath"/>))</code>, but with some setup that improves decompression performance 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static MemoryStream ReadAllFileBytesIntoMemoryStream(string filePath)
        {
            byte[] buffer = File.ReadAllBytes(filePath);
            //lengthy constructor is neccesary so that TryGetBuffer can be used in decompression code
            return new MemoryStream(buffer, 0, buffer.Length, true, true);
        }

        /// <summary>
        /// Opens a package, but only reads the header. No names, imports or exports are loaded (and an error will be thrown if any are accessed). The package is not decompressed and is not added to the package cache.
        /// </summary>
        /// <param name="targetPath"></param>
        /// <returns></returns>
        public static IMEPackage QuickOpenMEPackage(string pathToFile)
        {
            if (File.Exists(pathToFile))
            {
                pathToFile = Path.GetFullPath(pathToFile); //STANDARDIZE INPUT IF FILE EXISTS (it might be a memory file!)
            }

            using var fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read);
            return LoadPackage(fs, pathToFile, false, true);
        }

        private static IMEPackage LoadPackage(Stream stream, string filePath = null, bool useSharedCache = false, bool quickLoad = false, Func<ExportEntry, bool> dataLoadPredicate = null)
        {
#if DEBUG && !AZURE
            // This is only for net5-packagecache branch to trace package opening
            //Debug.WriteLine($"Loading package {filePath}");
            //if (filePath != null && filePath.EndsWith("Core.pcc"))
            //    Debug.WriteLine("hi");
#endif
            ushort version = 0;
            ushort licenseVersion = 0;
            bool fullyCompressed = false;

            var er = new EndianReader(stream);
            if (stream.ReadUInt32() == UnrealPackageFile.packageTagBigEndian) er.Endian = Endian.Big;

            // This is stored as integer by cooker as it is flipped by size word in big endian
            uint versionLicenseePacked = er.ReadUInt32();
            if (versionLicenseePacked is 0x00020000 or 0x00010000 && er.Endian == Endian.Little && filePath != null) //can only load fully compressed packages from disk since we won't know what the .us file has
            {
                //block size - this is a fully compressed file. we must decompress it
                //for some reason fully compressed files use a little endian package tag
                var usfile = filePath + ".us";
                if (File.Exists(usfile))
                {
                    fullyCompressed = true;
                }
                else if (File.Exists(filePath + ".UNCOMPRESSED_SIZE"))
                {
                    fullyCompressed = true;
                }
            }

            if (!fullyCompressed)
            {
                version = (ushort)(versionLicenseePacked & 0xFFFF);
                licenseVersion = (ushort)(versionLicenseePacked >> 16);
            }


            IMEPackage pkg;
            if (fullyCompressed ||
                version == MEPackage.ME3UnrealVersion && licenseVersion is MEPackage.ME3LicenseeVersion or MEPackage.ME3Xenon2011DemoLicenseeVersion ||
                version == MEPackage.ME3WiiUUnrealVersion && licenseVersion == MEPackage.ME3LicenseeVersion ||
                version == MEPackage.ME2UnrealVersion && licenseVersion == MEPackage.ME2LicenseeVersion || //PC and Xbox share this
                version == MEPackage.ME2PS3UnrealVersion && licenseVersion == MEPackage.ME2PS3LicenseeVersion ||
                version == MEPackage.ME2DemoUnrealVersion && licenseVersion == MEPackage.ME2LicenseeVersion ||
                version == MEPackage.ME1UnrealVersion && licenseVersion == MEPackage.ME1LicenseeVersion ||
                version == MEPackage.ME1PS3UnrealVersion && licenseVersion == MEPackage.ME1PS3LicenseeVersion ||
                version == MEPackage.ME1XboxUnrealVersion && licenseVersion == MEPackage.ME1XboxLicenseeVersion ||

                // LEGENDARY
                version == MEPackage.LE1UnrealVersion && licenseVersion == MEPackage.LE1LicenseeVersion ||
                version == MEPackage.LE2UnrealVersion && licenseVersion == MEPackage.LE2LicenseeVersion ||
                version == MEPackage.LE3UnrealVersion && licenseVersion == MEPackage.LE3LicenseeVersion



                )
            {
                stream.Position -= 8; //reset to start
                pkg = MEStreamConstructorDelegate(stream, filePath, quickLoad, dataLoadPredicate);
                MemoryAnalyzer.AddTrackedMemoryItem($"MEPackage {Path.GetFileName(filePath)}", new WeakReference(pkg));
            }
            else if (version == UDKPackage.UDKUnrealVersion || version == 867 && licenseVersion == 0)
            {
                //UDK
                stream.Position -= 8; //reset to start
                pkg = UDKStreamConstructorDelegate(stream, filePath);
                MemoryAnalyzer.AddTrackedMemoryItem($"UDKPackage {Path.GetFileName(filePath)}", new WeakReference(pkg));
            }
            else
            {
                throw new FormatException("Not an ME1, ME2, ME3, LE1, LE2, LE3,or UDK (2015) package file.");
            }

            if (useSharedCache)
            {
                pkg.noLongerUsed += Package_noLongerUsed;
            }

            return pkg;
        }

        /// <summary>
        /// Creates and saves a package. A package is not returned as the saving code will add data that must be re-read for a package to be properly used.
        /// </summary>
        /// <param name="path">Where to save the package</param>
        /// <param name="game">What game the package is for</param>
        public static void CreateAndSavePackage(string path, MEGame game)
        {
            switch (game)
            {
                case MEGame.UDK:
                    UDKConstructorDelegate(path).Save();
                    break;
                case MEGame.LELauncher:
                    throw new ArgumentException("Cannot create a package for LELauncher, it doesn't use packages");
                case MEGame.Unknown:
                    throw new ArgumentException("Cannot create a package file for an Unknown game!", nameof(game));
                default:
                    MEBlankPackageCreatorDelegate(path, game).Save();
                    break;
            }
        }

        /// <summary>
        /// Generates a new empty level package file.
        /// </summary>
        /// <param name="outpath">Where to save the package</param>
        /// <param name="game">What game the package is for</param>
        public static void CreateEmptyLevel(string outpath, MEGame game)
        {
            if (!game.IsOTGame() && !game.IsLEGame())
                throw new Exception(@"Cannot create a level for a game that is not ME1/2/3 or LE1/2/3");

            var emptyLevelName = $"{game}EmptyLevel";
            var packageStream = LegendaryExplorerCoreUtilities.LoadEmbeddedFile($@"Packages.EmptyLevels.{emptyLevelName}.{(game == MEGame.ME1 ? ".SFM" : "pcc")}");
            using var pcc = MEPackageHandler.OpenMEPackageFromStream(packageStream);
            for (int i = 0; i < pcc.Names.Count; i++)
            {
                string name = pcc.Names[i];
                if (name.Equals(emptyLevelName))
                {
                    var newName = name.Replace(emptyLevelName, Path.GetFileNameWithoutExtension(outpath));
                    pcc.replaceName(i, newName);
                }
            }

            var packguid = Guid.NewGuid();
            var packageExport = pcc.GetUExport(game switch
            {
                MEGame.LE1 => 4,
                MEGame.LE3 => 6,
                MEGame.ME2 => 7,
                _ => 1
            });
            packageExport.PackageGUID = packguid;
            pcc.PackageGuid = packguid;
            pcc.Save(outpath); // You must pass the path here as this file was loaded from memory
        }

        private static void Package_noLongerUsed(UnrealPackageFile sender)
        {
            var packagePath = sender.FilePath;
            if (Path.GetFileNameWithoutExtension(packagePath) != "Core") //Keep Core loaded as it is very often referenced
            {
                if (openPackages.TryRemove(packagePath, out IMEPackage _))
                {
                    //Debug.WriteLine($"Released from package cache: {packagePath}");
                }
                else
                {
                    Debug.WriteLine($"Failed to remove package from cache: {packagePath}");
                }
            }
        }

        private static void addToPackagesInTools(IMEPackage package)
        {
            if (!packagesInTools.Contains(package))
            {
                packagesInTools.Add(package);
                package.noLongerOpenInTools += Package_noLongerOpenInTools;
            }
        }

        private static void Package_noLongerOpenInTools(UnrealPackageFile sender)
        {
            IMEPackage package = sender as IMEPackage;
            packagesInTools.Remove(package);
            sender.noLongerOpenInTools -= Package_noLongerOpenInTools;

        }

        public static IMEPackage OpenUDKPackage(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, user, forceLoadFromDisk);
            if (pck.Game == MEGame.UDK)
            {
                return pck;
            }

            pck.Release(user);
            throw new FormatException("Not a UDK package file.");
        }

        public static IMEPackage OpenME3Package(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, user, forceLoadFromDisk);
            if (pck.Game == MEGame.ME3)
            {
                return pck;
            }

            pck.Release(user);
            throw new FormatException("Not an ME3 package file.");
        }

        public static IMEPackage OpenME2Package(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, user, forceLoadFromDisk);
            if (pck.Game == MEGame.ME2)
            {
                return pck;
            }

            pck.Release(user);
            throw new FormatException("Not an ME2 package file.");
        }

        public static IMEPackage OpenME1Package(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, user, forceLoadFromDisk);
            if (pck.Game == MEGame.ME1)
            {
                return pck;
            }

            pck.Release(user);
            throw new FormatException("Not an ME1 package file.");
        }

        // LEGENDARY EDITION
        public static IMEPackage OpenLE3Package(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, user, forceLoadFromDisk);
            if (pck.Game == MEGame.LE3)
            {
                return pck;
            }

            pck.Release(user);
            throw new FormatException("Not an LE3 package file.");
        }

        public static IMEPackage OpenLE2Package(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, user, forceLoadFromDisk);
            if (pck.Game == MEGame.LE2)
            {
                return pck;
            }

            pck.Release(user);
            throw new FormatException("Not an LE2 package file.");
        }

        public static IMEPackage OpenLE1Package(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, user, forceLoadFromDisk);
            if (pck.Game == MEGame.LE1)
            {
                return pck;
            }

            pck.Release(user);
            throw new FormatException("Not an LE1 package file.");
        }

        public static bool IsPackageInUse(string pathToFile) => openPackages.ContainsKey(Path.GetFullPath(pathToFile));

        public static void PrintOpenPackages()
        {
            Debug.WriteLine("Open Packages:");
            foreach (KeyValuePair<string, IMEPackage> package in openPackages)
            {
                Debug.WriteLine(package.Key);
            }
        }

        /// <summary>
        /// Does not register use!
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="package"></param>
        /// <returns></returns>
        public static bool TryGetPackageFromCache(string filePath, out IMEPackage package) => openPackages.TryGetValue(filePath, out package);

        //useful for scanning operations, where a common set of packages are going to be referenced repeatedly
        public static DisposableCollection<IMEPackage> OpenMEPackages(IEnumerable<string> filePaths) => new(filePaths.Select(filePath => OpenMEPackage(filePath)));
    }

    public class DisposableCollection<T> : List<T>, IDisposable where T : IDisposable
    {
        public DisposableCollection() : base() { }
        public DisposableCollection(IEnumerable<T> collection) : base(collection) { }
        public DisposableCollection(int capacity) : base(capacity) { }

        public void Dispose()
        {
            foreach (T disposable in this)
            {
                disposable?.Dispose();
            }
        }
    }
}