﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Numerics;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;

//using ImageMagick;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    /// <summary>
    /// Class where Mgamerz can put debug/dev/experimental code
    /// </summary>
    class PackageEditorExperimentsM
    {
        public static void CompareISB()
        {

        }

        public static void OverrideVignettes(PackageEditorWindow pewpf)
        {

            Task.Run(() =>
            {
                pewpf.BusyText = "Enumerating exports for PPS...";
                pewpf.IsBusy = true;
                var allFiles = MELoadedFiles.GetOfficialFiles(MEGame.LE3).Where(x => Path.GetExtension(x) == ".pcc").ToList();
                int totalFiles = allFiles.Count;
                int numDone = 0;
                foreach (string filePath in allFiles)
                {
                    //if (!filePath.EndsWith("Engine.pcc"))
                    //    continue;
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                    foreach (var f in pcc.Exports)
                    {
                        var props = f.GetProperties();
                        foreach (var prop in props)
                        {
                            if (prop is StructProperty sp && sp.StructType == "PostProcessSettings")
                            {
                                var vignette = sp.GetProp<BoolProperty>("bEnableVignette");
                                var vigOverride = sp.GetProp<BoolProperty>("bOverride_EnableVignette");

                                if (vigOverride != null && vignette != null)
                                {
                                    vignette.Value = false;
                                    vigOverride.Value = true;
                                    f.WriteProperty(sp);
                                }
                            }
                        }

                    }

                    if (pcc.IsModified)
                        pcc.Save();

                    numDone++;
                    pewpf.BusyText = $"Enumerating exports for PPS [{numDone}/{totalFiles}]";
                }
            }).ContinueWithOnUIThread(foundCandidates => { pewpf.IsBusy = false; });
        }

        public static void UpdateTexturesMatsToGame(PackageEditorWindow pewpf)
        {
            Task.Run(() =>
            {
                pewpf.BusyText = "Updating objects...";
                pewpf.IsBusy = true;
                var packages = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE3);

                //var updatableObjects = pewpf.Pcc.Exports.Where(x => x.IsTexture());
                //updatableObjects = updatableObjects.Concat(pewpf.Pcc.Exports.Where(x => x.ClassName == @"Material"));

                var lookupPackages = new[] { @"BIOG_HMF_ARM_HVY_R.pcc", @"BIOG_HMF_ARM_CTH_R.pcc", @"Startup.pcc" };
                foreach (var lookupPackage in lookupPackages)
                {
                    using var importPackage = MEPackageHandler.OpenMEPackage(packages[lookupPackage]);

                    foreach (var sourceObj in importPackage.Exports)
                    {
                        var matchingObj = pewpf.Pcc.Exports.FirstOrDefault(x => x.InstancedFullPath == sourceObj.InstancedFullPath);
                        if (matchingObj != null && !matchingObj.DataChanged)
                        {
                            if (!shouldUpdateObject(matchingObj))
                                continue;

                            var resultst = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular,
                                sourceObj, matchingObj.FileRef, matchingObj, true, out _,
                                errorOccuredCallback: x => throw new Exception(x),
                                importExportDependencies: true);
                            if (resultst.Any())
                            {
                                Debug.WriteLine("MERGE FAILED!");
                            }

                            if (matchingObj.DataChanged)
                                Debug.WriteLine($@"Updated {matchingObj.InstancedFullPath}");
                        }
                        else
                        {
                            //Debug.WriteLine($@"Did not update {sourceObj.InstancedFullPath}");
                        }
                    }


                }

            }).ContinueWithOnUIThread(x =>
            {
                pewpf.IsBusy = false;
            });
        }

        private static bool shouldUpdateObject(ExportEntry matchingObj)
        {
            if (matchingObj.ClassName == @"ObjectReferencer") return false;
            if (matchingObj.ClassName == @"ObjectRedirector") return false;

            return true;
        }

        public static void EnumerateAllFunctions(PackageEditorWindow pewpf)
        {

            Task.Run(() =>
            {
                pewpf.BusyText = "Enumerating functions...";
                pewpf.IsBusy = true;
                var allFiles = MELoadedFiles.GetOfficialFiles(MEGame.LE3).Where(x => Path.GetExtension(x) == ".pcc").ToList();
                int totalFiles = allFiles.Count;
                int numDone = 0;
                foreach (string filePath in allFiles)
                {
                    //if (!filePath.EndsWith("Engine.pcc"))
                    //    continue;
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                    foreach (var f in pcc.Exports.Where(x => x.ClassName is "Function" or "State"))
                    {
                        if (pcc.Game is MEGame.ME1 or MEGame.ME2)
                        {
                            var func = f.ClassName == "State" ? UE3FunctionReader.ReadState(f, f.Data) : UE3FunctionReader.ReadFunction(f, f.Data);
                            func.Decompile(new TextBuilder(), false, true); //parse bytecode
                        }
                        else
                        {
                            var func = new Function(f.Data, f);
                            func.ParseFunction();
                        }
                    }

                    numDone++;
                    pewpf.BusyText = $"Enumerating functions [{numDone}/{totalFiles}]";
                }
            }).ContinueWithOnUIThread(foundCandidates => { pewpf.IsBusy = false; });
        }

        public static void ShaderCacheResearch(PackageEditorWindow pewpf)
        {
            Dictionary<string, int> mapCount = new Dictionary<string, int>();
            bool ScanForNames(byte[] bytes, IMEPackage package)
            {
                bool result = false;
                int pos = 0;
                //while (pos < bytes.Length - 8)
                //{
                var nameP1 = BitConverter.ToInt32(bytes, pos);
                var nameP2 = BitConverter.ToInt32(bytes, pos + 4);

                if (nameP1 != 0 && nameP2 == 0 && package.IsName(nameP1))
                {
                    var name = package.GetNameEntry(nameP1);
                    if (!mapCount.TryGetValue(name, out var count))
                    {
                        count = 1;
                    }
                    else
                    {
                        count++;
                    }
                    mapCount[name] = count;
                    result = name.StartsWith("F");
                }
                pos++;
                //}

                return result;
            }

            Task.Run(() =>
            {
                pewpf.BusyText = "Scanning ShaderCache files...";
                pewpf.IsBusy = true;
                Dictionary<string, int> typeCount = new Dictionary<string, int>();

                var files = Directory.GetFiles(@"X:\Downloads\f", "*.pcc");
                foreach (var f in files)
                {
                    var package = MEPackageHandler.OpenMEPackage(f, forceLoadFromDisk: true);
                    var sfsce = package.FindExport("SeekFreeShaderCache");
                    if (sfsce != null)
                    {
                        var sfsc = ObjectBinary.From<ShaderCache>(sfsce);
                        foreach (var shaderPair in sfsc.Shaders)
                        {
                            var isF = ScanForNames(shaderPair.Value.unkBytes, package);
                            if (isF)
                            {
                                if (!typeCount.TryGetValue(shaderPair.Value.ShaderType, out var count))
                                {
                                    count = 1;
                                }
                                else
                                {
                                    count++;
                                }
                                typeCount[shaderPair.Value.ShaderType] = count;
                            }
                        }
                    }
                }

                Debug.WriteLine("");
                foreach (var kp in mapCount.OrderByDescending(x => x.Value))
                {
                    Debug.WriteLine($"{kp.Key}: {kp.Value}");
                }

                Debug.WriteLine("");
                Debug.WriteLine("Type counts:");
                foreach (var kp in typeCount.OrderByDescending(x => x.Value))
                {
                    Debug.WriteLine($"{kp.Key}: {kp.Value}");
                }
                return true;
            }).ContinueWithOnUIThread(foundCandidates =>
            {
                pewpf.IsBusy = false;
            });
        }

        public static void ResetTexturesInFile(IMEPackage sourcePackage, PackageEditorWindow pewpf)
        {
            if (sourcePackage.Game != MEGame.ME1 && sourcePackage.Game != MEGame.ME2 && sourcePackage.Game != MEGame.ME3)
            {
                MessageBox.Show(pewpf, "Not a trilogy file!");
                return;
            }

            Task.Run(() =>
            {
                pewpf.BusyText = "Finding unmodded candidates...";
                pewpf.IsBusy = true;
                return pewpf.GetUnmoddedCandidatesForPackage();
            }).ContinueWithOnUIThread(foundCandidates =>
            {
                pewpf.IsBusy = false;
                if (!foundCandidates.Result.Any())
                {
                    MessageBox.Show(pewpf, "Cannot find any candidates for this file!");
                    return;
                }

                var choices = foundCandidates.Result.DiskFiles.ToList(); //make new list
                choices.AddRange(foundCandidates.Result.SFARPackageStreams.Select(x => x.Key));

                var choice = InputComboBoxDialog.GetValue(pewpf, "Choose file to reset to:", "Texture reset", choices, choices.Last());
                if (string.IsNullOrEmpty(choice))
                {
                    return;
                }

                var restorePackage = MEPackageHandler.OpenMEPackage(choice, forceLoadFromDisk: true);

                // Get classes
                var differences = restorePackage.CompareToPackage(sourcePackage);

                // Classes
                var classNames = differences.Where(x => x.Entry != null).Select(x => x.Entry.ClassName).Distinct().OrderBy(x => x).ToList();
                if (classNames.Any())
                {
                    var allDiffs = "[ALL DIFFERENCES]";
                    classNames.Insert(0, allDiffs);
                    var restoreClass = InputComboBoxDialog.GetValue(pewpf, "Select class type to restore instances of:", "Data reset", classNames, classNames.FirstOrDefault());
                    if (string.IsNullOrEmpty(restoreClass))
                    {
                        return;
                    }

                    foreach (var exp in restorePackage.Exports.Where(x => x.ClassName != "BioMaterialInstanceConstant" || restoreClass == allDiffs || x.ClassName == restoreClass))
                    {
                        var origExp = restorePackage.GetUExport(exp.UIndex);
                        sourcePackage.GetUExport(exp.UIndex).Data = origExp.Data;
                        sourcePackage.GetUExport(exp.UIndex).Header = origExp.Header;
                    }
                }
            });
        }

        public static void DumpPackageTextures(IMEPackage sourcePackage, PackageEditorWindow pewpf)
        {
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select Folder Containing Localized Files"
            };
            if (dlg.ShowDialog(pewpf) == CommonFileDialogResult.Ok)
            {
                foreach (var t2dx in sourcePackage.Exports.Where(x => x.IsTexture()))
                {
                    var outF = Path.Combine(dlg.FileName, t2dx.ObjectName + ".png");
                    var t2d = new Texture2D(t2dx);
                    t2d.ExportToPNG(outF);
                }
            }

            MessageBox.Show("Done");
        }

        public static void CompactFileViaExternalFile(IMEPackage sourcePackage)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "*.pcc|*.pcc" };
            if (d.ShowDialog() == true)
            {

                using var compactedAlready = MEPackageHandler.OpenMEPackage(d.FileName);
                var fname = Path.GetFileNameWithoutExtension(sourcePackage.FilePath);
                var exportsToKeep = sourcePackage.Exports
                    .Where(x => x.FullPath == fname || x.FullPath == @"SeekFreeShaderCache" || x.FullPath.StartsWith("ME3ExplorerTrashPackage")).ToList();

                var entriesToTrash = new ConcurrentBag<ExportEntry>();
                Parallel.ForEach(sourcePackage.Exports, export =>
                {
                    var matchingExport = exportsToKeep.FirstOrDefault(x => x.FullPath == export.FullPath);
                    if (matchingExport == null)
                    {
                        matchingExport = compactedAlready.Exports.FirstOrDefault(x => x.FullPath == export.FullPath);
                    }

                    if (matchingExport == null)
                    {
                        //Debug.WriteLine($"Trash {export.FullPath}");
                        entriesToTrash.Add(export);
                    }
                });

                EntryPruner.TrashEntries(sourcePackage, entriesToTrash);
            }
        }



        /// <summary>
        /// Builds a comparison of TESTPATCH functions against their original design. View the difference with WinMerge Folder View.
        /// By Mgamerz
        /// </summary>
        public static void BuildTestPatchComparison()
        {
            var oldPath = ME3Directory.DefaultGamePath;
            // To run this change these values

            // Point to unpacked path.
            ME3Directory.DefaultGamePath = @"Z:\Mass Effect 3";
            var patchedOutDir = Directory.CreateDirectory(@"C:\users\mgamerz\desktop\patchcomp\patch").FullName;
            var origOutDir = Directory.CreateDirectory(@"C:\users\mgamerz\desktop\patchcomp\orig").FullName;
            var patchFiles = Directory.GetFiles(@"C:\Users\Mgamerz\Desktop\ME3CMM\data\Patch_001_Extracted\BIOGame\DLC\DLC_TestPatch\CookedPCConsole", "Patch_*.pcc");

            // End variables

            //preload these packages to speed up lookups
            using var package1 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "SFXGame.pcc"));
            using var package2 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "Engine.pcc"));
            using var package3 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "Core.pcc"));
            using var package4 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "Startup.pcc"));
            using var package5 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "GameFramework.pcc"));
            using var package6 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "GFxUI.pcc"));
            using var package7 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "BIOP_MP_COMMON.pcc"));

            // These paths can't be easily determined so just manually build list
            // Some are empty paths cause they could be determined with code updates 
            // and i was too lazy to remove them.
            Dictionary<string, string> extraMappings = new Dictionary<string, string>()
            {
                {"SFXGameContent.SFXAICmd_Base_GethPrimeShieldDrone", "SFXPawn_GethPrime"},
{"SFXGameMPContent.SFXGameEffect_MatchConsumable_AmmoPower_ArmorPiercing", "SFXGE_MatchConsumables"},
{"SFXGameMPContent.SFXGameEffect_MatchConsumable_AmmoPower_Disruptor", "SFXGE_MatchConsumables"},
{"SFXGameMPContent.SFXObjective_Retrieve_PickupObject", "SFXEngagement_Retrieve"},
{"SFXGameContentDLC_CON_MP2.SFXObjective_Retrieve_PickupObject_DLC", "SFXEngagement_RetrieveDLC"},
{"SFXGameContentDLC_CON_MP2.SFXObjective_Retrieve_DropOffLocation_DLC", "SFXEngagement_RetrieveDLC"},
{"SFXGameContent.SFXPowerCustomAction_GethPrimeTurret", "SFXPawn_GethPrime"},
{"SFXGameContent.SFXPowerCustomAction_ConcussiveShot", ""},
{"SFXGameContent.SFXPowerCustomAction_BioticCharge", ""},
{"SFXGameContentDLC_CON_MP1.SFXProjectile_BatarianSniperRound", "SFXWeapon_SniperRifle_BatarianDLC"},
{"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_BioticCharge_Krogan", "SFXPower_KroganBioticCharge"},
{"SFXGameMPContent.SFXPowerCustomActionMP_FemQuarianPassive", "SFXPowerMP_FemQuarPassive"},
{"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_KroganPassive_Vanguard", "SFXPower_KroganVanguardPassive"},
{"SFXGameContentDLC_CON_MP2.SFXPowerCustomActionMP_MaleQuarianPassive", "SFXPower_MQPassive"},
{"SFXGameMPContent.SFXPowerCustomActionMP_AsariPassive", ""},
{"SFXGameMPContent.SFXPowerCustomActionMP_DrellPassive", ""},
{"SFXGameMPContent.SFXPowerCustomActionMP_HumanPassive", ""},
{"SFXGameMPContent.SFXPowerCustomActionMP_KroganPassive", ""},
{"SFXGameMPContent.SFXPowerCustomActionMP_PassiveBase", ""},
{"SFXGameMPContent.SFXPowerCustomActionMP_SalarianPassive", ""},
{"SFXGameMPContent.SFXPowerCustomActionMP_TurianPassive", ""},
{"SFXGameContentDLC_CON_MP2.SFXPowerCustomActionMP_VorchaPassive", "SFXPower_VorchaPassive"},
{"SFXGameContentDLC_CON_MP2.SFXPowerCustomActionMP_WhipManPassive", "SFXPower_WhipManPassive"},
{"SFXGameContent.SFXAICmd_Banshee_Aggressive", "SFXpawn_Banshee"},
{"SFXGameContent.SFXAI_GethPrimeShieldDrone", "SFXPawn_GethPrime"},
{"SFXGameContent.SFXAI_ProtectorDrone", "SFXPower_ProtectorDrone"},
{"SFXGameContent.SFXAmmoContainer", "Biod_MPTowr"},
{"SFXGameContentDLC_CON_MP3.SFXCustomAction_N7TeleportPunchBase", "N7_Vanguard_MP"},
{"SFXGameContentDLC_CON_MP3.SFXCustomAction_N7VanguardEvadeBase", "N7_Vanguard_MP"},
{"SFXGameContent.SFXCustomAction_SimpleMoveBase", "SFXPawn_GethPyro"},
{"SFXGameContent.SFXCustomAction_BansheeDeath", "SFXPawn_Banshee"},
{"SFXGameContent.SFXCustomAction_BansheePhase", "SFXPawn_Banshee"},
{"SFXGameContent.SFXCustomAction_DeployTurret", "SFXPawn_Gunner"},
{"SFXGameMPContent.SFXCustomAction_KroganRoar", "Krogan_Soldier_MP"},
{"SFXGameContent.SFXCustomAction_Revive", "SFXCharacterClass_Infiltrator"},
{"SFXGameContent.SFXDroppedGrenade", "Biod_MPTowr"},
{"SFXGameContentDLC_CON_MP2_Retrieve.SFXEngagement_Retrieve_DLC", "Startup_DLC_CON_MP2_INT"},
{"SFXGameContent.SFXGameEffect_WeaponMod_PenetrationDamageBonus", "SFXWeaponMods_AssaultRifles"},
{"SFXGameContent.SFXGameEffect_WeaponMod_WeightBonus", "SFXWeaponMods_SMGs"},
{"SFXGameContentDLC_CON_MP1.SFXGameEffect_BatarianBladeDamageOverTime", "Batarian_Soldier_MP"},
{"SFXGameContent.SFXGrenadeContainer", "Biod_MPTowr"},
{"SFXGameMPContent.SFXObjective_Retrieve_DropOffLocation", "SFXEngagement_Retrieve"},
{"SFXGameMPContent.SFXObjective_Annex_DefendZone", "SFXEngagement_Annex_Upload"},
{"SFXGameMPContent.SFXObjective_Disarm_Base", "SFXEngagement_Disarm_Disable"},
{"SFXGameContentDLC_CON_MP3.SFXObjective_MobileAnnex", "SFXMobileAnnex"},
{"SFXOnlineFoundation.SFXOnlineComponentAchievementPC", ""}, //totes new
{"SFXGameContentDLC_CON_MP2.SFXPawn_PlayerMP_Sentinel_Vorcha", "Vorcha_Sentinel_MP"},
{"SFXGameContentDLC_CON_MP2.SFXPawn_PlayerMP_Soldier_Vorcha", "Vorcha_Soldier_MP"},
{"SFXGameContent.SFXPawn_GethPrimeShieldDrone", "SFXPawn_gethPrime"},
{"SFXGameContent.SFXPawn_GethPrimeTurret", "SFXPawn_GethPrime"},
{"SFXGameContent.SFXPawn_GunnerTurret", "SFXPawn_Gunner"},
{"SFXGameMPContent.SFXPawn_Krogan_MP", "Krogan_Soldier_MP"},
{"SFXGameContentDLC_CON_MP3.SFXPawn_PlayerMP_Sentinel_N7", "N7_Sentinel_MP"},
{"SFXGameContent.SFXPawn_Swarmer", "SFXPawn_Ravager"},
{"SFXGameContentDLC_CON_MP2.SFXPowerCustomActionMP_Damping", ""},
{"SFXGameContent.SFXPowerCustomAction_AIHacking", ""},
{"SFXGameContentDLC_CON_MP2.SFXPowerCustomActionMP_Flamer", ""},
{"SFXGameMPContent.SFXPowerCustomActionMP_Reave", ""},
{"SFXGameContentDLC_CON_MP3.SFXPowerCustomActionMP_Slash", ""},
{"SFXGameContent.SFXPowerCustomAction_Carnage", "SFXPower_Carnage"},
{"SFXGameContent.SFXPowerCustomAction_Marksman", "SFXPower_Marksman"},
{"SFXGameContent.SFXPowerCustomAction_Reave", "SFXPower_Reave"},
{"SFXGameContent.SFXPowerCustomAction_Stasis", "SFXPower_Stasis"},
{"SFXGameContent.SFXProjectile_BansheePhase", "SFXPawn_Banshee"},
{"SFXGameContentDLC_CON_MP1.SFXPawn_PlayerMP_Sentinel_Batarian", "Batarian_Sentinel_MP"},
{"SFXGameContentDLC_CON_MP1.SFXPawn_PlayerMP_Soldier_Batarian", "Batarian_Soldier_MP"},
{"SFXGameContent.SFXPowerCustomAction_AdrenalineRush", "SFXPower_AdrenalineRush"},
{"SFXGameContent.SFXPowerCustomAction_DefensiveShield", ""},
{"SFXGameContent.SFXPowerCustomAction_Fortification", ""},
{"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_AsariCommandoPassive", "SFXPower_AsariCommandoPassive"},
{"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_BatarianAttack", "SFXPower_BatarianAttack"},
{"SFXGameMPContent.SFXPowerCustomActionMP_BioticCharge", ""},
{"SFXGameMPContent.SFXPowerCustomActionMP_ConcussiveShot", ""},
{"SFXGameMPContent.SFXPowerCustomActionMP_Marksman", ""},
{"SFXGameContentDLC_CON_MP3.SFXPowerCustomActionMP_ShadowStrike", ""},
{"SFXGameMPContent.SFXPowerCustomActionMP_Singularity", ""},
{"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_BatarianPassive", "SFXPower_BatarianPassive"},
{"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_GethPassive", "SFXPower_GethPassive"},
{"SFXGameContent.SFXPowerCustomAction_Singularity", "SFXPower_Singularity"},
{"SFXGameContent.SFXPowerCustomAction_Incinerate", "SFXPower_Incinerate"},
{"SFXGameContent.SFXSeqAct_OpenWeaponSelection", "BioP_Cat002"},
{"SFXGameContent.SFXSeqAct_ClearParticlePools", "BioD_KroGar_500Gate"},
{"SFXGameContentLiveKismet.SFXSeqAct_SetAreaMap", "BioD_Cithub_Dock"},
{"SFXGameContent.SFXShield_EVA", "Biod_promar_710chase"},
{"SFXGameContent.SFXShield_Phantom", "SFXPawn_Phantom"},
{"SFXGameContentDLC_CON_MP2.SFXWeapon_Shotgun_Quarian", "SFXWeapon_Shotgun_QuarianDLC"},
{"SFXGameContentDLC_CON_MP2.SFXWeapon_SniperRifle_Turian", "SFXWeapon_SniperRifle_TurianDLC"},
{"SFXGameContentDLC_CON_GUN01.SFXWeapon_SniperRifle_Turian_GUN01", "SFXWeapon_SniperRifle_Turian_GUN01"},
{"SFXGameContentDLC_CON_MP1.SFXWeapon_Heavy_FlameThrower_GethTurret", "SFXPower_GethSentryTurret"}
            };
            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.ME3);
            List<string> outs = new List<string>();

            foreach (var pf in patchFiles)
            {
                using var package = MEPackageHandler.OpenMEPackage(pf);
                var classExp = package.Exports.FirstOrDefault(x => x.ClassName == "Class");
                if (classExp != null)
                {
                    // attempt to find base class?
                    // use resolver code so just fake an import
                    var ie = new ImportEntry(classExp.FileRef, classExp.idxLink, classExp.ObjectName)
                    {
                        ClassName = classExp.ClassName,
                        PackageFile = classExp.ParentName,
                    };
                    Debug.WriteLine("Looking up patch source " + classExp.InstancedFullPath);
                    ExportEntry matchingExport = null;
                    if (extraMappings.TryGetValue(classExp.FullPath, out var lookAtFname) && gameFiles.TryGetValue(lookAtFname + ".pcc", out var fullpath))
                    {
                        using var newP = MEPackageHandler.OpenMEPackage(fullpath);
                        var lookupCE = newP.Exports.FirstOrDefault(x => x.FullPath == classExp.FullPath);
                        if (lookupCE != null)
                        {
                            matchingExport = lookupCE;
                        }
                    }
                    else if (gameFiles.TryGetValue(classExp.ObjectName.Name.Replace("SFXPowerCustomAction", "SFXPower") + ".pcc", out var fullpath2))
                    {
                        using var newP = MEPackageHandler.OpenMEPackage(fullpath2);
                        // sfxgame.sfxgame is special case
                        if (classExp.ObjectName == "SFXGame")
                        {
                            var lookupCE = newP.Exports.FirstOrDefault(x => x.FullPath == "SFXGame");
                            if (lookupCE != null)
                            {
                                matchingExport = lookupCE;
                            }
                        }
                        else
                        {
                            var lookupCE = newP.Exports.FirstOrDefault(x => x.FullPath == classExp.FullPath);
                            if (lookupCE != null)
                            {
                                matchingExport = lookupCE;
                            }
                        }
                    }
                    else if (gameFiles.TryGetValue(classExp.ObjectName.Name.Replace("SFXPowerCustomActionMP", "SFXPower") + ".pcc", out var fullpath3))
                    {
                        using var newP = MEPackageHandler.OpenMEPackage(fullpath3);
                        var lookupCE = newP.Exports.FirstOrDefault(x => x.FullPath == classExp.FullPath);
                        if (lookupCE != null)
                        {
                            matchingExport = lookupCE;
                        }
                    }
                    else
                    {
                        matchingExport = EntryImporter.ResolveImport(ie);

                        if (matchingExport == null)
                        {
                            outs.Add(classExp.InstancedFullPath);
                        }
                    }


                    if (matchingExport != null)
                    {
                        //outs.Add(" >> Found original definition: " + matchingExport.ObjectName + " in " +
                        //                matchingExport.FileRef.FilePath);

                        var childrenFuncs = matchingExport.FileRef.Exports.Where(x =>
                            x.idxLink == matchingExport.UIndex && x.ClassName == "Function");
                        foreach (var v in childrenFuncs)
                        {
                            var localFunc = package.Exports.FirstOrDefault(x => x.FullPath == v.FullPath);
                            if (localFunc != null)
                            {
                                // Decomp original func
                                Function func3 = new Function(v.Data, v);
                                func3.ParseFunction();
                                StringBuilder stringoutput = new StringBuilder();
                                stringoutput.AppendLine(func3.GetSignature());
                                foreach (var t in func3.ScriptBlocks)
                                {
                                    stringoutput.AppendLine(t.text);
                                }

                                string originalFunc = stringoutput.ToString();

                                func3 = new Function(localFunc.Data, localFunc);
                                func3.ParseFunction();
                                stringoutput = new StringBuilder();
                                stringoutput.AppendLine(func3.GetSignature());
                                foreach (var t in func3.ScriptBlocks)
                                {
                                    stringoutput.AppendLine(t.text);
                                }

                                string newFunc = stringoutput.ToString();

                                if (newFunc != originalFunc)
                                {
                                    // put into files for winmerge to look at.
                                    var outname = $"{localFunc.FullPath} {Path.GetFileName(pf)}_{localFunc.UIndex}__{Path.GetFileName(v.FileRef.FilePath)}_{v.UIndex}.txt";
                                    File.WriteAllText(Path.Combine(origOutDir, outname), originalFunc);
                                    File.WriteAllText(Path.Combine(patchedOutDir, outname), newFunc);
                                    Debug.WriteLine("   ============= DIFFERENCE " + localFunc.FullPath);
                                }
                            }
                        }


                    }
                    else
                    {
                        outs.Add(" XX Could not find " + classExp.ObjectName);
                    }
                }
            }

            foreach (var o in outs)
            {
                Debug.WriteLine(o);
            }
            //Restore path.
            ME3Directory.DefaultGamePath = oldPath;
        }

        /// <summary>
        /// Rebuilds all netindexes based on the AdditionalPackageToCook list in the listed file's header
        /// </summary>
        public static void RebuildFullLevelNetindexes()
        {
            string pccPath = @"X:\SteamLibrary\steamapps\common\Mass Effect 3\BIOGame\CookedPCConsole\BioP_MPTowr.pcc";
            //string pccPath = @"X:\m3modlibrary\ME3\Redemption\DLC_MOD_MPMapPack - NetIndexing\CookedPCConsole\BioP_MPCron2.pcc";
            string[] subFiles =
            {
                "BioA_Cat004_000Global",
                "BioA_Cat004_100HangarBay",
                "BioD_Cat004_050Landing",
                "BioD_Cat004_100HangarBay",
                "BioD_MPCron_SubMaster",
                "BioSnd_MPCron"

            };
            Dictionary<int, List<string>> indices = new Dictionary<int, List<string>>();
            using var package = (MEPackage)MEPackageHandler.OpenMEPackage(pccPath);
            //package.AdditionalPackagesToCook = subFiles.ToList();
            //package.Save();
            //return;
            int currentNetIndex = 1;

            var netIndexedObjects = package.Exports.Where(x => x.NetIndex >= 0).OrderBy(x => x.NetIndex).ToList();

            foreach (var v in netIndexedObjects)
            {
                List<string> usages = null;
                if (!indices.TryGetValue(v.NetIndex, out usages))
                {
                    usages = new List<string>();
                    indices[v.NetIndex] = usages;
                }

                usages.Add($"{Path.GetFileNameWithoutExtension(v.FileRef.FilePath)} {v.InstancedFullPath}");
            }

            foreach (var f in package.AdditionalPackagesToCook)
            {
                var packPath = Path.Combine(Path.GetDirectoryName(pccPath), f + ".pcc");
                using var sPackage = (MEPackage)MEPackageHandler.OpenMEPackage(packPath);

                netIndexedObjects = sPackage.Exports.Where(x => x.NetIndex >= 0).OrderBy(x => x.NetIndex).ToList();
                foreach (var v in netIndexedObjects)
                {
                    List<string> usages = null;
                    if (!indices.TryGetValue(v.NetIndex, out usages))
                    {
                        usages = new List<string>();
                        indices[v.NetIndex] = usages;
                    }

                    usages.Add($"{Path.GetFileNameWithoutExtension(v.FileRef.FilePath)} {v.InstancedFullPath}");
                }
            }

            foreach (var i in indices)
            {
                Debug.WriteLine($"NetIndex {i.Key}");
                foreach (var s in i.Value)
                {
                    Debug.WriteLine("   " + s);
                }
            }
        }

        public static void ShiftInterpTrackMove(ExportEntry interpTrackMove)
        {
            var offsetX = int.Parse(PromptDialog.Prompt(null, "Enter X shift offset", "Offset X", "0", true));
            var offsetY = int.Parse(PromptDialog.Prompt(null, "Enter Y shift offset", "Offset Y", "0", true));
            var offsetZ = int.Parse(PromptDialog.Prompt(null, "Enter Z shift offset", "Offset Z", "0", true));

            var props = interpTrackMove.GetProperties();
            var posTrack = props.GetProp<StructProperty>("PosTrack");
            var points = posTrack.GetProp<ArrayProperty<StructProperty>>("Points");
            foreach (var point in points)
            {
                var outval = point.GetProp<StructProperty>("OutVal");
                outval.GetProp<FloatProperty>("X").Value += offsetX;
                outval.GetProp<FloatProperty>("Y").Value += offsetY;
                outval.GetProp<FloatProperty>("Z").Value += offsetZ;
            }

            interpTrackMove.WriteProperties(props);
        }

        /// <summary>
        /// Shifts an ME1 AnimCutscene by specified X Y Z values. Only supports 96NoW (3 32-bit float) animations
        /// By Mgamerz 
        /// </summary>
        /// <param name="export"></param>
        public static void ShiftME1AnimCutscene(ExportEntry export)
        {
            if (ObjectBinary.From(export) is AnimSequence animSeq)
            {
                var offsetX = int.Parse(PromptDialog.Prompt(null, "Enter X offset", "Offset X", "0", true));
                var offsetY = int.Parse(PromptDialog.Prompt(null, "Enter Y offset", "Offset Y", "0", true));
                var offsetZ = int.Parse(PromptDialog.Prompt(null, "Enter Z offset", "Offset Z", "0", true));
                var offsetVec = new Vector3(offsetX, offsetY, offsetZ);

                animSeq.DecompressAnimationData();
                foreach (AnimTrack track in animSeq.RawAnimationData)
                {
                    for (int i = 0; i < track.Positions.Count; i++)
                    {
                        track.Positions[i] = Vector3.Add(track.Positions[i], offsetVec);
                    }
                }

                PropertyCollection props = export.GetProperties();
                animSeq.UpdateProps(props, export.Game);
                export.WritePropertiesAndBinary(props, animSeq);
            }
        }

        public static void DumpAllExecFunctionsFromGame()
        {
            Dictionary<string, string> exportNameSignatureMapping = new Dictionary<string, string>();
            string gameDir = @"Z:\ME3-Backup\BioGame";

            var packages = Directory.GetFiles(gameDir, "*.pcc", SearchOption.AllDirectories);
            var sfars = Directory.GetFiles(gameDir + "\\DLC", "Default.sfar", SearchOption.AllDirectories).ToList();
            sfars.Insert(0, gameDir + "\\Patches\\PCConsole\\Patch_001.sfar");
            foreach (var sfar in sfars)
            {
                Debug.WriteLine("Loading " + sfar);
                DLCPackage dlc = new DLCPackage(sfar);
                foreach (var f in dlc.Files)
                {
                    if (f.isActualFile && Path.GetExtension(f.FileName) == ".pcc")
                    {
                        Debug.WriteLine(" >> Reading " + f.FileName);
                        var packageStream = dlc.DecompressEntry(f);
                        packageStream.Position = 0;
                        var package = MEPackageHandler.OpenMEPackageFromStream(packageStream, Path.GetFileName(f.FileName));
                        foreach (var exp in package.Exports.Where(x => x.ClassName == "Function"))
                        {
                            Function func = new Function(exp.Data, exp);
                            if (func.HasFlag("Exec") && !exportNameSignatureMapping.ContainsKey(exp.FullPath))
                            {
                                func.ParseFunction();
                                StringWriter sw = new StringWriter();
                                sw.WriteLine(func.GetSignature());
                                foreach (var v in func.ScriptBlocks)
                                {
                                    sw.WriteLine($"(MemPos 0x{v.memPosStr}) {v.text}");
                                }
                                exportNameSignatureMapping[exp.FullPath] = sw.ToString();
                            }
                        }
                    }
                }
            }

            foreach (var file in packages)
            {
                Debug.WriteLine(" >> Reading " + file);
                using var package = MEPackageHandler.OpenMEPackage(file);
                foreach (var exp in package.Exports.Where(x => x.ClassName == "Function"))
                {
                    Function func = new Function(exp.Data, exp);
                    if (func.HasFlag("Exec") && !exportNameSignatureMapping.ContainsKey(exp.FullPath))
                    {
                        func.ParseFunction();
                        StringWriter sw = new StringWriter();
                        sw.WriteLine(func.GetSignature());
                        foreach (var v in func.ScriptBlocks)
                        {
                            sw.WriteLine($"(MemPos 0x{v.memPosStr}) {v.text}");
                        }
                        exportNameSignatureMapping[exp.FullPath] = sw.ToString();
                    }
                }
            }

            var lines = exportNameSignatureMapping.Select(x => $"{x.Key}============================================================\n{x.Value}");
            File.WriteAllLines(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "fullfunctionsignatures.txt"), lines);
        }


        /// <summary>
        /// Extracts all NoramlizedAverateColors, tints them, and then reinstalls them to the export they came from
        /// </summary>
        /// <param name="Pcc"></param>
        public static void TintAllNormalizedAverageColors(IMEPackage Pcc)
        {
            MessageBox.Show("This is not implemented, code must be uncommented out");
            //var normalizedExports = Pcc.Exports
            //    .Where(x => x.ClassName == "LightMapTexture2D" && x.ObjectName.Name.StartsWith("NormalizedAverageColor")).ToList();
            //foreach (var v in normalizedExports)
            //{
            //    MemoryStream pngImage = new MemoryStream();
            //    Texture2D t2d = new Texture2D(v);
            //    t2d.ExportToPNG(outStream: pngImage);
            //    pngImage.Position = 0; //reset
            //    MemoryStream outStream = new MemoryStream();
            //    using (var image = new MagickImage(pngImage))
            //    {

            //        var tintColor = MagickColor.FromRgb((byte)128, (byte)0, (byte)0);
            //        //image.Colorize(tintColor, new Percentage(80), new Percentage(5), new Percentage(5) );
            //        //image.Settings.FillColor = tintColor;
            //        //image.Tint("30%", tintColor);
            //        image.Modulate(new Percentage(82), new Percentage(100), new Percentage(0));
            //        //image.Colorize(tintColor, new Percentage(100), new Percentage(0), new Percentage(0) );
            //        image.Write(outStream, MagickFormat.Png32);
            //    }
            //    //outStream = pngImage;
            //    outStream.Position = 0;
            //    outStream.WriteToFile(Path.Combine(Directory.CreateDirectory(@"C:\users\mgame\desktop\normalizedCols").FullName, v.ObjectName.Instanced + ".png"));
            //    var convertedBackImage = new MassEffectModder.Images.Image(outStream, Image.ImageFormat.PNG);
            //    t2d.Replace(convertedBackImage, t2d.Export.GetProperties());
            //}
        }

        /// <summary>
        /// Traverses the Level object's navigation point start to its end and finds which objecst are not in the NavList of the Level
        /// By Mgamerz
        /// </summary>
        /// <param name="Pcc"></param>
        public static void ValidateNavpointChain(IMEPackage Pcc)
        {
            var pl = Pcc.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");
            if (pl != null)
            {
                var persistentLevel = ObjectBinary.From<Level>(pl);
                var nlSU = persistentLevel.NavListStart;
                var nlS = Pcc.GetUExport(nlSU.value);
                List<ExportEntry> navList = new List<ExportEntry>();
                List<ExportEntry> itemsMissingFromWorldNPC = new List<ExportEntry>();
                if (!persistentLevel.NavPoints.Any(x => x.value == nlS.UIndex))
                {
                    itemsMissingFromWorldNPC.Add(nlS);
                }
                var nnP = nlS.GetProperty<ObjectProperty>("nextNavigationPoint");
                navList.Add(nlS);
                Debug.WriteLine($"{nlS.UIndex} {nlS.InstancedFullPath}");
                while (nnP != null)
                {
                    var nextNavigationPoint = nnP.ResolveToEntry(Pcc) as ExportEntry;
                    Debug.WriteLine($"{nextNavigationPoint.UIndex} {nextNavigationPoint.InstancedFullPath}");
                    if (!persistentLevel.NavPoints.Any(x => x.value == nextNavigationPoint.UIndex))
                    {
                        itemsMissingFromWorldNPC.Add(nextNavigationPoint);
                    }
                    navList.Add(nextNavigationPoint);
                    nnP = nextNavigationPoint.GetProperty<ObjectProperty>("nextNavigationPoint");
                }

                Debug.WriteLine($"{navList.Count} items in actual nav chain");
                foreach (var v in itemsMissingFromWorldNPC)
                {
                    Debug.WriteLine($"Item missing from NavPoints list: {v.UIndex} {v.InstancedFullPath}");
                }
            }
        }

        public static void SetAllWwiseEventDurations(IMEPackage Pcc)
        {
            var wwevents = Pcc.Exports.Where(x => x.ClassName == "WwiseEvent").ToList();
            foreach (var wwevent in wwevents)
            {
                var eventbin = wwevent.GetBinaryData<WwiseEvent>();
                if (!eventbin.Links.IsEmpty() && !eventbin.Links[0].WwiseStreams.IsEmpty())
                {
                    var wwstream = Pcc.GetUExport(eventbin.Links[0].WwiseStreams[0]);
                    var streambin = wwstream?.GetBinaryData<WwiseStream>() ?? null;
                    if (streambin != null)
                    {
                        var duration = streambin.GetAudioInfo().GetLength();
                        var durtnMS = wwevent.GetProperty<FloatProperty>("DurationMilliseconds");
                        if (durtnMS != null && duration != null)
                        {
                            durtnMS.Value = (float)duration.TotalMilliseconds;
                            wwevent.WriteProperty(durtnMS);
                        }
                    }
                }
            }
        }

        public static void PrintAllNativeFuncsToDebug(IMEPackage package)
        {
            var newCachedInfo = new SortedDictionary<int, CachedNativeFunctionInfo>();
            foreach (ExportEntry export in package.Exports)
            {
                if (export.ClassName == "Function")
                {

                    BinaryReader reader = new EndianReader(new MemoryStream(export.Data)) { Endian = package.Endian };
                    reader.ReadBytes(12); // skip props
                    int super = reader.ReadInt32();
                    int nextItemInCompChain = reader.ReadInt32();
                    int childProbe = reader.ReadInt32();
                    if (package.Game is MEGame.ME1 or MEGame.ME2)
                    {
                        reader.ReadBytes(8); // some name
                        int line = reader.ReadInt32();
                        int textPos = reader.ReadInt32();
                    }
                    else
                    {
                        reader.ReadInt32(); // memorySize
                    }

                    int scriptSize = reader.ReadInt32();
                    byte[] bytecode = reader.ReadBytes(scriptSize);
                    int nativeIndex = reader.ReadInt16();
                    if (package.Game is MEGame.ME1 or MEGame.ME2)
                    {
                        int operatorPrecedence = reader.ReadByte();
                    }

                    int functionFlags = reader.ReadInt32();
                    if ((functionFlags & UE3FunctionReader._flagSet.GetMask("Net")) != 0)
                    {
                        reader.ReadInt16(); // repOffset
                    }

                    if (package.Game is MEGame.ME1 or MEGame.ME2)
                    {
                        int friendlyNameIndex = reader.ReadInt32();
                        reader.ReadInt32();
                    }

                    var function = new UnFunction(export, export.ObjectName,
                        new FlagValues(functionFlags, UE3FunctionReader._flagSet), bytecode, nativeIndex, 1); // USES PRESET 1 DO NOT TRUST

                    if (nativeIndex != 0 /*&& CachedNativeFunctionInfo.GetNativeFunction(nativeIndex) == null*/)
                    {
                        Debug.WriteLine($">>NATIVE Function {nativeIndex} {export.ObjectName}");
                        var newInfo = new CachedNativeFunctionInfo
                        {
                            nativeIndex = nativeIndex,
                            Name = export.ObjectName,
                            Filename = Path.GetFileName(package.FilePath),
                            Operator = function.Operator,
                            PreOperator = function.PreOperator,
                            PostOperator = function.PostOperator
                        };
                        newCachedInfo[nativeIndex] = newInfo;
                    }
                }
            }
            //Debug.WriteLine(JsonConvert.SerializeObject(new { NativeFunctionInfo = newCachedInfo }, Formatting.Indented));

            //Dictionary<int, string> nativeMap = new Dictionary<int, string>();
            //foreach (var ee in package.Exports.Where(x => x.ClassName == "Function"))
            //{
            //    int nativeIndex = 0;
            //    var data = ee.Data;
            //    var offset = data.Length - (package.Game == MEGame.ME3 || package.Platform == MEPackage.GamePlatform.PS3 ? 4 : 12);
            //    if (package.Platform == MEPackage.GamePlatform.Xenon && package.Game == MEGame.ME1)
            //    {
            //        if (ee.ObjectName.Name == "ClientWeaponSet")
            //            Debugger.Break();
            //        // It's byte aligned. We have to read front to back
            //        int scriptSize = EndianReader.ToInt32(data, 0x28, ee.FileRef.Endian);
            //        nativeIndex = EndianReader.ToInt16(data, scriptSize + 0x2C, ee.FileRef.Endian);
            //        if (nativeIndex == 0) nativeIndex = -1;
            //    }
            //    var flags = nativeIndex == 0 ? EndianReader.ToInt32(data, offset, ee.FileRef.Endian) : 0; // if we calced it don't use it's value
            //    FlagValues fs = new FlagValues(flags, UE3FunctionReader._flagSet);
            //    if (nativeIndex >= 0 || fs.HasFlag("Native"))
            //    {
            //        if (nativeIndex == 0)
            //        {
            //            var nativeBackOffset = ee.FileRef.Game == MEGame.ME3 ? 6 : 7;
            //            if (ee.Game < MEGame.ME3 && ee.FileRef.Platform != MEPackage.GamePlatform.PS3) nativeBackOffset = 0xF;
            //            nativeIndex = EndianReader.ToInt16(data, data.Length - nativeBackOffset, ee.FileRef.Endian);
            //        }
            //        if (nativeIndex > 0)
            //        {
            //            nativeMap[nativeIndex] = ee.ObjectName;
            //        }
            //    }
            //}

            //var natives = nativeMap.OrderBy(x => x.Key).Select(x => $"NATIVE_{x.Value} = 0x{x.Key:X2}");
            //foreach (var n in nativeMap)
            //{
            //    var function = CachedNativeFunctionInfo.GetNativeFunction(n.Key); //have to figure out how to do this, it's looking up name of native function
            //    if (function == null)
            //    {
            //        Debug.WriteLine($"NATIVE_{n.Value} = 0x{n.Key:X2}");
            //    }
            //}
        }

        public static void BuildME1NativeFunctionsInfo()
        {
            if (ME1Directory.DefaultGamePath != null)
            {
                var newCachedInfo = new SortedDictionary<int, CachedNativeFunctionInfo>();
                var dir = new DirectoryInfo(ME1Directory.DefaultGamePath);
                var filesToSearch = dir.GetFiles( /*"*.sfm", SearchOption.AllDirectories).Union(dir.GetFiles(*/"*.u",
                    SearchOption.AllDirectories).ToArray();
                Debug.WriteLine("Number of files: " + filesToSearch.Length);
                foreach (FileInfo fi in filesToSearch)
                {
                    using (var package = MEPackageHandler.OpenME1Package(fi.FullName))
                    {
                        Debug.WriteLine(fi.Name);
                        foreach (ExportEntry export in package.Exports)
                        {
                            if (export.ClassName == "Function")
                            {

                                BinaryReader reader = new BinaryReader(new MemoryStream(export.Data));
                                reader.ReadBytes(12);
                                int super = reader.ReadInt32();
                                int children = reader.ReadInt32();
                                reader.ReadBytes(12);
                                int line = reader.ReadInt32();
                                int textPos = reader.ReadInt32();
                                int scriptSize = reader.ReadInt32();
                                byte[] bytecode = reader.ReadBytes(scriptSize);
                                int nativeIndex = reader.ReadInt16();
                                int operatorPrecedence = reader.ReadByte();
                                int functionFlags = reader.ReadInt32();
                                if ((functionFlags & UE3FunctionReader._flagSet.GetMask("Net")) != 0)
                                {
                                    reader.ReadInt16(); // repOffset
                                }

                                int friendlyNameIndex = reader.ReadInt32();
                                reader.ReadInt32();
                                var function = new UnFunction(export, package.GetNameEntry(friendlyNameIndex),
                                    new FlagValues(functionFlags, UE3FunctionReader._flagSet), bytecode, nativeIndex,
                                    operatorPrecedence);

                                if (nativeIndex != 0 && CachedNativeFunctionInfo.GetNativeFunction(nativeIndex) == null)
                                {
                                    Debug.WriteLine($">>NATIVE Function {nativeIndex} {export.ObjectName}");
                                    var newInfo = new CachedNativeFunctionInfo
                                    {
                                        nativeIndex = nativeIndex,
                                        Name = export.ObjectName,
                                        Filename = fi.Name,
                                        Operator = function.Operator,
                                        PreOperator = function.PreOperator,
                                        PostOperator = function.PostOperator
                                    };
                                    newCachedInfo[nativeIndex] = newInfo;
                                }
                            }
                        }
                    }
                }
                Debug.WriteLine(JsonConvert.SerializeObject(new { NativeFunctionInfo = newCachedInfo }, Formatting.Indented));

                //File.WriteAllText(Path.Combine(App.ExecFolder, "ME1NativeFunctionInfo.json"),
                //    JsonConvert.SerializeObject(new { NativeFunctionInfo = newCachedInfo }, Formatting.Indented));
                Debug.WriteLine("Done");
            }
        }

        public static void FindME1ME22DATables()
        {
            if (ME1Directory.DefaultGamePath != null)
            {
                var newCachedInfo = new SortedDictionary<int, CachedNativeFunctionInfo>();
                var dir = new DirectoryInfo(Path.Combine(ME1Directory.DefaultGamePath /*, "BioGame", "CookedPC", "Maps"*/));
                var filesToSearch = dir.GetFiles("*.sfm", SearchOption.AllDirectories)
                    .Union(dir.GetFiles("*.u", SearchOption.AllDirectories))
                    .Union(dir.GetFiles("*.upk", SearchOption.AllDirectories)).ToArray();
                Debug.WriteLine("Number of files: " + filesToSearch.Length);
                foreach (FileInfo fi in filesToSearch)
                {
                    using (var package = MEPackageHandler.OpenME1Package(fi.FullName))
                    {
                        foreach (ExportEntry export in package.Exports)
                        {
                            if ((export.ClassName == "BioSWF"))
                            //|| export.ClassName == "Bio2DANumberedRows") && export.ObjectName.Contains("BOS"))
                            {
                                Debug.WriteLine(
                                    $"{export.ClassName}({export.ObjectName.Instanced}) in {fi.Name} at export {export.UIndex}");
                            }
                        }
                    }
                }

                //File.WriteAllText(System.Windows.Forms.Application.StartupPath + "//exec//ME1NativeFunctionInfo.json", JsonConvert.SerializeObject(new { NativeFunctionInfo = newCachedInfo }, Formatting.Indented));
                Debug.WriteLine("Done");
            }
        }

        public static void FindAllME3PowerCustomActions()
        {
            if (ME3Directory.DefaultGamePath != null)
            {
                var newCachedInfo = new SortedDictionary<string, List<string>>();
                var dir = new DirectoryInfo(ME3Directory.DefaultGamePath);
                var filesToSearch = dir.GetFiles("*.pcc", SearchOption.AllDirectories).ToArray();
                Debug.WriteLine("Number of files: " + filesToSearch.Length);
                foreach (FileInfo fi in filesToSearch)
                {
                    using (var package = MEPackageHandler.OpenME3Package(fi.FullName))
                    {
                        foreach (ExportEntry export in package.Exports)
                        {
                            if (export.SuperClassName == "SFXPowerCustomAction")
                            {
                                Debug.WriteLine(
                                    $"{export.ClassName}({export.ObjectName}) in {fi.Name} at export {export.UIndex}");
                                if (newCachedInfo.TryGetValue(export.ObjectName, out List<string> instances))
                                {
                                    instances.Add($"{fi.Name} at export {export.UIndex}");
                                }
                                else
                                {
                                    newCachedInfo[export.ObjectName] = new List<string>
                                        {$"{fi.Name} at export {export.UIndex}"};
                                }
                            }
                        }
                    }
                }


                string outstr = "";
                foreach (KeyValuePair<string, List<string>> instancelist in newCachedInfo)
                {
                    outstr += instancelist.Key;
                    outstr += "\n";
                    foreach (string str in instancelist.Value)
                    {
                        outstr += " - " + str + "\n";
                    }
                }

                File.WriteAllText(@"C:\users\public\me3powers.txt", outstr);
                Debug.WriteLine("Done");
            }
        }

        public static void FindAllME2Powers()
        {
            if (ME2Directory.DefaultGamePath != null)
            {
                var newCachedInfo = new SortedDictionary<string, List<string>>();
                var dir = new DirectoryInfo(ME2Directory.DefaultGamePath);
                var filesToSearch = dir.GetFiles("*.pcc", SearchOption.AllDirectories).ToArray();
                Debug.WriteLine("Number of files: " + filesToSearch.Length);
                foreach (FileInfo fi in filesToSearch)
                {
                    using var package = MEPackageHandler.OpenMEPackage(fi.FullName);
                    foreach (ExportEntry export in package.Exports)
                    {
                        if (export.SuperClassName == "SFXPower")
                        {
                            Debug.WriteLine(
                                $"{export.ClassName}({export.ObjectName}) in {fi.Name} at export {export.UIndex}");
                            if (newCachedInfo.TryGetValue(export.ObjectName, out List<string> instances))
                            {
                                instances.Add($"{fi.Name} at export {export.UIndex}");
                            }
                            else
                            {
                                newCachedInfo[export.ObjectName] = new List<string>
                                    {$"{fi.Name} at export {export.UIndex}"};
                            }
                        }
                    }
                }


                string outstr = "";
                foreach (KeyValuePair<string, List<string>> instancelist in newCachedInfo)
                {
                    outstr += instancelist.Key;
                    outstr += "\n";
                    foreach (string str in instancelist.Value)
                    {
                        outstr += " - " + str + "\n";
                    }
                }

                File.WriteAllText(@"C:\users\public\me2powers.txt", outstr);
                Debug.WriteLine("Done");
            }
        }

        /// <summary>
        /// Asset Database doesn't search by memory entry, so if I'm looking to see if another entry exists I can't find it. For example I'm trying to find all copies of a specific FaceFX anim set.
        /// </summary>
        /// <param name="packageEditorWpf"></param>
        public static void FindNamedObject(PackageEditorWindow packageEditorWpf)
        {
            var namedObjToFind = PromptDialog.Prompt(packageEditorWpf, "Enter the name of the object you want to search for in files", "Object finder");
            if (!string.IsNullOrWhiteSpace(namedObjToFind))
            {
                var dlg = new CommonOpenFileDialog("Pick a folder to scan (includes subdirectories)")
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true
                };
                if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    packageEditorWpf.IsBusy = true;
                    Task.Run(() =>
                    {
                        ConcurrentDictionary<string, string> threadSafeList = new ConcurrentDictionary<string, string>();
                        packageEditorWpf.BusyText = "Getting list of all package files";
                        int numPackageFiles = 0;
                        var files = Directory.GetFiles(dlg.FileName, "*.pcc", SearchOption.AllDirectories).Where(x => x.RepresentsPackageFilePath()).ToList();
                        var totalfiles = files.Count;
                        long filesDone = 0;
                        Parallel.ForEach(files, pf =>
                        {
                            try
                            {
                                using var package = MEPackageHandler.OpenMEPackage(pf);
                                var hasObject = package.Exports.Any(x => x.ObjectName.Name.Equals(namedObjToFind, StringComparison.InvariantCultureIgnoreCase));
                                if (hasObject)
                                {
                                    threadSafeList.TryAdd(pf, pf);
                                }
                            }
                            catch
                            {

                            }

                            long v = Interlocked.Increment(ref filesDone);
                            packageEditorWpf.BusyText = $"Scanning files [{v}/{totalfiles}]";
                        });
                        return threadSafeList;
                    }).ContinueWithOnUIThread(filesWithObjName =>
                    {
                        packageEditorWpf.IsBusy = false;
                        ListDialog ld = new ListDialog(filesWithObjName.Result.Select(x => x.Value), "Object name scan", "Here is the list of files that have this objects of this name within them.", packageEditorWpf);
                        ld.Show();
                    });
                }
            }
        }

        public static void CheckImports(IMEPackage Pcc, PackageCache globalCache = null)
        {
            if (Pcc == null) return;
            PackageCache pc = new PackageCache();
            // Enumerate and resolve all imports.
            foreach (var import in Pcc.Imports)
            {
                if (import.InstancedFullPath.StartsWith("Core."))
                    continue; // Most of these are native-native
                if (GlobalUnrealObjectInfo.IsAKnownNativeClass(import))
                    continue; // Native is always loaded iirc
                              //Debug.WriteLine($@"Resolving {import.FullPath}");
                var export = EntryImporter.ResolveImport(import, globalCache, pc);
                if (export != null)
                {

                }
                else
                {
                    Debug.WriteLine($@" >>> UNRESOLVABLE IMPORT: {import.FullPath}!");
                }
            }
            pc.ReleasePackages();
        }

        public static void RandomizeTerrain(IMEPackage Pcc)
        {
            ExportEntry terrain = Pcc.Exports.FirstOrDefault(x => x.ClassName == "Terrain");
            if (terrain != null)
            {
                Random r = new Random();

                var terrainBin = terrain.GetBinaryData<Terrain>();
                for (int i = 0; i < terrainBin.Heights.Length; i++)
                {
                    terrainBin.Heights[i] = (ushort)(r.Next(2000) + 13000);
                }

                terrain.WriteBinary(terrainBin);
            }
        }

        public static void ResetPackageVanillaPart(IMEPackage sourcePackage, PackageEditorWindow pewpf)
        {
            if (sourcePackage.Game != MEGame.ME1 && sourcePackage.Game != MEGame.ME2 && sourcePackage.Game != MEGame.ME3)
            {
                MessageBox.Show(pewpf, "Not a trilogy file!");
                return;
            }

            Task.Run(() =>
            {
                pewpf.BusyText = "Finding unmodded candidates...";
                pewpf.IsBusy = true;
                return pewpf.GetUnmoddedCandidatesForPackage();
            }).ContinueWithOnUIThread(foundCandidates =>
            {
                pewpf.IsBusy = false;
                if (!foundCandidates.Result.Any()) MessageBox.Show(pewpf, "Cannot find any candidates for this file!");

                var choices = foundCandidates.Result.DiskFiles.ToList(); //make new list
                choices.AddRange(foundCandidates.Result.SFARPackageStreams.Select(x => x.Key));

                var choice = InputComboBoxDialog.GetValue(pewpf, "Choose file to reset to:", "Package reset", choices, choices.Last());
                if (string.IsNullOrEmpty(choice))
                {
                    return;
                }

                var restorePackage = MEPackageHandler.OpenMEPackage(choice, forceLoadFromDisk: true);
                for (int i = 0; i < restorePackage.NameCount; i++)
                {
                    sourcePackage.replaceName(i, restorePackage.GetNameEntry(i));
                }

                foreach (var imp in sourcePackage.Imports)
                {
                    var origImp = restorePackage.FindImport(imp.InstancedFullPath);
                    if (origImp != null)
                    {
                        imp.Header = origImp.Header;
                    }
                }

                foreach (var exp in sourcePackage.Exports)
                {
                    var origExp = restorePackage.FindExport(exp.InstancedFullPath);
                    if (origExp != null)
                    {
                        exp.Data = origExp.Data;
                        exp.Header = origExp.GetHeader();
                    }
                }
            });
        }

        public static void TestLODBias(PackageEditorWindow pew)
        {
            string[] extensions = { ".pcc" };
            FileInfo[] files = new DirectoryInfo(LE3Directory.CookedPCPath)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(f => f.Name.Contains("Cat002") && extensions.Contains(f.Extension.ToLower()))
                .ToArray();
            foreach (var f in files)
            {
                var p = MEPackageHandler.OpenMEPackage(f.FullName, forceLoadFromDisk: true);
                foreach (var tex in p.Exports.Where(x => x.ClassName == "Texture2D"))
                {
                    tex.WriteProperty(new IntProperty(-5, "InternalFormatLODBias"));
                }
                p.Save();
            }
        }

        public static void FindEmptyMips(PackageEditorWindow pew)
        {
            string[] extensions = { ".pcc" };
            FileInfo[] files = new DirectoryInfo(LE3Directory.CookedPCPath)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(f.Extension.ToLower()))
                .ToArray();
            foreach (var f in files)
            {
                var p = MEPackageHandler.OpenMEPackage(f.FullName, forceLoadFromDisk: true);
                foreach (var tex in p.Exports.Where(x => x.ClassName == "Texture2D"))
                {
                    var t = ObjectBinary.From<UTexture2D>(tex);
                    if (t.Mips[0].StorageType == StorageTypes.empty)
                        Debugger.Break();
                }
            }
        }

        public static void ListNetIndexes(PackageEditorWindow pew)
        {
            // Not sure this works
            var strs = new List<string>();
            var Pcc = pew.Pcc;
            foreach (ExportEntry exp in Pcc.Exports)
            {
                if (exp.ParentName == "PersistentLevel")
                {
                    strs.Add($"{exp.NetIndex} {exp.InstancedFullPath}");
                }
            }

            var d = new ListDialog(strs, "NetIndexes", "Here are the netindexes in Package Editor's loaded file", pew);
            d.Show();
        }

        public static void GenerateNewGUIDForFile(PackageEditorWindow pew)
        {
            MessageBox.Show(
                "GetPEWindow() process applies immediately and cannot be undone.\nEnsure the file you are going to regenerate is not open in Legendary Explorer in any tools.\nBe absolutely sure you know what you're doing before you use GetPEWindow()!");
            OpenFileDialog d = new OpenFileDialog
            {
                Title = "Select file to regen guid for",
                Filter = "*.pcc|*.pcc"
            };
            if (d.ShowDialog() == true)
            {
                using (IMEPackage sourceFile = MEPackageHandler.OpenMEPackage(d.FileName))
                {
                    string fname = Path.GetFileNameWithoutExtension(d.FileName);
                    Guid newGuid = Guid.NewGuid();
                    ExportEntry selfNamingExport = null;
                    foreach (ExportEntry exp in sourceFile.Exports)
                    {
                        if (exp.ClassName == "Package"
                            && exp.idxLink == 0
                            && string.Equals(exp.ObjectName.Name, fname, StringComparison.InvariantCultureIgnoreCase))
                        {
                            selfNamingExport = exp;
                            break;
                        }
                    }

                    if (selfNamingExport == null)
                    {
                        MessageBox.Show(
                            "Selected package does not contain a self-naming package export.\nCannot regenerate package file-level GUID if it doesn't contain self-named export.");
                        return;
                    }

                    selfNamingExport.PackageGUID = newGuid;
                    sourceFile.PackageGuid = newGuid;
                    sourceFile.Save();
                }

                MessageBox.Show("Generated a new GUID for package.");
            }
        }

        public static void GenerateGUIDCacheForFolder(PackageEditorWindow pew)
        {
            CommonOpenFileDialog m = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select folder to generate GUID cache on"
            };
            if (m.ShowDialog(pew) == CommonFileDialogResult.Ok)
            {
                string dir = m.FileName;
                string[] files = Directory.GetFiles(dir, "*.pcc");
                if (Enumerable.Any(files))
                {
                    var packageGuidMap = new Dictionary<string, Guid>();
                    var GuidPackageMap = new Dictionary<Guid, string>();

                    pew.IsBusy = true;
                    string guidcachefile = null;
                    foreach (string file in files)
                    {
                        string fname = Path.GetFileNameWithoutExtension(file);
                        if (fname.StartsWith("GuidCache"))
                        {
                            guidcachefile = file;
                            continue;
                        }

                        if (fname.Contains("_LOC_"))
                        {
                            Debug.WriteLine("--> Skipping " + fname);
                            continue; //skip localizations
                        }

                        Debug.WriteLine(Path.GetFileName(file));
                        bool hasPackageNamingItself = false;
                        using (var package = MEPackageHandler.OpenMEPackage(file))
                        {
                            var filesToSkip = new[]
                            {
                                "BioD_Cit004_270ShuttleBay1", "BioD_Cit003_600MechEvent", "CAT6_Executioner",
                                "SFXPawn_Demo", "SFXPawn_Sniper", "SFXPawn_Heavy", "GethAssassin",
                                "BioD_OMG003_125LitExtra"
                            };
                            foreach (ExportEntry exp in package.Exports)
                            {
                                if (exp.ClassName == "Package" && exp.idxLink == 0 &&
                                    !filesToSkip.Contains(exp.ObjectName.Name))
                                {
                                    if (string.Equals(exp.ObjectName.Name, fname,
                                        StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        hasPackageNamingItself = true;
                                    }

                                    Guid guid = exp.PackageGUID;
                                    if (guid != Guid.Empty)
                                    {
                                        GuidPackageMap.TryGetValue(guid, out string packagename);
                                        if (packagename != null && packagename != exp.ObjectName.Name)
                                        {
                                            Debug.WriteLine(
                                                $"-> {exp.UIndex} {exp.ObjectName.Name} has a guid different from already found one ({packagename})! {guid}");
                                        }

                                        if (packagename == null)
                                        {
                                            GuidPackageMap[guid] = exp.ObjectName.Name;
                                        }
                                    }
                                }
                            }
                        }

                        if (!hasPackageNamingItself)
                        {
                            Debug.WriteLine("----HAS NO SELF NAMING EXPORT");
                        }
                    }

                    foreach (KeyValuePair<Guid, string> entry in GuidPackageMap)
                    {
                        // do something with entry.Value or entry.Key
                        Debug.WriteLine($"  {entry.Value} {entry.Key}");
                    }

                    if (guidcachefile != null)
                    {
                        Debug.WriteLine("Opening GuidCache file " + guidcachefile);
                        using (var package = MEPackageHandler.OpenMEPackage(guidcachefile))
                        {
                            var cacheExp = package.Exports.FirstOrDefault(x => x.ObjectName == "GuidCache");
                            if (cacheExp != null)
                            {
                                var data = new MemoryStream();
                                var expPre = cacheExp.Data.Take(12).ToArray();
                                data.Write(expPre, 0, 12); //4 byte header, None
                                data.WriteInt32(GuidPackageMap.Count);
                                foreach (KeyValuePair<Guid, string> entry in GuidPackageMap)
                                {
                                    int nametableIndex = cacheExp.FileRef.FindNameOrAdd(entry.Value);
                                    data.WriteInt32(nametableIndex);
                                    data.WriteInt32(0);
                                    data.Write(entry.Key.ToByteArray(), 0, 16);
                                }

                                cacheExp.Data = data.ToArray();
                            }

                            package.Save();
                        }
                    }

                    Debug.WriteLine("Done. Cache size: " + GuidPackageMap.Count);
                    pew.IsBusy = false;
                }
            }
        }

        public static void MakeAllGrenadesAndAmmoRespawn(PackageEditorWindow pew)
        {
            var ammoGrenades = pew.Pcc.Exports.Where(x =>
                x.ClassName != "Class" && !x.IsDefaultObject && (x.ObjectName == "SFXAmmoContainer" ||
                                                                 x.ObjectName == "SFXGrenadeContainer" ||
                                                                 x.ObjectName == "SFXAmmoContainer_Simulator"));
            foreach (var container in ammoGrenades)
            {
                BoolProperty respawns = new BoolProperty(true, "bRespawns");
                float respawnTimeVal = 20;
                if (container.ObjectName == "SFXGrenadeContainer")
                {
                    respawnTimeVal = 8;
                }

                if (container.ObjectName == "SFXAmmoContainer")
                {
                    respawnTimeVal = 3;
                }

                if (container.ObjectName == "SFXAmmoContainer_Simulator")
                {
                    respawnTimeVal = 5;
                }

                FloatProperty respawnTime = new FloatProperty(respawnTimeVal, "RespawnTime");
                var currentprops = container.GetProperties();
                currentprops.AddOrReplaceProp(respawns);
                currentprops.AddOrReplaceProp(respawnTime);
                container.WriteProperties(currentprops);
            }
        }

        public static void CheckAllGameImports(IMEPackage pewPcc)
        {
            if (pewPcc == null)
                return;

            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(pewPcc.Game);

            PackageCache pc = new PackageCache();
            var safeFiles = EntryImporter.FilesSafeToImportFrom(pewPcc.Game).ToList();
            safeFiles.AddRange(loadedFiles.Where(x => x.Key.StartsWith("Startup_") && (!pewPcc.Game.IsGame2() || x.Key.Contains("_INT"))).Select(x => x.Key));
            if (pewPcc.Game.IsGame3())
            {
                // SP ONLY
                safeFiles.Add(@"BIO_COMMON.pcc");
            }

            foreach (var f in safeFiles.Distinct())
            {
                pc.GetCachedPackage(loadedFiles[f]);
            }

            foreach (var f in loadedFiles)
            {
                using var p = MEPackageHandler.OpenMEPackage(f.Value);
                CheckImports(p, pc);
            }
        }

        public static void DumpAllLE1TLK(PackageEditorWindow pewpf)
        {
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select output folder"
            };
            if (dlg.ShowDialog(pewpf) == CommonFileDialogResult.Ok)
            {
                var langFilter = PromptDialog.Prompt(pewpf,
                    "Enter the language suffix to filter, or blank to dump INT. For example, PLPC, DE, FR.",
                    "Enter language filter", "", true);

                Task.Run(() =>
                {
                    pewpf.BusyText = "Dumping TLKs...";
                    pewpf.IsBusy = true;
                    var allPackages = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE1).ToList();
                    int numDone = 0;
                    foreach (var f in allPackages)
                    {
                        //if (!f.Key.Contains("Startup"))
                        //    continue;
                        pewpf.BusyText = $"Dumping TLKs [{++numDone}/{allPackages.Count}]";
                        using var package = MEPackageHandler.OpenMEPackage(f.Value);
                        foreach (var v in package.LocalTalkFiles)
                        {
                            if (!string.IsNullOrWhiteSpace(langFilter) && !v.Name.EndsWith($"_{langFilter}"))
                            {
                                continue;
                            }
                            var outPath = Path.Combine(dlg.FileName,
                                $"{Path.GetFileNameWithoutExtension(f.Key)}.{package.GetEntry(v.UIndex).InstancedFullPath}.xml");
                            v.saveToFile(outPath);
                        }

                    }
                }).ContinueWithOnUIThread(x => { pewpf.IsBusy = false; });
            }
        }
    }
}
