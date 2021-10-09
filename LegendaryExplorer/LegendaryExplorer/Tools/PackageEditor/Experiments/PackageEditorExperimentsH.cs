using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    static internal class PackageEditorExperimentsH
    {
        /// <summary>
        /// Collects all TLK exports from the entire ME1 game and exports them into a single GlobalTLK file
        /// </summary>
        /// <param name="pew">Instance of Package Editor</param>
        public static void BuildME1SuperTLKFile (PackageEditorWindow pew)
        {
            string myBasePath = ME1Directory.DefaultGamePath;
            string searchDir = ME1Directory.CookedPCPath;

            CommonOpenFileDialog d = new CommonOpenFileDialog { Title = "Select folder to search", IsFolderPicker = true, InitialDirectory = myBasePath };
            if (d.ShowDialog() == CommonFileDialogResult.Ok)
            {
                searchDir = d.FileName;
            }

            Microsoft.Win32.OpenFileDialog outputFileDialog = new () { 
                Title = "Select GlobalTlk file to output to (GlobalTlk exports will be completely overwritten)", 
                Filter = "*.upk|*.upk" };
            bool? result = outputFileDialog.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                Debug.WriteLine("No output file specified");
                return;
            }
            string outputFilePath = outputFileDialog.FileName;

            string[] extensions = { ".u", ".upk" };

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
                            var LoadedStrings = new List<ME1TalkFile.TLKStringRef>();
                            foreach (var tlkString in stringMapping)
                            {
                                // Do the important part
                                LoadedStrings.Add(new ME1TalkFile.TLKStringRef(tlkString.Key, 1, tlkString.Value));
                            }

                            HuffmanCompression huff = new HuffmanCompression();
                            huff.LoadInputData(LoadedStrings);
                            huff.serializeTalkfileToExport(exp);
                        }
                    }
                    o.Save();

                }

                return total;

            }).ContinueWithOnUIThread((total) =>
            {
                pew.IsBusy = false;
                pew.StatusBar_LeftMostText.Text = $"Wrote {total} lines to {outputFilePath}";
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
                if (receivers.All(property => property.GetPropOrDefault<ObjectProperty>("Component").Value != smcSourceUindex)) continue;

                // Bad hack because the reindexer isn't working - don't do this
                decalComponent.Parent.ObjectName = new NameReference(decalComponent.Parent.ObjectName.Name,
                    decalComponent.Parent.ObjectName.Number + 300);

                // Import the Decal tree into the new file
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, decalComponent.Parent, o,
                    o.FindEntry("TheWorld.PersistentLevel"), true, out IEntry clonedDecalEntry);
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
    }
}