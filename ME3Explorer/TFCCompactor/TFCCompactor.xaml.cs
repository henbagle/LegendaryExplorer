﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gammtek.Conduit.Extensions.Collections.Generic;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using SlavaGu.ConsoleAppLauncher;

namespace ME3Explorer.TFCCompactor
{
    /// <summary>
    /// Interaction logic for TFCCompactor.xaml
    /// </summary>
    public partial class TFCCompactor : NotifyPropertyChangedWindowBase
    {
        private BackgroundWorker backgroundWorker;

        public TFCCompactor()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        private int _progressBarMax = 100, _progressBarValue;
        private bool _progressBarIndeterminate;
        public int ProgressBarMax
        {
            get => _progressBarMax;
            set => SetProperty(ref _progressBarMax, value);
        }
        public int ProgressBarValue
        {
            get => _progressBarValue;
            set => SetProperty(ref _progressBarValue, value);
        }
        public bool ProgressBarIndeterminate
        {
            get => _progressBarIndeterminate;
            set => SetProperty(ref _progressBarIndeterminate, value);
        }
        private GameWrapper _selectedGame;

        public GameWrapper SelectedGame
        {
            get => _selectedGame;
            set => SetProperty(ref _selectedGame, value);
        }

        private string _currentOperationText;
        public string CurrentOperationText
        {
            get => _currentOperationText;
            set => SetProperty(ref _currentOperationText, value);
        }

        private bool _scanForGameCompleted;

        public bool ScanForGameCompleted
        {
            get => _scanForGameCompleted;
            set => SetProperty(ref _scanForGameCompleted, value);
        }

        public bool IsNotBusy => backgroundWorker == null || !backgroundWorker.IsBusy;

        private string SelectedDLCModFolder;

        public ObservableCollectionExtended<GameWrapper> GameList { get; } = new ObservableCollectionExtended<GameWrapper>();
        public ObservableCollectionExtended<string> CustomDLCFolderList { get; } = new ObservableCollectionExtended<string>();
        public ObservableCollectionExtended<TFCSelector> TextureCachesToPullFromList { get; } = new ObservableCollectionExtended<TFCSelector>();

        public ICommand CompactTFCCommand { get; set; }
        public ICommand ScanCommand { get; set; }

        private void LoadCommands()
        {
            if (ME2Directory.DLCPath != null)
            {
                GameList.Add(new GameWrapper(MEGame.ME2, "Mass Effect 2", ME2Directory.DLCPath));
            }

            if (ME3Directory.DLCPath != null)
            {
                GameList.Add(new GameWrapper(MEGame.ME3, "Mass Effect 3", ME3Directory.DLCPath));
            }

            GameList.Add(new GameWrapper(MEGame.Unknown, "Select game...", null) { IsBrowseForCustom = true, IsCustomPath = true });


            CompactTFCCommand = new GenericCommand(BeginTFCCompaction, () => ScanForGameCompleted && IsNotBusy);
            ScanCommand = new GenericCommand(BeginReferencedTFCScan, () => DLCModFolderIsSelected() && IsNotBusy);
        }

