using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using System.Xml;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using HuffmanCompression = LegendaryExplorerCore.TLK.ME1.HuffmanCompression;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    /// <summary>
    /// Class for 'Others' package experiments who aren't main devs
    /// </summary>
    class PackageEditorExperimentsO
    {
        public static void DumpPackageToT3D(IMEPackage package)
        {
            var levelExport =
                package.Exports.FirstOrDefault(x => x.ObjectName == "Level" && x.ClassName == "PersistentLevel");
            if (levelExport != null)
            {
                var level = ObjectBinary.From<Level>(levelExport);
                foreach (var actoruindex in level.Actors)
                {
                    if (package.TryGetUExport(actoruindex.value, out var actorExport))
                    {
                        switch (actorExport.ClassName)
                        {
                            case "StaticMesh":
                                var sm = ObjectBinary.From<StaticMesh>(actorExport);

                                // Look at vars in sm to find what you need
                                //ExportT3D(sm, "FILENAMEHERE.txt", null); //??
                                break;
                            case "StaticMeshCollectionActor":

                                break;
                        }

                    }
                }
            }
        }

        // Might already be in ME3EXP?
        //A function for converting radians to unreal rotation units (necessary for UDK)
        private static float RadianToUnrealDegrees(float Angle)
        {
            return Angle * (32768 / 3.1415f);
        }

        // Might already be in ME3EXP?
        //A function for converting radians to degrees
        private static float RadianToDegrees(float Angle)
        {
            return Angle * (180 / 3.1415f);
        }

        public static void ExportT3D(StaticMesh staticMesh, string Filename, Matrix4x4 m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3((float)Math.Atan2(m.M32, m.M33), (float)Math.Asin(-1 * m.M31),
                (float)Math.Atan2(-1 * m.M21, m.M11));
            float RotatorX = Rotator.X;
            RotatorX = RadianToDegrees(RotatorX);
            float RotatorY = Rotator.Y;
            RotatorY = RadianToDegrees(RotatorY);
            float RotatorZ = Rotator.Z;
            RotatorZ = RadianToDegrees(RotatorZ);

            Vector3 Location = new Vector3(m.M41, m.M42, m.M43);

            //Only rotation, location, scale, actor name and model name are needed for a level recreation, everything else is just a placeholder
            //Need to override ToString to use US CultureInfo to avoid "commas instead of dots" bug
            //Indexes here is just to make names unique
            if (staticMesh != null)
            {
                Writer.WriteLine(
                    $"Begin Actor Class=StaticMeshActor Name={staticMesh.Export.ObjectName} Archetype=StaticMeshActor'/Script/Engine.Default__StaticMeshActor'");
                Writer.WriteLine(
                    "        Begin Object Class=StaticMeshComponent Name=\"StaticMeshComponent0\" Archetype=StaticMeshComponent'/Script/Engine.Default__StaticMeshActor:StaticMeshComponent0'");
                Writer.WriteLine("        End Object");
                Writer.WriteLine("        Begin Object Name=\"StaticMeshComponent0\"");
                Writer.WriteLine("            StaticMesh=StaticMesh'/Game/ME3/ME3Architecture/Static/" +
                                 staticMesh.Export.ObjectName + "." + staticMesh.Export.ObjectName +
                                 "'"); //oriignal code was duplicated
                Writer.WriteLine("            RelativeLocation=(X=" +
                                 Location.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 "," +
                                 "Y=" + Location.Y.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                                 "Z=" + Location.Z.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("            RelativeRotation=(Pitch=" +
                                 RotatorY.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 "," +
                                 "Yaw=" + RotatorZ.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                                 "Roll=" + RotatorX.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("            RelativeScale3D=(X=" +
                                 IncScale3D.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 "," +
                                 "Y=" + IncScale3D.Y.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                                 "Z=" + IncScale3D.Z.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("        End Object");
                Writer.WriteLine("        StaticMeshComponent=StaticMeshComponent0");
                Writer.WriteLine($"        ActorLabel=\"{staticMesh.Export.ObjectName}\"");
                Writer.WriteLine("End Actor");
            }

            Writer.Close();
        }


        //UDK version, need to figure out how to apply rotation properly
        public static void ExportT3D_UDK(StaticMesh STM, string Filename, Matrix4x4 m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3((float)Math.Atan2(m.M32, m.M33), (float)Math.Asin(-1 * m.M31),
                (float)Math.Atan2(-1 * m.M21, m.M11));
            float RotatorX = Rotator.X;
            RotatorX = RadianToUnrealDegrees(RotatorX);
            float RotatorY = Rotator.Y;
            RotatorY = RadianToUnrealDegrees(RotatorY);
            float RotatorZ = Rotator.Z;
            RotatorZ = RadianToUnrealDegrees(RotatorZ);

            Vector3 Location = new Vector3(m.M41, m.M42, m.M43);

            if (STM != null)
            {
                Writer.WriteLine("      Begin Actor Class=StaticMeshActor Name=STMC_" + STM.Export.ObjectName.Number +
                                 " Archetype=StaticMeshActor'Engine.Default__StaticMeshActor'");
                Writer.WriteLine("          Begin Object Class=StaticMeshComponent Name=STMC_" +
                                 STM.Export.ObjectName.Number + " ObjName=" + STM.Export.ObjectName.Instanced +
                                 " Archetype=StaticMeshComponent'Engine.Default__StaticMeshActor:StaticMeshComponent0'");
                Writer.WriteLine(
                    "              StaticMesh=StaticMesh'A_Cathedral.Static." + STM.Export.ObjectName + "'");
                Writer.WriteLine("              LODData(0)=");
                Writer.WriteLine("              VertexPositionVersionNumber=1");
                Writer.WriteLine("              ReplacementPrimitive=None");
                Writer.WriteLine("              bAllowApproximateOcclusion=True");
                Writer.WriteLine("              bForceDirectLightMap=True");
                Writer.WriteLine("              bUsePrecomputedShadows=True");
                Writer.WriteLine("              LightingChannels=(bInitialized=True,Static=True)");
                Writer.WriteLine("              Name=\"" + STM.Export.ObjectName + "_" + STM.Export.ObjectName.Number +
                                 "\"");
                Writer.WriteLine(
                    "              ObjectArchetype=StaticMeshComponent'Engine.Default__StaticMeshActor:StaticMeshComponent0'");
                Writer.WriteLine("          End Object");
                Writer.WriteLine("          StaticMeshComponent=StaticMeshComponent'" +
                                 STM.Export.ObjectName.Instanced + "'");
                Writer.WriteLine("          Components(0)=StaticMeshComponent'" + STM.Export.ObjectName.Instanced +
                                 "'");
                Writer.WriteLine("          Location=(X=" +
                                 Location.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 "," +
                                 "Y=" + Location.Y.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                                 "Z=" + Location.Z.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("          Rotation=(Pitch=" +
                                 RotatorY.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 "Yaw=" + RotatorZ.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                                 "Roll=" + RotatorX.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("          DrawScale=(X=" +
                                 IncScale3D.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 "," +
                                 "Y=" + IncScale3D.Y.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                                 "Z=" + IncScale3D.Z.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("          CreationTime=1.462282");
                Writer.WriteLine("          Tag=\"StaticMeshActor\"");
                Writer.WriteLine("          CollisionComponent=StaticMeshComponent'" + STM.Export.ObjectName + "'");
                Writer.WriteLine("          Name=\"STMC_" + STM.Export.ObjectName.Number.ToString("D") + "\"");
                Writer.WriteLine("          ObjectArchetype=StaticMeshActor'Engine.Default__StaticMeshActor'");
                Writer.WriteLine("      End Actor");
            }

            Writer.Close();
        }



        //an attempt to recreate the assembling process in MaxScript similar to unreal t3d
        //Rotation is buggy, doesn't properly for now
        public static void ExportT3D_MS(StaticMesh STM, string Filename, Matrix4x4 m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3((float)Math.Atan2(m.M32, m.M33), (float)Math.Asin(-1 * m.M31),
                (float)Math.Atan2(-1 * m.M21, m.M11));
            float RotatorX = Rotator.X;
            RotatorX = RadianToDegrees(RotatorX);
            float RotatorY = Rotator.Y;
            RotatorY = RadianToDegrees(RotatorY);
            float RotatorZ = Rotator.Z;
            RotatorZ = RadianToDegrees(RotatorZ);

            Vector3 Location = new Vector3(m.M41, m.M42, m.M43);

            if (STM != null)
            {
                Writer.WriteLine($"{STM.Export.ObjectName} = instance ${STM.Export.ObjectName}");
                Writer.WriteLine(
                    $"{STM.Export.ObjectName}.name = \"{STM.Export.ObjectName}\" --name the copy as \"{STM.Export.ObjectName}\"");
                Writer.WriteLine("$" + STM.Export.ObjectName + ".Position=[" +
                                 Location.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 ", " + Location.Y.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 ", " + Location.Z.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "]");
                Writer.WriteLine("$" + STM.Export.ObjectName + ".scale=[" +
                                 IncScale3D.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 ", " + IncScale3D.Y.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 ", " + IncScale3D.Z.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "]");
                Writer.WriteLine("--Setting the rotation");
                Writer.WriteLine("fn SetObjectRotation obj rx ry rz =");
                Writer.WriteLine("(");
                Writer.WriteLine("-- Reset the object's transformation matrix so that");
                Writer.WriteLine("-- it only includes position and scale information.");
                Writer.WriteLine("-- Doing this clears out any previous object rotation.");
                Writer.WriteLine("local translateMat = transMatrix obj.transform.pos");
                Writer.WriteLine("local scaleMat = scaleMatrix obj.transform.scale");
                Writer.WriteLine("obj.transform = scaleMat * translateMat");
                Writer.WriteLine("-- Perform each axis rotation individually");
                Writer.WriteLine("rotate obj (angleaxis rx [1,0,0])");
                Writer.WriteLine("rotate obj (angleaxis ry [0,1,0])");
                Writer.WriteLine("rotate obj (angleaxis rz [0,0,1])");
                Writer.WriteLine(")");
                Writer.WriteLine("-- Set currently selected Object's rotation to " +
                                 RotatorX.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 " " + RotatorY.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 " " + RotatorZ.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("SetObjectRotation $" + STM.Export.ObjectName +
                                 " " + RotatorX.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 " " + RotatorY.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                                 " " + RotatorZ.ToString("F3",
                                     System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("-------------------------------------------------------");
                Writer.WriteLine("-------------------------------------------------------");
            }

            Writer.Close();
        }

        /// <summary>
        /// Collects all TLK exports from the entire ME1 game and exports them into a single GlobalTLK file
        /// </summary>
        /// <param name="pew">Instance of Package Editor</param>
        public static void BuildME1SuperTLKFile(PackageEditorWindow pew)
        {
            string myBasePath = ME1Directory.DefaultGamePath;
            string searchDir = ME1Directory.CookedPCPath;

            CommonOpenFileDialog d = new CommonOpenFileDialog
                { Title = "Select folder to search", IsFolderPicker = true, InitialDirectory = myBasePath };
            if (d.ShowDialog() == CommonFileDialogResult.Ok)
            {
                searchDir = d.FileName;
            }

            Microsoft.Win32.OpenFileDialog outputFileDialog = new()
            {
                Title = "Select GlobalTlk file to output to (GlobalTlk exports will be completely overwritten)",
                Filter = "*.upk|*.upk"
            };
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
                            (x.ObjectName == "tlk" || x.ObjectName == "tlk_M" || x.ObjectName == "GlobalTlk_tlk" ||
                             x.ObjectName == "GlobalTlk_tlk_M") && x.ClassName == "BioTlkFile").ToList();
                        if (tlkExports.Count > 0)
                        {
                            string subPath = f.FullName.Substring(basePathLen);
                            foreach (ExportEntry exp in tlkExports)
                            {
                                var stringMapping = ((exp.ObjectName == "tlk" || exp.ObjectName == "GlobalTlk_tlk")
                                    ? tlkLines
                                    : tlkLines_m);
                                var talkFile = new ME1TalkFile(exp);
                                foreach (var sref in talkFile.StringRefs)
                                {
                                    if (sref.StringID == 0) continue; //skip blank
                                    if (sref.Data == null || sref.Data == "-1" || sref.Data == "")
                                        continue; //skip blank

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
                        (x.ObjectName == "GlobalTlk_tlk" || x.ObjectName == "GlobalTlk_tlk_M") &&
                        x.ClassName == "BioTlkFile").ToList();
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
                foreach (string filePath in MELoadedFiles.GetOfficialFiles(game, includeAFCs: true)
                    .Where(f => f.EndsWith(".afc", StringComparison.OrdinalIgnoreCase)))
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

        [DllImport(
            @"C:\Program Files (x86)\Audiokinetic\Wwise 2019.1.6.7110\SDK\x64_vc140\Release\bin\AkSoundEngineDLL.dll")]
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
            Microsoft.Win32.OpenFileDialog outputFileDialog = new()
            {
                Title = "Select .XML file to import",
                Filter = "*.xml|*.xml"
            };
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
            Microsoft.Win32.OpenFileDialog outputFileDialog = new()
            {
                Title = "Select TOC File",
                Filter = "*.bin|*.bin"
            };
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
                { Title = "Select folder to search", IsFolderPicker = true };
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
                var matInstanceConstants = pcc.Exports.Where((e) => e.ClassName=="MaterialInstanceConstant" && e.ObjectName.ToString().StartsWith("HMM_BRT_HVYa_MAT"));
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

                var startupF = new ME1TalkFile((ExportEntry)startup.GetEntry(3));
                var startupM = new ME1TalkFile((ExportEntry)startup.GetEntry(4));

                var globalF = (ExportEntry)global.GetEntry(1);
                var stringsF = new List<ME1TalkFile.TLKStringRef>();
                var globalM = (ExportEntry)global.GetEntry(2);
                var stringsM = new List<ME1TalkFile.TLKStringRef>();

                foreach (var id in stringIds)
                {
                    if (id >= 200000) continue;
                    var fStr = startupF.findDataById(id);
                    var mStr = startupM.findDataById(id);
                    fStr = fStr.Substring(1, fStr.Length - 2);
                    mStr = mStr.Substring(1, mStr.Length - 2);
                    stringsF.Add(new ME1TalkFile.TLKStringRef(id, 1, fStr));
                    stringsM.Add(new ME1TalkFile.TLKStringRef(id, 1, mStr));
                }

                HuffmanCompression hcf = new HuffmanCompression();
                HuffmanCompression hcm = new HuffmanCompression();

                hcf.LoadInputData(stringsF);
                hcm.LoadInputData(stringsM);
                hcf.serializeTalkfileToExport(globalF);
                hcm.serializeTalkfileToExport(globalM);
                global.Save();
            }


        }
    }
}