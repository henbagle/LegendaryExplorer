﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using BioMorphFace = LegendaryExplorerCore.Unreal.Classes.BioMorphFace;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    internal static class PackageEditorExperimentsH
    {
        /// <summary>
        /// Collects all TLK exports from the entire ME1 game and exports them into a single GlobalTLK file
        /// </summary>
        /// <param name="pew">Instance of Package Editor</param>
        public static void BuildME1SuperTLKFile (PackageEditorWindow pew)
        {
            string gameString = InputComboBoxDialog.GetValue(pew, "Choose game to create SuperTLK for:",
                "Create SuperTLK file", new[] { "LE1", "ME1" }, "LE1");
            var game = Enum.Parse<MEGame>(gameString);
            if(!game.IsGame1()) return;

            var locPrompt = new PromptDialog("Enter file localization suffix to scan",
                "Create SuperTLK file", "_INT")
            {
                Owner = pew
            };
            locPrompt.ShowDialog();
            if (locPrompt.DialogResult == false) return;
            var locSuffix = locPrompt.ResponseText;

            string myBasePath = MEDirectories.GetDefaultGamePath(game);
            string searchDir = MEDirectories.GetCookedPath(game);

            CommonOpenFileDialog d = new CommonOpenFileDialog { Title = "Select folder to search", IsFolderPicker = true, InitialDirectory = myBasePath };
            if (d.ShowDialog() == CommonFileDialogResult.Ok)
            {
                searchDir = d.FileName;
            }

            var filter = game is MEGame.LE1 ? "*.pcc|*.pcc" : "*.upk|*.upk";
            Microsoft.Win32.OpenFileDialog outputFileDialog = new () {
                Title = "Select GlobalTlk file to output to (GlobalTlk exports will be completely overwritten)", 
                Filter = filter };
            bool? result = outputFileDialog.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                Debug.WriteLine("No output file specified");
                return;
            }
            string outputFilePath = outputFileDialog.FileName;

            string[] extensions = { ".u", ".upk", ".pcc" };

            pew.IsBusy = true;

            var tlkLines = new SortedDictionary<int, string>();
            var tlkLines_m = new SortedDictionary<int, string>();

            Task.Run(() =>
            {
                FileInfo[] files = new DirectoryInfo(searchDir)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Where(f => extensions.Contains(f.Extension.ToLower()))
                    .ToArray();
                int i = 1;
                foreach (FileInfo f in files)
                {
                    pew.BusyText = $"[{i}/{files.Length}] Scanning Packages for TLK Exports";
                    int basePathLen = myBasePath.Length;
                    if ((f.Name.Contains("LOC") || f.Name.Contains("Startup")) && !f.Name.Contains(locSuffix))
                    {
                        i++;
                        continue;
                    }
                    using (IMEPackage pack = MEPackageHandler.OpenMEPackage(f.FullName))
                    {
                        List<ExportEntry> tlkExports = pack.Exports.Where(x =>
                            (x.ObjectName == "tlk" || x.ObjectName == "tlk_M" || x.ObjectName == "GlobalTlk_tlk" || x.ObjectName == "GlobalTlk_tlk_M") && x.ClassName == "BioTlkFile").ToList();
                        if (tlkExports.Count > 0)
                        {
                            string subPath = f.FullName.Substring(basePathLen);
                            foreach (ExportEntry exp in tlkExports)
                            {
                                var stringMapping = ((exp.ObjectName == "tlk" || exp.ObjectName == "GlobalTlk_tlk") ? tlkLines : tlkLines_m);
                                var talkFile = new ME1TalkFile(exp);
                                foreach (var sref in talkFile.StringRefs)
                                {
                                    if (sref.StringID == 0) continue; //skip blank
                                    if (sref.Data == null || sref.Data == "-1" || sref.Data == "") continue; //skip blank

                                    if (!stringMapping.TryGetValue(sref.StringID, out var dictEntry))
                                    {
                                        stringMapping[sref.StringID] = sref.Data;
                                    }

                                }
                            }
                        }

                        i++;
                    }
                }

                int total = tlkLines.Count;

                using (IMEPackage o = MEPackageHandler.OpenMEPackage(outputFilePath))
                {
                    List<ExportEntry> tlkExports = o.Exports.Where(x =>
                        (x.ObjectName == "GlobalTlk_tlk" || x.ObjectName == "GlobalTlk_tlk_M") && x.ClassName == "BioTlkFile").ToList();
                    if (tlkExports.Count > 0)
                    {
                        foreach (ExportEntry exp in tlkExports)
                        {
                            var stringMapping = (exp.ObjectName == "GlobalTlk_tlk" ? tlkLines : tlkLines_m);
                            var talkFile = new ME1TalkFile(exp);
                            var LoadedStrings = new List<TLKStringRef>();
                            foreach (var tlkString in stringMapping)
                            {
                                // Do the important part
                                LoadedStrings.Add(new TLKStringRef(tlkString.Key, tlkString.Value, 1));
                            }

                            HuffmanCompression huff = new HuffmanCompression();
                            huff.LoadInputData(LoadedStrings);
                            huff.SerializeTalkfileToExport(exp);
                        }
                    }
                    o.Save();

                }

                return total;

            }).ContinueWithOnUIThread(async (total) =>
            {
                var actualTotal = await total;
                pew.IsBusy = false;
                pew.StatusBar_LeftMostText.Text = $"Wrote {actualTotal} lines to {outputFilePath}";
            });

        }

        public static void AssociateAllExtensions()
        {
            FileAssociations.AssociatePCCSFM();
            FileAssociations.AssociateUPKUDK();
            FileAssociations.AssociateOthers();
        }

        public static void CreateAudioSizeInfo(PackageEditorWindow pew, MEGame game = MEGame.ME3)
        {
            pew.IsBusy = true;
            pew.BusyText = $"Creating audio size info for {game}";

            CaseInsensitiveDictionary<long> audioSizes = new();

            Task.Run(() =>
            {
                foreach (string filePath in MELoadedFiles.GetOfficialFiles(game, includeAFCs:true).Where(f => f.EndsWith(".afc", StringComparison.OrdinalIgnoreCase)))
                {
                    var info = new FileInfo(filePath);
                    audioSizes.Add(info.Name.Split('.')[0], info.Length);
                }
            }).ContinueWithOnUIThread((prevTask) =>
            {
                pew.IsBusy = false;

                var outFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"{game}-vanillaaudiosizes.json");
                File.WriteAllText(outFile, JsonConvert.SerializeObject(audioSizes));

            });
        }

        [DllImport(@"C:\Program Files (x86)\Audiokinetic\Wwise 2019.1.6.7110\SDK\x64_vc140\Release\bin\AkSoundEngineDLL.dll")]
        public static extern uint GetIDFromString(string str);

        public static void GenerateWwiseId(PackageEditorWindow pew)
        {
            if (pew.TryGetSelectedExport(out var exp) && File.Exists(Misc.AppSettings.Settings.Wwise_7110Path))
            {
                string name = exp.ObjectName.Name;
                MessageBox.Show(GetIDFromString(name).ToString());
            }
        }

        public static void CreateTestTLKWithStringIDs(PackageEditorWindow pew)
        {
            Microsoft.Win32.OpenFileDialog outputFileDialog = new () { 
                Title = "Select .XML file to import", 
                Filter = "*.xml|*.xml" };
            bool? result = outputFileDialog.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                Debug.WriteLine("No output file specified");
                return;
            }

            string inputXmlFile = outputFileDialog.FileName;
            string outputTlkFile = Path.ChangeExtension(inputXmlFile, "tlk");
            try
            {
                LegendaryExplorerCore.TLK.ME2ME3.HuffmanCompression hc =
                    new LegendaryExplorerCore.TLK.ME2ME3.HuffmanCompression();
                hc.LoadInputData(inputXmlFile, true);
                hc.SaveToFile(outputTlkFile);
                MessageBox.Show("Done.");
            }
            catch
            {
                MessageBox.Show("Unable to create test TLK file.");
            }
        }

        public static void UpdateLocalFunctions(PackageEditorWindow pew)
        {
            if (pew.TryGetSelectedExport(out var export) && ObjectBinary.From(export) is UStruct uStruct)
            {
                uStruct.UpdateChildrenChain(relinkChildrenStructs: false);
                if (uStruct is UClass uClass)
                {
                    uClass.UpdateLocalFunctions();
                }
                export.WriteBinary(uStruct);
            }
        }

        public static void DumpTOC()
        {
            Microsoft.Win32.OpenFileDialog outputFileDialog = new () { 
                Title = "Select TOC File", 
                Filter = "*.bin|*.bin" };
            bool? result = outputFileDialog.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                Debug.WriteLine("No output file specified");
                return;
            }
            string inputFile = outputFileDialog.FileName;
            string outputFile = Path.ChangeExtension(inputFile, "txt");

            var toc = new TOCBinFile(inputFile);
            toc.DumpTOCToTxtFile(outputFile);
        }

        public static void ExportMorphFace(PackageEditorWindow pew)
        {
            if (pew.TryGetSelectedExport(out var export) && export.ClassName == "BioMorphFace")
            {
                if (UModelHelper.GetLocalUModelVersion() < UModelHelper.SupportedUModelBuildNum)
                {
                    MessageBox.Show("UModel not installed or incorrect version!");
                    return;
                }
                pew.IsBusy = true;
                pew.BusyText = "Applying MorphFace to head mesh...";
                var morphFace = new BioMorphFace(export);
                var rop = new RelinkerOptionsPackage();

                // Create a new file containing only the headmesh
                var tempFilePath = Path.Combine(Path.GetTempPath(), "HeadMeshExport.pcc");
                if(File.Exists(tempFilePath)) File.Delete(tempFilePath);
                MEPackageHandler.CreateAndSavePackage(tempFilePath, export.Game);
                using var tempFile = MEPackageHandler.OpenMEPackage(tempFilePath);
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies,
                    morphFace.m_oBaseHead, tempFile, null, true, rop, out var clonedHeadEntry);
                var clonedHead = clonedHeadEntry as ExportEntry;
                var appliedHead = morphFace.Apply();

                // Clone materials
                for (var i = 0; i < appliedHead.Materials.Length; i++)
                {
                    var originalMat = export.FileRef.GetEntry(appliedHead.Materials[i].value);
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies,
                        originalMat, tempFile, null, true, rop, out var clonedMat);
                    appliedHead.Materials[i] = new UIndex(clonedMat.UIndex);
                }
                clonedHead.WriteBinary(appliedHead);
                clonedHead.ObjectName = new NameReference(export.ObjectNameString);
                tempFile.Save();

                // Ensure UModel
                // Pass error message back
                Task.Run(() =>
                {
                    return UModelHelper.EnsureUModel(
                        () => pew.IsBusy = true,
                        null,
                        null,
                        busyText => pew.BusyText = busyText
                    );
                }).ContinueWithOnUIThread(x =>
                {
                    // Export the cloned headmesh
                    if (x != null)
                    {
                        MessageBox.Show($"Couldn't export via umodel: {x.Result}");
                    }
                    else
                    {
                        pew.BusyText = "Exporting via UModel...";
                        UModelHelper.ExportViaUModel(pew, clonedHead);
                        //File.Delete(tempFilePath);
                    }
                    pew.IsBusy = false;
                });
                
            }
            else
            {
                MessageBox.Show("Must have 'BioMorphFace' export selected in the tree view.");
            }
        }

        public static void LE1Elevator(PackageEditorWindow getPeWindow)
        {
            IMEPackage Pcc = getPeWindow.Pcc;
            var convo = Pcc.FindExport("sp_news_vids_D.sp_news_vids_dlg");
            if (convo.ClassName != "BioConversation") return;
            var props = convo.GetProperties();
            var entries = props.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
            var one = entries[123];
            var two = entries[125];
            var three = entries[127];
            one.Properties.AddOrReplaceProp(new IntProperty(6291, "nConditionalFunc"));
            one.Properties.AddOrReplaceProp(new IntProperty(1, "nConditionalParam"));
            one.Properties.AddOrReplaceProp(new BoolProperty(false, "bFireConditional"));

            two.Properties.AddOrReplaceProp(new IntProperty(6290, "nConditionalFunc"));
            two.Properties.AddOrReplaceProp(new IntProperty(1, "nConditionalParam"));
            two.Properties.AddOrReplaceProp(new BoolProperty(false, "bFireConditional"));

            three.Properties.AddOrReplaceProp(new IntProperty(6289, "nConditionalFunc"));
            three.Properties.AddOrReplaceProp(new IntProperty(1, "nConditionalParam"));
            three.Properties.AddOrReplaceProp(new BoolProperty(false, "bFireConditional"));

            entries[123] = one;
            entries[125] = two;
            entries[127] = three;
            props.AddOrReplaceProp(entries);
            convo.WriteProperties(props);
        }

        public static void FixPinkVisor(PackageEditorWindow getPeWindow)
        {
            string searchDir = ME1Directory.DLCPath;
            CommonOpenFileDialog d = new CommonOpenFileDialog
                {Title = "Select folder to search", IsFolderPicker = true};
            if (d.ShowDialog() == CommonFileDialogResult.Ok)
            {
                searchDir = d.FileName;
            }
            else return;

            FileInfo[] files = new DirectoryInfo(searchDir)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(f => f.Extension.ToLower() == ".pcc")
                .ToArray();

            var colors = new Dictionary<float, float[]>();
            colors.Add(0.20784314f, new[] {0.12941177f, 0.13725491f, 0.16078432f, 1f}); // grey/corpsec
            colors.Add(0.003921569f, new[] {0.023529412f, 0.039215688f, 0.019607844f, 1f}); // green/cerberus
            colors.Add(0.32156864f, new[] {0.6f, 0.5294118f, 0.33333334f, 1f}); // cream
            colors.Add(0.050980393f, new[] {0.039215688f, 0.039215688f, 0.019607844f, 1f}); // camo


            foreach (var file in files)
            {
                using var pcc = MEPackageHandler.OpenMEPackage(file.FullName);
                var matInstanceConstants = pcc.Exports.Where((e) =>
                    e.ClassName == "MaterialInstanceConstant" &&
                    e.ObjectName.ToString().StartsWith("HMM_BRT_HVYa_MAT"));
                foreach (var matInstanceConstant in matInstanceConstants)
                {
                    var matProps = matInstanceConstant.GetProperties();
                    var vectors = matProps.GetProp<ArrayProperty<StructProperty>>("VectorParameterValues");
                    foreach (var structProp in vectors.Values)
                    {
                        if (structProp.GetProp<NameProperty>("ParameterName").Value == "HGR_Colour_01")
                        {
                            var linearColor = structProp.Properties.GetProp<StructProperty>("ParameterValue");

                            var oldR = linearColor.Properties.GetProp<FloatProperty>("R").Value;
                            float key = colors.Keys.FirstOrDefault(f => Math.Abs(f - oldR) < 0.0001);
                            if (colors.TryGetValue(key, out var newColors))
                            {
                                linearColor.Properties.AddOrReplaceProp(new FloatProperty(newColors[0], "R"));
                                linearColor.Properties.AddOrReplaceProp(new FloatProperty(newColors[1], "G"));
                                linearColor.Properties.AddOrReplaceProp(new FloatProperty(newColors[2], "B"));
                                linearColor.Properties.AddOrReplaceProp(new FloatProperty(newColors[3], "A"));
                                structProp.Properties.AddOrReplaceProp(linearColor);
                            }

                        }
                    }

                    matProps.AddOrReplaceProp(vectors);
                    matInstanceConstant.WriteProperties(matProps);
                }

                pcc.Save();
            }
        }

        public static void MakeLocalizedTLK(PackageEditorWindow pew = null)
        {
            var locales = new Dictionary<string, string>();
            locales.Add("Startup_DE.pcc", "DLC_MOD_LE1CP_GlobalTlk_DE.pcc");
            locales.Add("Startup_ES.pcc", "DLC_MOD_LE1CP_GlobalTlk_ES.pcc");
            locales.Add("Startup_FR.pcc", "DLC_MOD_LE1CP_GlobalTlk_FR.pcc");
            locales.Add("Startup_IT.pcc", "DLC_MOD_LE1CP_GlobalTlk_IT.pcc");
            locales.Add("Startup_JA.pcc", "DLC_MOD_LE1CP_GlobalTlk_JPN.pcc");
            locales.Add("Startup_PLPC.pcc", "DLC_MOD_LE1CP_GlobalTlk_PLPC.pcc");
            locales.Add("Startup_RA.pcc", "DLC_MOD_LE1CP_GlobalTlk_RA.pcc");

            string mod =
                @"D:\Mass Effect Modding\ME3TweaksModManager\mods\LE1\LE1 Community Patch\DLC_MOD_LE1CP\CookedPCConsole";

            string xmlFilePath = @"D:\Mass Effect Modding\My Mods\LE1_CP\localized.xml";

            var xmlDoc = new XmlDocument();
            var stringIds = new List<int>();
            xmlDoc.Load(xmlFilePath);
            var children = xmlDoc.DocumentElement?.ChildNodes;
            foreach (XmlNode c in children)
            {
                var strid = c.FirstChild?.InnerText;
                if (int.TryParse(strid, out int id))
                {
                    stringIds.Add(id);
                }
            }

            foreach (var kvp in locales)
            {
                using IMEPackage startup =
                    MEPackageHandler.OpenMEPackage(Path.Combine(LE1Directory.CookedPCPath, kvp.Key));
                using IMEPackage global =
                    MEPackageHandler.OpenMEPackage(Path.Combine(mod, kvp.Value));

                var startupF = new ME1TalkFile((ExportEntry) startup.GetEntry(3));
                var startupM = new ME1TalkFile((ExportEntry) startup.GetEntry(4));

                var globalF = (ExportEntry) global.GetEntry(1);
                var stringsF = new List<TLKStringRef>();
                var globalM = (ExportEntry) global.GetEntry(2);
                var stringsM = new List<TLKStringRef>();

                foreach (var id in stringIds)
                {
                    if (id >= 200000) continue;
                    var fStr = startupF.FindDataById(id);
                    var mStr = startupM.FindDataById(id);
                    fStr = fStr.Substring(1, fStr.Length - 2);
                    mStr = mStr.Substring(1, mStr.Length - 2);
                    stringsF.Add(new TLKStringRef(id, fStr));
                    stringsM.Add(new TLKStringRef(id, mStr));
                }

                HuffmanCompression hcf = new HuffmanCompression();
                HuffmanCompression hcm = new HuffmanCompression();

                hcf.LoadInputData(stringsF);
                hcm.LoadInputData(stringsM);
                hcf.SerializeTalkfileToExport(globalF);
                hcm.SerializeTalkfileToExport(globalM);
                global.Save();
            }


        }

        public static void FindUncapitalizedTargetText(PackageEditorWindow pew = null)
        {
            pew.IsBusy = true;
            var i = 1;
            Dictionary<int, string> badOnes = new();
            Task.Run(() =>
            {
                string searchDir = LE1Directory.CookedPCPath;
                FileInfo[] files = new DirectoryInfo(searchDir)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Where(f => f.Extension.ToLower() == ".pcc")
                    .ToArray();

                void AddToBadOnes(int strRef)
                {
                    string str = TlkManagerNS.TLKManagerWPF.GlobalFindStrRefbyID(strRef, MEGame.LE1);
                    if (strRef != 0 && str is not null && str != "No Data")
                    {
                        if (str.Split(" ").Any(w => w[0] != w.ToUpperInvariant()[0]))
                        {
                            badOnes.TryAdd(strRef, str);
                        }
                    }
                }

                foreach (var file in files)
                {
                    using var pcc = MEPackageHandler.OpenMEPackage(file.FullName);
                    pew.BusyText = $"[{i}/{files.Length}] Scanning Packages for capitalized text";
                    HashSet<string> classes = new HashSet<string>()
                    {
                        "BioArtPlaceableInertType", "BioArtPlaceableUseableType", "BioArtPlaceableBehavior",
                        "BioSeqAct_ModifyPropertyPawn", "BioSeqAct_ModifyPropertyArtPlaceable"
                    };
                    var potentialExports = pcc.Exports.Where(e => classes.Contains(e.ClassName));
                    foreach (var export in potentialExports)
                    {
                        int targetTipStrRef = 0;
                        string targetTip = null;
                        if (export.GetProperty<StringRefProperty>("m_nTargetTipTextOverridden") is { } prop)
                        {
                            AddToBadOnes(prop.Value);
                        }
                        else if (export.GetProperty<StringRefProperty>("m_nTargetTipText") is { } prop2)
                        {
                            AddToBadOnes(prop2.Value);
                        }
                        else if (export.GetProperty<StringRefProperty>("ActorGameNameStrRef") is { } prop3)
                        {
                            AddToBadOnes(prop3.Value);
                        }
                        else if (export.ClassName == "BioSeqAct_ModifyPropertyPawn" ||
                                 export.ClassName == "BioSeqAct_ModifyPropertyArtPlaceable")
                        {
                            var variableLinks = SeqTools.GetVariableLinksOfNode(export);
                            var link = variableLinks.FirstOrDefault(e => e.LinkDesc == "m_nTargetTipTextOverridden");
                            if (link is not null)
                            {
                                var tgtOverride = ((ExportEntry) link.LinkedNodes[0])
                                    .GetProperty<StringRefProperty>("m_srValue")?.Value ?? 0;
                                AddToBadOnes(tgtOverride);
                            }

                            link = variableLinks.FirstOrDefault(e => e.LinkDesc == "ActorGameNameStrRef");
                            if (link is not null)
                            {
                                var tgtOverride = ((ExportEntry) link.LinkedNodes[0])
                                    .GetProperty<StringRefProperty>("m_srValue")?.Value ?? 0;
                                AddToBadOnes(tgtOverride);
                            }
                        }
                    }

                    i++;
                }

                using StreamWriter outputFile = new(@"D:\Mass Effect Modding\My Mods\LE1 Community Patch\TextFix.txt");
                foreach (var kvp in badOnes)
                {
                    outputFile.WriteLine($"{kvp.Key}: {kvp.Value}");
                }
            }).ContinueWithOnUIThread(_ =>
            {
                pew.IsBusy = false;
                pew.BusyText = $"Done.";
            });
        }

        /// <summary>
        /// Copies all DecalActors that project onto a certain StaticMeshComponent to another file to project onto another SMC
        /// </summary>
        /// <param name="getPeWindow"></param>
        public static void CopyDecalActors(PackageEditorWindow getPeWindow)
        {
            Microsoft.Win32.OpenFileDialog outputFileDialog = new () {
                Title = "Select file to copy DecalActors to",
                Filter = "*.pcc|*.pcc" };
            bool? result = outputFileDialog.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                Debug.WriteLine("No output file specified");
                return;
            }
            string outputFilePath = outputFileDialog.FileName;
            int smcSourceUindex = 0;
            if (PromptDialog.Prompt(null, "Enter Source StaticMeshComponent UIndex") is string smcSourceStr)
            {
                if (string.IsNullOrEmpty(smcSourceStr) || !int.TryParse(smcSourceStr, out var smcSrcId))
                {
                    MessageBox.Show("Wrong", "Warning", MessageBoxButton.OK);
                    return;
                }
                smcSourceUindex = smcSrcId;
            }
            int smcTargetUindex = 0;
            if (PromptDialog.Prompt(null, "Enter Target StaticMeshComponent UIndex") is string smcTargetStr)
            {
                if (string.IsNullOrEmpty(smcTargetStr) || !int.TryParse(smcTargetStr, out var smcTgtId))
                {
                    MessageBox.Show("Wrong", "Warning", MessageBoxButton.OK);
                    return;
                }
                smcTargetUindex = smcTgtId;
            }

            using IMEPackage o = MEPackageHandler.OpenMEPackage(outputFilePath);
            IEntry smaTarget = o.GetEntry(smcTargetUindex).Parent;

            foreach (var decalComponent in getPeWindow.Pcc.Exports.Where(c => c.ClassName == "DecalComponent"))
            {
                // Check this DecalComponent contains the decal we're looking for, if not continue
                var receivers = decalComponent.GetProperty<ArrayProperty<StructProperty>>("DecalReceivers")?.Values ?? new List<StructProperty>();
                if (receivers.All(property => property.GetProp<ObjectProperty>("Component").Value != smcSourceUindex)) continue;

                // Bad hack because the reindexer isn't working - don't do this
                decalComponent.Parent.ObjectName = new NameReference(decalComponent.Parent.ObjectName.Name,
                    decalComponent.Parent.ObjectName.Number + 300);

                // Import the Decal tree into the new file
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, decalComponent.Parent, o,
                    o.FindEntry("TheWorld.PersistentLevel"), true, new RelinkerOptionsPackage(),out IEntry clonedDecalEntry);
                ExportEntry newDecalComponent = clonedDecalEntry.GetChildren().FirstOrDefault(e => e.ClassName == "DecalComponent") as ExportEntry;
                o.AddToLevelActorsIfNotThere(decalComponent.Parent as ExportEntry);

                // Add the DecalReceivers property with our target SMC
                var props = newDecalComponent.GetProperties();
                props.AddOrReplaceProp(new ArrayProperty<StructProperty>(new List<StructProperty>()
                {
                    new StructProperty("DecalReceiver", false, new ObjectProperty(smcTargetUindex, "Component"))
                }, "DecalReceivers"));

                // Add the Filter array with our SMA, and set the filter mode
                props.AddOrReplaceProp(new ArrayProperty<ObjectProperty>( new List<ObjectProperty>()
                {
                    new ObjectProperty(smaTarget.UIndex)
                }, "Filter"));
                props.AddOrReplaceProp(new EnumProperty("FM_Affect", "EFilterMode", MEGame.LE1, "FilterMode"));

                // Remove the other static receivers from the binary
                var binary = ObjectBinary.From<DecalComponent>(newDecalComponent);
                var targetStaticReceiver =
                    binary.StaticReceivers.FirstOrDefault(t => t.PrimitiveComponent == smcSourceUindex);
                if(targetStaticReceiver is not null) targetStaticReceiver.PrimitiveComponent = smcTargetUindex;
                binary.StaticReceivers = targetStaticReceiver is null ? new StaticReceiverData[] { } : new[] {targetStaticReceiver};
                newDecalComponent.WritePropertiesAndBinary(props, binary);
            }
        }

        public static void MakoBanter(PackageEditorWindow pew)
        {
            var pcc = pew.Pcc;
            var cache = new PackageCache();
            var banterRootSeq = pcc?.FindExport("TheWorld.PersistentLevel.Main_Sequence.RunCompanionBanter");
            if (pcc is null || banterRootSeq is null) return;

            foreach (var banterSequenceEntry in banterRootSeq.GetAllDescendants().Where(i =>
                         i is ExportEntry {ClassName: "Sequence"} exp && exp.ObjectNameString.StartsWith("Play_")))
            {
                var banterSequence = banterSequenceEntry as ExportEntry;
                var makoObject = SequenceObjectCreator.CreateSequenceObject(pcc, "BioSeqVar_ObjectFindByTag");
                var ownerHench = GetHenchFromSeqName(banterSequence.ObjectName);
                var sequenceObjects = KismetHelper.GetSequenceObjects(banterSequence).OfType<ExportEntry>().ToList();
                if(string.IsNullOrEmpty(ownerHench)) continue;

                makoObject.WriteProperty(new StrProperty("UNC_DefaultRover", "m_sObjectTagToFind"));
                KismetHelper.AddObjectToSequence(makoObject, banterSequence);


                foreach (var fovo in banterSequence.GetChildren<ExportEntry>()
                             .Where(e => e.ClassName == "BioSeqAct_FaceOnlyVO"))
                {
                    var fovoProps = fovo.GetProperties();
                    var henchTag = fovoProps.GetProp<NameProperty>("m_nmSpeakerTag").Value.Name;

                    // Add actor name override
                    fovoProps.AddOrReplaceProp(new StringRefProperty(GetHenchStrRef(henchTag), "m_srActorNameOverride"));

                    // Create pawn variable link, link to Mako
                    var seqVarLink = new PropertyCollection()
                    {
                        new ArrayProperty<ObjectProperty>("LinkedVariables")
                        {
                            new ObjectProperty(makoObject)
                        },
                        new StrProperty("Pawn", "LinkDesc"),
                        new ObjectProperty(pcc.FindImport("Engine.SeqVar_Object"), "ExpectedType"),
                        new NameProperty("None", "LinkVar"),
                        new NameProperty("Targets", "PropertyName"),
                        new IntProperty(1, "MinVars"),
                        new IntProperty(1, "MaxVars"),
                        new BoolProperty(false, "bWriteable"),
                        new BoolProperty(false, "bModifiesLinkedObject"),
                        new BoolProperty(false, "bAllowAnyType")
                    };
                    fovoProps.AddOrReplaceProp(new ArrayProperty<StructProperty>("VariableLinks") { new StructProperty("SeqVarLink", seqVarLink)});
                    fovo.WriteProperties(fovoProps);

                    // If this FOVO belongs to a hench who doesn't own the dialogue, create a PM_CheckState to make sure
                    // they are in the party first. The outer sequence already checks if the owner is in the party before running the sequence.
                    if (henchTag != ownerHench)
                    {
                        var newCond = MakeHenchInPartyCheckState(fovo, banterSequence, sequenceObjects, henchTag);
                    }
                }

                // FOVOs will still have "Failed" out links to use the conversation in squad system.
                // Change all the "Failed" out links to their respective CheckState False links, where applicable
                foreach (var checkState in banterSequence.GetChildren<ExportEntry>()
                             .Where(e => e.ClassName == "BioSeqAct_PMCheckState"))
                {
                    var stateLinks = SeqTools.GetOutboundLinksOfNode(checkState);
                    var fovo = stateLinks[0][0].LinkedOp as ExportEntry;
                    if (fovo is null) return;
                    var fovoLinks = SeqTools.GetOutboundLinksOfNode(fovo);
                    if (fovoLinks.Count >= 2)
                    {
                        foreach (var fail in fovoLinks[1])
                        {
                            if (fail.LinkedOp is ExportEntry failExp)
                            {
                                KismetHelper.CreateOutputLink(checkState, "False", failExp);
                            }
                        }
                        fovoLinks[1].Clear();
                    }
                    SeqTools.WriteOutboundLinksToNode(fovo, fovoLinks);
                }
            }

            ExportEntry MakeHenchInPartyCheckState(ExportEntry fovo, ExportEntry sequence, IEnumerable<ExportEntry> elements, string henchTag)
            {
                var inLinks = SeqTools.FindOutboundConnectionsToNode(fovo, elements);
                var newCheck = SequenceObjectCreator.CreateSequenceObject(fovo.FileRef, "BioSeqAct_PMCheckState", cache);
                KismetHelper.AddObjectToSequence(newCheck, sequence);
                KismetHelper.CreateOutputLink(newCheck, "True", fovo);
                KismetHelper.SetComment(newCheck, $"{henchTag} in party?");
                newCheck.WriteProperty(new IntProperty(GetHenchBool(henchTag), "m_nIndex"));
                foreach (var inLink in inLinks)
                {
                    var toLinks = SeqTools.GetOutboundLinksOfNode(inLink);
                    foreach (var port in toLinks)
                    {
                        foreach (var link in port)
                        {
                            if (link.LinkedOp.UIndex == fovo.UIndex)
                            {
                                link.LinkedOp = newCheck;
                            }
                        }
                    }
                    SeqTools.WriteOutboundLinksToNode(inLink, toLinks);
                }
                return newCheck;
            }

            string GetHenchFromSeqName(string seqName)
            {
                if (seqName.Contains("Kaidan")) return "hench_humanmale";
                else if (seqName.Contains("Ashley")) return "hench_humanfemale";
                else if (seqName.Contains("Garrus")) return "hench_turian";
                else if (seqName.Contains("Wrex")) return "hench_krogan";
                else if (seqName.Contains("Tali")) return "hench_quarian";
                else if (seqName.Contains("Liara")) return "hench_asari";
                else return null;
            }

            int GetHenchBool(string tag) => tag switch
            {
                "hench_humanmale" => 3817,
                "hench_humanfemale" => 3818,
                "hench_turian" => 3819,
                "hench_krogan" => 3820,
                "hench_asari" => 3821,
                "hench_quarian" => 3822
            };

            int GetHenchStrRef(string tag) => tag switch
            {
                "hench_humanmale" => 164532,
                "hench_humanfemale" => 164530,
                "hench_turian" => 165839,
                "hench_krogan" => 164529,
                "hench_asari" => 149285,
                "hench_quarian" => 164533
            };
        }

        public static void FixAshKaidanConvos(PackageEditorWindow pew)
        {
            IMEPackage Pcc = pew.Pcc;
            bool isAsh = false;
            ExportEntry convo = Pcc.FindExport("cha00_humanmale_kaidan_d.cha00_humanmale_kaidan_dlg");
            if (convo is null)
            {
                convo = Pcc.FindExport("cha00_humanfemale_ashley_d.cha00_humanfemale_ashley_dlg");
                isAsh = true;
            }

            HashSet<int> badEntries = GetBadNodes(false, isAsh);
            HashSet<int> badReplies =GetBadNodes(true, isAsh);
            if (convo is null || convo.ClassName != "BioConversation") return;
            var props = convo.GetProperties();
            var entries = props.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
            var replies = props.GetProp<ArrayProperty<StructProperty>>("m_ReplyList");

            foreach (var reply in replies)
            {
                var entryList = reply.GetProp<ArrayProperty<IntProperty>>("EntryList");
                var newEntryList = entryList.Where((prop) => !badEntries.Contains(prop.Value)).ToList();
                if (newEntryList.Count != entryList.Count)
                {
                    reply.Properties.AddOrReplaceProp(new ArrayProperty<IntProperty>(newEntryList, "EntryList"));
                }
            }
            foreach (var entry in entries)
            {
                var entryList = entry.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                var newEntryList = entryList.Where((prop) => !badReplies.Contains(prop.GetProp<IntProperty>("nIndex")?.Value ?? 0)).ToList();
                if (newEntryList.Count != entryList.Count)
                {
                    entry.Properties.AddOrReplaceProp(new ArrayProperty<StructProperty>(newEntryList, "ReplyListNew"));
                }
            }
            props.AddOrReplaceProp(entries);
            props.AddOrReplaceProp(replies);
            convo.WriteProperties(props);

            HashSet<int> GetBadNodes(bool isReplies, bool isAsh) => (isReplies, isAsh) switch
            {
                (true, true) => new HashSet<int>(new[] {85, 357}),
                (false, true) => new HashSet<int>(new[] {88, 172, 175, 184, 245, 286, 380}),
                (true, false) => new HashSet<int>(new[] {104, 108}),
                (false, false) => new HashSet<int>(new[] {109, 136, 266}),
            };
        }
    }
}