        private static string[] basegameTFCs = { "CharTextures", "Movies", "Textures", "Lighting" };
        private void BeginReferencedTFCScan()
        {
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerReportsProgress = true;
            var dlcDir = SelectedGame.DLCPath;
            dlcDir = Path.Combine(dlcDir, SelectedDLCModFolder);


            backgroundWorker.DoWork += (a, b) =>
            {
                CurrentOperationText = "Getting list of files";
                ProgressBarValue = 0;
                ProgressBarIndeterminate = true;

                string[] files = Directory.GetFiles(dlcDir, "*.pcc", SearchOption.AllDirectories);
                ProgressBarMax = files.Length;
                ProgressBarIndeterminate = false;

                if (files.Any())
                {
                    SortedSet<TFCSelector> referencedTFCs = new SortedSet<TFCSelector>();
                    foreach (string file in files)
                    {
                        CurrentOperationText = $"Scanning {Path.GetFileName(file)}...";
                        using (var package = MEPackageHandler.OpenMEPackage(file))
                        {
                            var textureExports = package.Exports.Where(x => x.IsTexture());
                            foreach (var texture in textureExports)
                            {
                                if (texture.GetProperty<NameProperty>("TextureFileCacheName") is NameProperty tfcNameProperty)
                                {
                                    string tfcname = tfcNameProperty.Value;
                                    if (tfcname == "CustTextures0")
                                    {
                                        Debug.WriteLine($"CustTextures0 TFC Reference: {texture.FullPath} {texture.UIndex} in {texture.FileRef.FilePath}");
                                    }
                                    if (!basegameTFCs.Contains(tfcname))
                                    {
                                    //Check that external mips are referenced.
                                    //some texture2d have a tfc but don't have any external mips for some reason
                                    Texture2D texture2d = new Texture2D(texture);
                                        var topmip = texture2d.GetTopMip();
                                        if (topmip.storageType == StorageTypes.extLZO ||
                                            topmip.storageType == StorageTypes.extZlib ||
                                            topmip.storageType == StorageTypes.extUnc)
                                        {
                                            if (referencedTFCs.Add(new TFCSelector(tfcname, true)))
                                            {
                                                Debug.WriteLine($"Reference to {tfcname} in {Path.GetFileName(texture.FileRef.FilePath)} {texture.UIndex} {texture.InstancedFullPath}");
                                                Application.Current.Dispatcher.Invoke(delegate
                                                {
                                                    TextureCachesToPullFromList.ReplaceAll(referencedTFCs);
                                                });
                                            }
                                        }
                                        else
                                        {
                                        //Debug.WriteLine($"Skipping Reference, no external mips defined: {texture.GetFullPath} {texture.UIndex} in {texture.FileRef.FilePath}");
                                    }
                                    }
                                }
                            }
                        }

                        ProgressBarValue++;
                    }
                }
            };
            backgroundWorker.RunWorkerCompleted += (a, b) =>
            {
                ScanForGameCompleted = true;
                backgroundWorker = null;
                CurrentOperationText = "Initial scan completed";
                OnPropertyChanged(nameof(IsNotBusy));
                CommandManager.InvalidateRequerySuggested();
            };
            backgroundWorker.RunWorkerAsync();
            OnPropertyChanged(nameof(IsNotBusy));
        }

