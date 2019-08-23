﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
using Microsoft.Win32;

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
        public ObservableCollectionExtended<string> TextureCachesToPullFromList { get; } = new ObservableCollectionExtended<string>();

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
            ScanCommand = new GenericCommand(BeginReferencedTFCScan, DLCModFolderIsSelected);
        }

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
                    Debug.WriteLine("Many files.");
                    SortedSet<string> referencedTFCs = new SortedSet<string>();
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
                                    if (tfcNameProperty.Value == "CustTextures0" || tfcNameProperty.Value == "TEXTURES_DLC_MOD_PEOM2")
                                        Debug.WriteLine(tfcNameProperty.Value + " referenced in " + texture.GetFullPath + " (" + texture.UIndex + ") in " + file);

                                    if (referencedTFCs.Add(tfcNameProperty.Value))
                                    {
                                        Application.Current.Dispatcher.Invoke(delegate
                                        {
                                            TextureCachesToPullFromList.ReplaceAll(referencedTFCs);
                                        });
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
                OnPropertyChanged(nameof(IsNotBusy));
            };
            backgroundWorker.RunWorkerAsync();
            OnPropertyChanged(nameof(IsNotBusy));
        }

        private void BeginTFCCompaction()
        {
            //throw new NotImplementedException();
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
                SelectedDLCModFolder = (string)e.AddedItems[0];
            }
            else
            {
                SelectedDLCModFolder = null;
            }
        }

        private void GameComboBox_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
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
                    SelectedGame = newItem;
                    var officialDLC = newItem.Game == MEGame.ME3 ? ME3Directory.OfficialDLC : ME2Directory.OfficialDLC;
                    var DLC = MELoadedFiles.GetEnabledDLC(newItem.Game, newItem.DLCPath).Select(x => Path.GetFileName(x)).Where(x => !officialDLC.Contains(x));
                    CustomDLCFolderList.ReplaceAll(DLC);
                }
            }
        }
    }
}