        private void BeginTFCCompaction()
        {
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerReportsProgress = true;
            string sourceGamePath = Path.GetDirectoryName(Path.GetDirectoryName(SelectedGame.DLCPath));
            string workingGamePath = Path.Combine(Path.GetTempPath(), "TFCCompact"); //Todo: Allow user to change this path

            backgroundWorker.DoWork += (a, b) =>
            {
                if (Directory.Exists(workingGamePath))
                {
                //Apparently the built in delete directory can't handle there being files
                //even though the documentation says it can delete subfiles.
                DeleteDirectory(workingGamePath);
                }
                CurrentOperationText = "Creating compaction workspace";
                ProgressBarValue = 0;
                ProgressBarMax = 100;
                ProgressBarIndeterminate = false;

                var dlcTFCsToPullFrom = TextureCachesToPullFromList.Where(x => x.Selected).Select(x => x.TFCName);
            //Create workspace for MEM
            var game = (int)SelectedGame.Game;

            //Create fake game directory
            Directory.CreateDirectory(workingGamePath);
                Directory.CreateDirectory(Path.Combine(workingGamePath, "Binaries"));
                if (game == 3)
                {
                    Directory.CreateDirectory(Path.Combine(workingGamePath, "Binaries", "win32"));
                    File.Create(Path.Combine(workingGamePath, "Binaries", "win32", "MassEffect3.exe")).Close();
                }
                else
                {
                //ME2
                File.Create(Path.Combine(workingGamePath, "Binaries", "MassEffect2.exe"));
                }

                string cookedDirName = game == 2 ? "CookedPC" : "CookedPCConsole";
                Directory.CreateDirectory(Path.Combine(workingGamePath, "BioGame"));
                var dlcDir = Path.Combine(workingGamePath, "BioGame", "DLC");
                Directory.CreateDirectory(dlcDir);
                var basegameCookedDir = Path.Combine(workingGamePath, "BioGame", cookedDirName);
                Directory.CreateDirectory(basegameCookedDir);

            //Copy basegame TFCs to cookedDiretory
            var basegameDirToCopyFrom = MEDirectories.CookedPath(SelectedGame.Game);
                var tfcs = Directory.GetFiles(basegameDirToCopyFrom, "*.tfc").ToList();
                var currentgamefiles = MELoadedFiles.GetFilesLoadedInGame(SelectedGame.Game, forceReload: true, includeTFC: true);
                var debug = currentgamefiles.Where(x => x.Value.Contains(".tfc")).ToList();
                debug.ForEach(x => Debug.WriteLine(x));
                foreach (var tfc in dlcTFCsToPullFrom)
                {
                    var fullname = tfc.EndsWith(".tfc") ? tfc : tfc + ".tfc";
                    if (currentgamefiles.TryGetValue(fullname, out string fullpath))
                    {
                        tfcs.Add(fullpath);
                    }
                    else if (SelectedGame.Game == MEGame.ME3)
                    {
                    //Attempt SFAR lookup at later stage below. Will abort if we cannot find TFC.
                    tfcs.Add(tfc); //no suffix
                }
                    else
                    {
                        b.Result = new Tuple<CompactionResult, string>(CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
                        return;
                    }
                }

                ProgressBarIndeterminate = true;
                foreach (var tfc in tfcs)
                {
                    string tfcShortName = Path.GetFileNameWithoutExtension(tfc);
                    CurrentOperationText = $"Staging {tfcShortName}";
                    string destPath = Path.Combine(basegameCookedDir, tfcShortName + ".tfc");
                    if (File.Exists(tfc))
                    {
                        File.Copy(tfc, destPath, true);
                    }
                    else if (SelectedGame.Game == MEGame.ME3)
                    {
                        if (tfcShortName.StartsWith("Textures_DLC"))
                        {
                            string dlcFolderName = tfc.Substring(9);
                            var sfar = Path.Combine(MEDirectories.DLCPath(SelectedGame.Game), dlcFolderName, "CookedPCConsole", "Default.sfar");
                            if (File.Exists(sfar) && new FileInfo(sfar).Length > 32)
                            {
                            //sfar exists and is not fully unpacked (with mem style 32 byte sfar)
                            DLCPackage p = new DLCPackage(sfar);
                                var tfcIndex = p.FindFileEntry(tfcShortName + ".tfc");
                                if (tfcIndex >= 0)
                                {
                                    var tfcMemory = p.DecompressEntry(tfcIndex);
                                    File.WriteAllBytes(destPath, tfcMemory.ToArray());
                                    tfcMemory.Close();
                                }
                                else
                                {
                                //Can't find TFC!
                                b.Result = new Tuple<CompactionResult, string>(CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
                                    return;
                                }
                            }
                            else
                            {
                            //Can't find TFC!
                            b.Result = new Tuple<CompactionResult, string>(CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
                                return;
                            }
                        }
                        else
                        {
                        //Can't find TFC!
                        b.Result = new Tuple<CompactionResult, string>(CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
                            return;
                        }
                    }
                    else
                    {
                    //Can't find TFC!
                    b.Result = new Tuple<CompactionResult, string>(CompactionResult.RESULT_ERROR_TFC_NOT_FOUND, $"Unable to find TFC for compaction in game directory: {tfc}");
                        return;
                    }
                }

            //Copy DLC
            var destDLCDir = Path.Combine(dlcDir, SelectedDLCModFolder);
                var sourceDLCDir = Path.Combine(SelectedGame.DLCPath, SelectedDLCModFolder);
                CopyDir.CopyAll_ProgressBar(new DirectoryInfo(sourceDLCDir), Directory.CreateDirectory(destDLCDir), backgroundWorker, ignoredExtensions: new string[] { ".tfc" });
            // get MassEffectModder.ini
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MassEffectModder");
                string _iniPath = Path.Combine(path, "MassEffectModder.ini");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                if (!File.Exists(_iniPath))
                {
                    File.Create(_iniPath);
                }

                Ini.IniFile ini = new Ini.IniFile(_iniPath);
                var oldValue = ini.ReadValue("ME" + game, "GameDataPath");
                ini.WriteValue("GameDataPath", "ME" + game, workingGamePath);

            //Scan game
            ProgressBarMax = 100;
                ProgressBarValue = 0;
                var triggers = new Dictionary<string, Action<string>> {
                    { "TASK_PROGRESS", s => ProgressBarValue = int.Parse(s)},
                    { "PROCESSING_FILE", s => CurrentOperationText = $"Building texture map for {Path.GetFileName(s)}"}
                };

                string args = $"--scan --gameid {game} --ipc";
                var memProcess = MassEffectModder.MassEffectModderIPCWrapper.RunMEM(args, triggers);
                while (memProcess.State == AppState.Running)
                {
                    Thread.Sleep(100); //this is kind of hacky but it works
            }

            //Extract textures
            var tempTextureCache = Directory.CreateDirectory(Path.Combine(workingGamePath, "TextureStaging")).FullName;
                var singlepasscount = Directory.GetFiles(destDLCDir, "*.pcc", SearchOption.AllDirectories).Length;
                var totalpasses = singlepasscount * dlcTFCsToPullFrom.Count();
                int previousFullStepsDone = 0;
                ProgressBarValue = 0;
                ProgressBarMax = totalpasses;
                foreach (var tfcname in dlcTFCsToPullFrom)
                {
                    CurrentOperationText = $"Extracting referenced textures from {tfcname}";
                    triggers = new Dictionary<string, Action<string>> {
                        { "Package", s =>
                            {
                                int done = int.Parse(s.Substring(0,s.IndexOf('/')));
                                ProgressBarValue = previousFullStepsDone + done;
                                Debug.WriteLine(done + " "+ProgressBarValue);
                            }
                        }
                    };

                    args = $"--extract-all-dds --gameid {game} --output \"{tempTextureCache}\" --tfc-name {tfcname}";
                    memProcess = MassEffectModder.MassEffectModderIPCWrapper.RunMEM(args, null, triggers); //this command does not support IPC commands
                while (memProcess.State == AppState.Running)
                    {
                        Thread.Sleep(100); //this is kind of hacky but it works
                }

                    previousFullStepsDone += singlepasscount;
                }

            //Install new textures
            CurrentOperationText = $"Building new TFC";
                ProgressBarValue = 0;
                ProgressBarMax = Directory.GetFiles(tempTextureCache, "*.dds").Length;
                triggers = new Dictionary<string, Action<string>> {
                    { "Installing", s =>
                        {
                            string remainingStr = s.Substring(5); //cut off "mod: "

                            int done = int.Parse(remainingStr.Substring(0,remainingStr.IndexOf(' ')));
                            ProgressBarValue = done;
                            CurrentOperationText = "Building new TFC | "+remainingStr.Substring(remainingStr.LastIndexOf(' ')).Trim();
                        }
                    }
                };

                args = $"-dlc-mod-for-mgamerz {game} \"{tempTextureCache}\" {"Textures_" + SelectedDLCModFolder}";
                memProcess = MassEffectModder.MassEffectModderIPCWrapper.RunMEM(args, null, triggers, true); //this command does not support IPC commands
            while (memProcess.State == AppState.Running)
                {
                    Thread.Sleep(100); //this is kind of hacky but it works
            }


            //Restore old path in MEM ini
            if (!string.IsNullOrEmpty(oldValue))
                {
                    ini.WriteValue("GameDataPath", "ME" + game, oldValue);
                }
            };
            backgroundWorker.ProgressChanged += (a, b) =>
            {
                if (b.UserState is ThreadCommand tc)
                {
                    switch (tc.Command)
                    {
                        case CopyDir.UPDATE_PROGRESSBAR_VALUE:
                            ProgressBarValue = (int)tc.Data;
                            break;
                        case CopyDir.UPDATE_PROGRESSBAR_MAXVALUE:
                            ProgressBarMax = (int)tc.Data;
                            break;
                        case CopyDir.UPDATE_CURRENT_FILE_TEXT:
                            CurrentOperationText = (string)tc.Data;
                            break;
                        case CopyDir.UPDATE_PROGRESSBAR_INDETERMINATE:
                            ProgressBarIndeterminate = (bool)tc.Data;
                            break;
                    }
                }
            };
            backgroundWorker.RunWorkerCompleted += (a, b) =>
            {
                if (b.Result is Tuple<CompactionResult, string> result)
                {
                    switch (result.Item1)
                    {
                        case CompactionResult.RESULT_OK:
                        //nothing
                        MessageBox.Show("Done.");
                            break;
                        case CompactionResult.RESULT_ERROR_TFC_NOT_FOUND:
                            MessageBox.Show(result.Item2);
                            break;
                    }
                }
                OnPropertyChanged(nameof(IsNotBusy));
            };
            backgroundWorker.RunWorkerAsync();
            OnPropertyChanged(nameof(IsNotBusy));
        }

        private enum CompactionResult
        {
            RESULT_OK,
            RESULT_ERROR_TFC_NOT_FOUND
        }

        private bool GameIsSelected() => SelectedGame != null && SelectedGame.IsBrowseForCustom == false;
        private bool DLCModFolderIsSelected() => GameIsSelected() && SelectedDLCModFolder != null;

        public class GameWrapper : NotifyPropertyChangedBase
        {
            public MEGame Game;
            private string _displayName;
            public string DisplayName
            {
                get => _displayName;
                set => SetProperty(ref _displayName, value);
            }
            public string DLCPath;
            public bool IsBrowseForCustom;
            public bool IsCustomPath;

            public GameWrapper(MEGame game, string displayName, string dlcPath)
            {
                Game = game;
                DisplayName = displayName;
                DLCPath = dlcPath;
            }
        }

        private void DLCModComboBox_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                ScanForGameCompleted = false;
                SelectedDLCModFolder = (string)e.AddedItems[0];
            }
            else
            {
                ScanForGameCompleted = false;
                SelectedDLCModFolder = null;
            }
        }


        /// <summary>
        /// Depth-first recursive delete, with handling for descendant 
        /// directories open in Windows Explorer.
        /// </summary>
        public static void DeleteDirectory(string path)
        {
            foreach (string directory in Directory.GetDirectories(path))
            {
                DeleteDirectory(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
        }

        private void GameComboBox_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                ScanForGameCompleted = false;
                var newItem = (GameWrapper)e.AddedItems[0];
                if (newItem.IsBrowseForCustom)
                {
                    // Browse
                    OpenFileDialog ofd = new OpenFileDialog();
                    ofd.Title = $"Select game executable";
                    string filter = $"ME2/ME3 executable|MassEffect2.exe;MassEffect3.exe";
                    ofd.Filter = filter;
                    if (ofd.ShowDialog() == true)
                    {
                        MEGame gameSelected = Path.GetFileName(ofd.FileName).Equals("MassEffect3.exe", StringComparison.InvariantCultureIgnoreCase) ? MEGame.ME3 : MEGame.ME2;

                        string result = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

                        if (gameSelected == MEGame.ME3)
                            result = Path.GetDirectoryName(result); //up one more because of win32 directory.
                        string displayPath = result;
                        result = Path.Combine(result, @"BioGame\DLC");

                        if (Directory.Exists(result))
                        {
                            newItem.Game = gameSelected;
                            newItem.DisplayName = displayPath;
                            newItem.DLCPath = result;
                            newItem.IsCustomPath = true;
                            newItem.IsBrowseForCustom = false;
                            GameList.RemoveAll(x => (x.IsBrowseForCustom || x.IsCustomPath) && x != newItem);
                            GameList.Add(new GameWrapper(MEGame.Unknown, "Select game...", null) { IsBrowseForCustom = true, IsCustomPath = true });
                            SelectedGame = newItem;
                            var officialDLC = gameSelected == MEGame.ME3 ? ME3Directory.OfficialDLC : ME2Directory.OfficialDLC;
                            var DLC = MELoadedFiles.GetEnabledDLC(gameSelected, result).Select(x => Path.GetFileName(x)).Where(x => !officialDLC.Contains(x));
                            CustomDLCFolderList.ReplaceAll(DLC);
                        }
                    }
                }
                else
                {
                    ScanForGameCompleted = false;
                    SelectedGame = newItem;
                    var officialDLC = newItem.Game == MEGame.ME3 ? ME3Directory.OfficialDLC : ME2Directory.OfficialDLC;
                    var DLC = MELoadedFiles.GetEnabledDLC(newItem.Game, newItem.DLCPath).Select(x => Path.GetFileName(x)).Where(x => !officialDLC.Contains(x));
                    CustomDLCFolderList.ReplaceAll(DLC);
                }
            }
        }

        public class TFCSelector : IComparable
        {
            public TFCSelector(string tfcname, bool selected)
            {
                TFCName = tfcname;
                Selected = selected;
            }

            public string TFCName { get; set; }
            public bool Selected { get; set; }

            public int CompareTo(object other)
            {
                if (other is TFCSelector t)
                {
                    return TFCName.CompareTo(t.TFCName);
                }
                return 1;
            }

            public override bool Equals(object other)
            {
                if (other is TFCSelector t)
                {
                    return t.TFCName == TFCName;
                }
                return false;
            }
        }
    }
}
