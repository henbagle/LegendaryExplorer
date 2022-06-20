﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Numerics;
using FontAwesome5;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.GameInterop;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Collections.ObjectModel;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using InterpCurveVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<System.Numerics.Vector3>;
using InterpCurveFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<float>;
using LegendaryExplorer.Tools.PackageEditor;

namespace LegendaryExplorer.Tools.LiveLevelEditor
{
    /// <summary>
    /// Interaction logic for LiveLevelEditorWindow.xaml - MGAMERZ PIPE VERSION
    /// </summary>
    public partial class LiveLevelEditorWindow2 : TrackingNotifyPropertyChangedWindowBase
    {
        #region Properties and Startup

        private static readonly Dictionary<MEGame, LiveLevelEditorWindow2> Instances = new();
        public static LiveLevelEditorWindow2 Instance(MEGame game)
        {
            if (!GameController.GetInteropTargetForGame(game)?.ModInfo?.CanUseLLE ?? true)
                throw new ArgumentException(@"Live Level Editor does not support this game!", nameof(game));

            return Instances.TryGetValue(game, out var lle) ? lle : null;
        }

        private bool _readyToView;
        public bool ReadyToView
        {
            get => _readyToView;
            set
            {
                if (SetProperty(ref _readyToView, value))
                {
                    OnPropertyChanged(nameof(CamPathReadyToView));
                }
            }
        }

        public bool IsME3 => Game is MEGame.ME3;

        public bool CamPathReadyToView => _readyToView && IsME3;

        private bool _readyToInitialize = true;
        public bool ReadyToInitialize
        {
            get => _readyToInitialize;
            set => SetProperty(ref _readyToInitialize, value);
        }

        public MEGame Game { get; }
        public InteropTarget GameTarget { get; }

        public LiveLevelEditorWindow2(MEGame game) : base("Live Level Editor", true)
        {
            Game = game;
            GameTarget = GameController.GetInteropTargetForGame(game);
            if (GameTarget is null || !GameTarget.ModInfo.CanUseLLE)
            {
                throw new Exception($"{game} does not support Live Level Editor!");
            }

            GameTarget.GameReceiveMessage += GameControllerOnReceiveMessage2; // Use pipe version

            if (Instance(game) is not null)
            {
                throw new Exception($"Can only have one instance of {game} LiveLevelEditor open!");
            }
            Instances[game] = this;

            DataContext = this;
            LoadCommands();
            InitializeComponent();
            GameOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            GameOpenTimer.Tick += CheckIfGameOpen;
            RetryLoadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            RetryLoadTimer.Tick += RetryLoadLiveEditor;

            switch (game)
            {
                case MEGame.ME3:
                    gameInstalledReq.FullfilledText = "Mass Effect 3 is installed";
                    gameInstalledReq.UnFullfilledText = "Can't find Mass Effect 3 installation!";
                    gameInstalledReq.ButtonText = "Set ME3 path";
                    break;
                case MEGame.ME2:
                    gameInstalledReq.FullfilledText = "Mass Effect 2 is installed";
                    gameInstalledReq.UnFullfilledText = "Can't find Mass Effect 2 installation!";
                    gameInstalledReq.ButtonText = "Set ME2 path";
                    break;
                case MEGame.LE1:
                    gameInstalledReq.FullfilledText = "Legendary Edition is installed";
                    gameInstalledReq.UnFullfilledText = "Can't find Legendary Edition installation!";
                    gameInstalledReq.ButtonText = "Set LE1 path";
                    break;
            }
        }

        private void LiveLevelEditor_OnClosing(object sender, CancelEventArgs e)
        {
            DisposeCamPath();
            DataContext = null;
            GameTarget.GameReceiveMessage -= GameControllerOnReceiveMessage;
            Instances.Remove(Game);
        }

        #endregion

        #region Commands
        public Requirement.RequirementCommand GameInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand ASILoaderInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand InteropASIInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand ConsoleASIInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand SupportFilesInstalledRequirementCommand { get; set; }
        public ICommand LoadLiveEditorCommand { get; set; }
        public ICommand OpenPackageCommand { get; set; }
        public ICommand OpenActorInPackEdCommand { get; set; }
        public ICommand RegenActorListCommand { get; set; }
        void LoadCommands()
        {
            GameInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsGameInstalled(Game), () => InteropHelper.SelectGamePath(Game));
            ASILoaderInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsASILoaderInstalled(Game), () => InteropHelper.OpenASILoaderDownload(Game));
            InteropASIInstalledRequirementCommand = new Requirement.RequirementCommand(() => true, /*InteropHelper.IsInteropASIInstalled(Game),*/ () => InteropHelper.OpenInteropASIDownload(Game));
            ConsoleASIInstalledRequirementCommand = new Requirement.RequirementCommand(InteropHelper.IsME3ConsoleExtensionInstalled, InteropHelper.OpenConsoleExtensionDownload);
            SupportFilesInstalledRequirementCommand = new Requirement.RequirementCommand(() => true/*AreSupportFilesInstalled*/, InstallSupportFiles);
            LoadLiveEditorCommand = new GenericCommand(LoadLiveEditor, CanLoadLiveEditor);
            OpenPackageCommand = new GenericCommand(OpenPackage, CanOpenPackage);
            OpenActorInPackEdCommand = new GenericCommand(OpenActorInPackEd, CanOpenInPackEd);
            RegenActorListCommand = new GenericCommand(RegenActorList);
        }

        private void RegenActorList()
        {
            SetBusy("Building Actor List", () => { });
            DumpActors();
        }

        private bool CanOpenInPackEd() => SelectedActor != null;

        private void OpenActorInPackEd()
        {
            if (SelectedActor != null)
            {
                OpenInPackEd(SelectedActor.FileName, SelectedActor.UIndex);
            }
        }

        private bool CanOpenPackage() => listBoxPackages.SelectedItem != null;

        private void OpenPackage()
        {
            if (listBoxPackages.SelectedItem is KeyValuePair<string, List<ActorEntry>> kvp)
            {
                OpenInPackEd(kvp.Key);
            }
        }

        private void OpenInPackEd(string fileName, int uIndex = 0)
        {
            if (MELoadedFiles.GetFilesLoadedInGame(Game).TryGetValue(fileName, out string filePath))
            {
                if (WPFBase.TryOpenInExisting(filePath, out PackageEditorWindow packEd))
                {
                    packEd.GoToNumber(uIndex);
                }
                else
                {
                    PackageEditorWindow p = new();
                    p.Show();
                    p.LoadFile(filePath, uIndex);
                }
            }
            else
            {
                MessageBox.Show(this, $"Cannot Find pcc named {fileName}!");
            }
        }

        private bool CanLoadLiveEditor() => ReadyToInitialize && gameInstalledReq.IsFullfilled && asiLoaderInstalledReq.IsFullfilled && supportFilesInstalledReq.IsFullfilled
                                         && interopASIInstalledReq.IsFullfilled && (!IsME3 || consoleASIInstalledReq.IsFullfilled) && GameController.TryGetMEProcess(Game, out _);

        private void LoadLiveEditor()
        {
            SetBusy("Loading Live Editor", () => RetryLoadTimer.Stop());
            //GameTarget.ExecuteConsoleCommands("ce LoadLiveEditor");
            InteropHelper.SendMessageToGame("LLE_TEST_ACTIVE", Game); // If this response works, we will know we are ready and can now start
            RetryLoadTimer.Start();
        }

        private bool AreSupportFilesInstalled()
        {
            return InteropModInstaller.IsModInstalledAndUpToDate(GameTarget);
        }

        private void InstallSupportFiles()
        {
            SetBusy("Installing Support Files");
            Task.Run(() =>
            {
                InteropModInstaller installer = Game is MEGame.LE1 ? new LE1InteropModInstaller(GameTarget, SelectLE1Map) : new InteropModInstaller(GameTarget);
                installer.InstallDLC_MOD_Interop();
                EndBusy();
                CommandManager.InvalidateRequerySuggested();
            });
        }

        /// <summary>
        /// Callback method for the user to select a map when installing the LE1 Interop Mod
        /// </summary>
        /// <param name="maps">List of LE1 Map files to be selectable in the dialog</param>
        /// <returns>List of master file names to augment and install in interop mod</returns>
        private IEnumerable<string> SelectLE1Map(IEnumerable<string> maps)
        {
            var selectedMaps = new List<string>();
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dropdownDialog = new DropdownPromptDialog("Please select the master file for the map you will be live editing.\nThe interop mod must be re-installed if you want to edit in a different map.", "Select LE1 Map to use in Interop Mod",
                    "Select master file", maps, this);
                dropdownDialog.ShowDialog();
                if (dropdownDialog.DialogResult == true)
                {
                    if (dropdownDialog.Response == "CUSTOM")
                    {
                        var prompt = new PromptDialog("Enter custom master file name. (For advanced users only)",
                            "Custom Master File")
                        {
                            Owner = this
                        };
                        prompt.ShowDialog();
                        if (prompt.DialogResult == true) selectedMaps.Add(prompt.ResponseText);
                    }
                    else selectedMaps.Add(dropdownDialog.Response);
                }
            });

            return selectedMaps;
        }
        #endregion

        // NOT USED
        private void GameControllerOnReceiveMessage(string msg)
        {
            if (msg == LiveEditHelper.LoaderLoadedMessage)
            {
                ReadyToView = false;
                ReadyToInitialize = true;
                instructionsTab.IsSelected = true;
                if (!GameOpenTimer.IsEnabled)
                {
                    GameOpenTimer.Start();
                }

                ActorDict.Clear();
            }
            else if (msg == "LiveEditor string Loaded")
            {
                RetryLoadTimer.Stop();
                BusyText = "Building Actor list";
                ReadyToView = true;
                actorTab.IsSelected = true;
                InitializeCamPath();
                EndBusy();
            }
            else if (msg == "LiveEditor string ActorsDumped")
            {
                BuildActorDict();
                EndBusy();
            }
            else if (msg == "LiveEditCamPath string CamPathComplete")
            {
                playbackState = PlaybackState.Paused;
                PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            }
            else if (msg.StartsWith("LiveEditor string ActorSelected"))
            {
                Vector3 pos = defaultPosition;
                if (msg.IndexOf("vector") is int idx && idx > 0 &&
                    msg.Substring(idx + 7).Split(' ') is string[] { Length: 3 } strings)
                {
                    var floats = new float[3];
                    for (int i = 0; i < 3; i++)
                    {
                        if (float.TryParse(strings[i], out float f))
                        {
                            floats[i] = f;
                        }
                        else
                        {
                            defaultPosition.CopyTo(floats);
                            break;
                        }
                    }
                    pos = new Vector3(floats[0], floats[1], floats[2]);


                }
                noUpdate = true;
                XPos = (int)pos.X;
                YPos = (int)pos.Y;
                ZPos = (int)pos.Z;
                noUpdate = false;
                EndBusy();
            }
            else if (msg.StartsWith("LiveEditor string ActorRotation"))
            {
                Rotator rot = defaultRotation;
                if (msg.IndexOf("vector") is int idx && idx > 0 &&
                    msg.Substring(idx + 7).Split(' ') is string[] { Length: >= 3 } strings)
                {
                    var floats = new float[3];
                    for (int i = 0; i < 3; i++)
                    {
                        if (float.TryParse(strings[i], out float f))
                        {
                            floats[i] = f;
                        }
                        else
                        {
                            defaultPosition.CopyTo(floats);
                            break;
                        }
                    }
                    rot = Rotator.FromDirectionVector(new Vector3(floats[0], floats[1], floats[2]));
                    if (msg.IndexOf("int") is int rollIdx && rollIdx > 0 &&
                        msg.Substring(rollIdx + 4).Split(' ') is string[] { Length: >= 1 } rollStrings &&  int.TryParse(rollStrings[0], out int roll))
                    {
                        rot = new Rotator(rot.Pitch, rot.Yaw, roll);
                    }
                }
                noUpdate = true;
                Yaw = (int)rot.Yaw.UnrealRotationUnitsToDegrees();
                Pitch = (int)rot.Pitch.UnrealRotationUnitsToDegrees();
                Roll = (int)rot.Roll.UnrealRotationUnitsToDegrees();
                noUpdate = false;
                EndBusy();
            }
        }

        private void GameControllerOnReceiveMessage2(string msg)
        {
            var command = msg.Split(" ");
            if (command.Length < 2)
                return;
            if (command[0] != "LIVELEVELEDITOR")
                return; // Not for us

            Debug.WriteLine($"LLE Command: {msg}");
            var verb = command[1]; // Message Info
            // "READY" is done on first initialize and will automatically 
            if (verb == "READY") // We polled game, and found LLE is available
            {
                //ReadyToView = false;
                //ReadyToInitialize = true;
                //instructionsTab.IsSelected = true;
                //if (!GameOpenTimer.IsEnabled)
                //{
                //    GameOpenTimer.Start();
                //}

                //ActorDict.Clear();

                // Test: Dump actors
                DumpActors();
            }
            else if (verb == "ACTORDUMPSTART") // We're about to receive a list of actors
            {
                ActorDict.Clear();
            }
            else if (verb == "ACTORINFO") // We're about to receive a list of this many actors
            {
                BuildActor(string.Join("", command.Skip(2))); // Skip tool and verb
            }
            else if (verb == "ACTORDUMPFINISHED") // Finished dumping actors
            {
                // We have reached the end of the list
                RetryLoadTimer.Stop();
                BusyText = "Building Actor list";
                ReadyToView = true;
                actorTab.IsSelected = true;
                InitializeCamPath();
                EndBusy();
            }
            else if (verb == "ACTORLOC" && command.Length == 5)
            {
                Vector3 pos = defaultPosition;
                pos.X = float.Parse(command[2]);
                pos.Y = float.Parse(command[3]);
                pos.Z = float.Parse(command[4]);
                //if (msg.IndexOf("vector") is int idx && idx > 0 &&
                //    msg.Substring(idx + 7).Split(' ') is string[] { Length: 3 } strings)
                //{
                //    var floats = new float[3];
                //    for (int i = 0; i < 3; i++)
                //    {
                //        if (float.TryParse(strings[i], out float f))
                //        {
                //            floats[i] = f;
                //        }
                //        else
                //        {
                //            defaultPosition.CopyTo(floats);
                //            break;
                //        }
                //    }
                //    pos = new Vector3(floats[0], floats[1], floats[2]);


                //}
                noUpdate = true;
                XPos = (int)pos.X;
                YPos = (int)pos.Y;
                ZPos = (int)pos.Z;
                noUpdate = false;
                EndBusy();
            }
            else if (verb == "ACTORROT" && command.Length == 5)
            {
                Rotator rot = new Rotator(int.Parse(command[2]), int.Parse(command[3]), int.Parse(command[4]));

                //if (msg.IndexOf("vector") is int idx && idx > 0 &&
                //    msg.Substring(idx + 7).Split(' ') is string[] { Length: >= 3 } strings)
                //{
                //    var floats = new float[3];
                //    for (int i = 0; i < 3; i++)
                //    {
                //        if (float.TryParse(strings[i], out float f))
                //        {
                //            floats[i] = f;
                //        }
                //        else
                //        {
                //            defaultPosition.CopyTo(floats);
                //            break;
                //        }
                //    }
                //    rot = Rotator.FromDirectionVector(new Vector3(floats[0], floats[1], floats[2]));
                //    if (msg.IndexOf("int") is int rollIdx && rollIdx > 0 &&
                //        msg.Substring(rollIdx + 4).Split(' ') is string[] { Length: >= 1 } rollStrings &&  int.TryParse(rollStrings[0], out int roll))
                //    {
                //        rot = new Rotator(rot.Pitch, rot.Yaw, roll);
                //    }
                //}
                noUpdate = true;
                Yaw = (int)rot.Yaw.UnrealRotationUnitsToDegrees();
                Pitch = (int)rot.Pitch.UnrealRotationUnitsToDegrees();
                Roll = (int)rot.Roll.UnrealRotationUnitsToDegrees();
                noUpdate = false;
                EndBusy();
            }


            else if (verb == "LiveEditor string Loaded")
            {
                RetryLoadTimer.Stop();
                BusyText = "Building Actor list";
                ReadyToView = true;
                actorTab.IsSelected = true;
                InitializeCamPath();
                EndBusy();
            }
            else if (msg == "LiveEditCamPath string CamPathComplete")
            {
                playbackState = PlaybackState.Paused;
                PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            }
        }

        /// <summary>
        /// Tells the game to dump the list of actors
        /// </summary>
        private void DumpActors()
        {
            // Reload all files in the game to make sure the list is current
            MELoadedFiles.GetFilesLoadedInGame(Game, true);
            InteropHelper.SendMessageToGame("LLE_DUMP_ACTORS", Game);
        }


        private readonly DispatcherTimer GameOpenTimer;
        private void CheckIfGameOpen(object sender, EventArgs e)
        {
            if (!GameController.TryGetMEProcess(Game, out _))
            {
                ReadyToInitialize = false;
                EndBusy();
                ReadyToView = false;
                SelectedActor = null;
                instructionsTab.IsSelected = true;
                GameOpenTimer.Stop();
            }
        }

        private readonly DispatcherTimer RetryLoadTimer;
        private void RetryLoadLiveEditor(object sender, EventArgs e)
        {
            if (ReadyToView)
            {
                EndBusy();
                RetryLoadTimer.Stop();
            }
            else
            {
                GameTarget.ExecuteConsoleCommands("ce LoadLiveEditor");
            }
        }

        public ObservableDictionary<string, ObservableCollectionExtended<ActorEntry2>> ActorDict { get; } = new();
        private void BuildActor(string actorInfoStr)
        {
            string[] parts = actorInfoStr.Split(':');

            // We only care about actors that loaded from a package file directly (level files)
            // GetFilesLoadedInGame will return a cached result
            if (parts.Length == 2  && MELoadedFiles.GetFilesLoadedInGame(Game).ContainsKey($"{parts[0]}.pcc"))
            {
                ActorDict.AddToListAt($"{parts[0]}.pcc", new ActorEntry2
                {
                    ActorName = parts[1],
                    FileName = $"{parts[0]}.pcc",
                });
            }
            else
            {
                Debug.WriteLine($"SKIPPING {actorInfoStr}");
            }
        }

        private void BuildActorDict()
        {
            ActorDict.Clear();
            string actorDumpPath = Path.Combine(MEDirectories.GetExecutableFolderPath(Game), "ME3ExpActorDump.txt");
            if (!File.Exists(actorDumpPath))
            {
                return;
            }

            var actors = new Dictionary<string, List<ActorEntry2>>();
            string[] lines = File.ReadAllLines(actorDumpPath);
            Dictionary<string, string> gameFiles = MELoadedFiles.GetFilesLoadedInGame(Game);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split(':');
                if (parts.Length == 2 && gameFiles.ContainsKey($"{parts[0]}.pcc"))
                {
                    actors.AddToListAt($"{parts[0]}.pcc", new ActorEntry2
                    {
                        ActorName = parts[1],
                        FileName = $"{parts[0]}.pcc",
                        ActorListIndex = i
                    });
                }
            }

            foreach ((string fileName, List<ActorEntry2> actorEntries) in actors)
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(gameFiles[fileName]);
                if (pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is { } levelExport)
                {
                    Level levelBin = levelExport.GetBinaryData<Level>();
                    var uIndices = levelBin.Actors.Where(uIndex => pcc.IsUExport(uIndex)).ToList();
                    List<string> instancedNames = uIndices.Select(uIndex => pcc.GetUExport(uIndex).ObjectName.Instanced).ToList();
                    foreach (ActorEntry2 actorEntry in actorEntries)
                    {
                        if (instancedNames.IndexOf(actorEntry.ActorName) is int idx && idx >= 0)
                        {
                            actorEntry.UIndex = uIndices[idx];
                            ActorDict.AddToListAt(fileName, actorEntry);
                        }
                    }
                }
            }
        }

        #region BusyHost

        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyText;

        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        private ICommand _cancelBusyCommand;

        public ICommand CancelBusyCommand
        {
            get => _cancelBusyCommand;
            set => SetProperty(ref _cancelBusyCommand, value);
        }

        public void SetBusy(string busyText, Action onCancel = null)
        {
            BusyText = busyText;
            if (onCancel != null)
            {
                CancelBusyCommand = new GenericCommand(() =>
                {
                    onCancel();
                    EndBusy();
                }, () => true);
            }
            else
            {
                CancelBusyCommand = new DisabledCommand();
            }

            IsBusy = true;
        }

        public void EndBusy()
        {
            IsBusy = false;
        }

        #endregion

        private ActorEntry2 _selectedActor;

        public ActorEntry2 SelectedActor
        {
            get => _selectedActor;
            set
            {
                if (SetProperty(ref _selectedActor, value) && !noUpdate && value != null)
                {
                    SetBusy($"Selecting {value.ActorName}", () => { });
                    InteropHelper.SendMessageToGame($"LLE_GET_ACTOR_POSDATA {Path.GetFileNameWithoutExtension(value.FileName)} TheWorld.PersistentLevel.{value.ActorName}", Game);
                }
            }
        }

        #region Position/Rotation
        private static readonly Vector3 defaultPosition = Vector3.Zero;
        private readonly Rotator defaultRotation = new(0, 0, 0);
        private int _xPos = (int)defaultPosition.X;
        public int XPos
        {
            get => _xPos;
            set
            {
                if (SetProperty(ref _xPos, value))
                {
                    UpdateLocation();
                }
            }
        }

        private int _yPos = (int)defaultPosition.Y;
        public int YPos
        {
            get => _yPos;
            set
            {
                if (SetProperty(ref _yPos, value))
                {
                    UpdateLocation();
                }
            }
        }

        private int _zPos = (int)defaultPosition.Z;
        public int ZPos
        {
            get => _zPos;
            set
            {
                if (SetProperty(ref _zPos, value))
                {
                    UpdateLocation();
                }
            }
        }

        private int _posIncrement = 10;
        public int PosIncrement
        {
            get => _posIncrement;
            set => SetProperty(ref _posIncrement, value);
        }

        private int _pitch;
        public int Pitch
        {
            get => _pitch;
            set
            {
                if (SetProperty(ref _pitch, value))
                {
                    UpdateRotation();
                }
            }
        }

        private int _yaw;
        public int Yaw
        {
            get => _yaw;
            set
            {
                if (SetProperty(ref _yaw, value))
                {
                    UpdateRotation();
                }
            }
        }

        private int _roll;
        public int Roll
        {
            get => _roll;
            set
            {
                if (SetProperty(ref _roll, value))
                {
                    UpdateRotation();
                }
            }
        }

        private int _rotIncrement = 5;
        public int RotIncrement
        {
            get => _rotIncrement;
            set => SetProperty(ref _rotIncrement, value);
        }

        private enum FloatVarIndexes
        {
            XPos = 1,
            YPos = 2,
            ZPos = 3,
            XRotComponent = 4,
            YRotComponent = 5,
            ZRotComponent = 6,
        }

        private enum BoolVarIndexes
        {
        }

        private enum IntVarIndexes
        {
            ActorArrayIndex = 1,
            ME3Pitch = 2,
            ME3Yaw = 3,
            ME3Roll = 4,
        }

        private bool noUpdate;
        private void UpdateLocation()
        {
            if (noUpdate) return;
            GameTarget.ExecuteConsoleCommands(VarCmd(XPos, FloatVarIndexes.XPos),
                                                     VarCmd(YPos, FloatVarIndexes.YPos),
                                                     VarCmd(ZPos, FloatVarIndexes.ZPos),
                                                     "ce SetLocation");
        }

        private void UpdateRotation()
        {
            if (noUpdate) return;

            int pitch = ((float)Pitch).DegreesToUnrealRotationUnits();
            int yaw = ((float)Yaw).DegreesToUnrealRotationUnits();
            if (Game is MEGame.ME3)
            {
                int roll = ((float)Roll).DegreesToUnrealRotationUnits();
                GameTarget.ExecuteConsoleCommands(VarCmd(pitch, IntVarIndexes.ME3Pitch),
                                                      VarCmd(yaw, IntVarIndexes.ME3Yaw),
                                                      VarCmd(roll, IntVarIndexes.ME3Roll),
                                                      "ce SetRotation");
            }
            else
            {
                var rot = new Rotator(pitch, yaw, 0).GetDirectionalVector();
                GameTarget.ExecuteConsoleCommands(VarCmd(rot.X, FloatVarIndexes.XRotComponent),
                                                      VarCmd(rot.Y, FloatVarIndexes.YRotComponent),
                                                      VarCmd(rot.Z, FloatVarIndexes.ZRotComponent),
                                                      "ce SetRotation");
            }
        }

        private static string VarCmd(float value, FloatVarIndexes index)
        {
            return $"initplotmanagervaluebyindex {(int)index} float {value}";
        }

        private static string VarCmd(bool value, BoolVarIndexes index)
        {
            return $"initplotmanagervaluebyindex {(int)index} bool {(value ? 1 : 0)}";
        }

        private static string VarCmd(int value, IntVarIndexes index)
        {
            return $"initplotmanagervaluebyindex {(int)index} int {value}";
        }

        #endregion

        #region CamPath
        private const int CamPath_InterpDataIDX = 63;
        private const int CamPath_LoopGateIDX = 77;
        private const int CamPath_InterpTrackMoveIDX = 70;
        private const int CamPath_FOVTrackIDX = 82;

        private IMEPackage camPathPackage;

        public ExportEntry interpTrackMove { get; set; }
        public ExportEntry fovTrackExport { get; set; }

        private FileSystemWatcher savedCamsFileWatcher;

        private EFontAwesomeIcon _playPauseImageSource = EFontAwesomeIcon.Solid_Play;
        public EFontAwesomeIcon PlayPauseIcon
        {
            get => _playPauseImageSource;
            set => SetProperty(ref _playPauseImageSource, value);
        }

        private enum PlaybackState
        {
            Playing, Stopped, Paused
        }

        private PlaybackState playbackState = PlaybackState.Stopped;

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (noUpdate) return;
            switch (playbackState)
            {
                case PlaybackState.Playing:
                    pauseCam();
                    break;
                case PlaybackState.Stopped:
                case PlaybackState.Paused:
                    playCam();
                    break;
            }
        }

        private void playCam()
        {
            playbackState = PlaybackState.Playing;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Pause;
            GameTarget.ExecuteConsoleCommands("ce playcam");
        }

        private void pauseCam()
        {
            playbackState = PlaybackState.Paused;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            GameTarget.ExecuteConsoleCommands("ce pausecam");
        }

        private bool _shouldLoop;

        public bool ShouldLoop
        {
            get => _shouldLoop;
            set
            {
                if (SetProperty(ref _shouldLoop, value) && !noUpdate)
                {
                    GameTarget.ExecuteConsoleCommands(_shouldLoop ? "ce loopcam" : "ce noloopcam");
                }
            }
        }

        private void StopAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (noUpdate) return;
            playbackState = PlaybackState.Stopped;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            GameTarget.ExecuteConsoleCommands("ce stopcam");
        }

        private void SaveCamPath(object sender, RoutedEventArgs e)
        {
            camPathPackage.GetUExport(CamPath_InterpDataIDX).WriteProperty(new FloatProperty(Math.Max(Move_CurveEditor.Time, FOV_CurveEditor.Time), "InterpLength"));
            camPathPackage.GetUExport(CamPath_LoopGateIDX).WriteProperty(new BoolProperty(ShouldLoop, "bOpen"));
            camPathPackage.Save();
            LiveEditHelper.PadCamPathFile(Game);
            GameTarget.ExecuteConsoleCommands("ce stopcam", "ce LoadCamPath");
            playbackState = PlaybackState.Stopped;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
        }

        private void InitializeCamPath()
        {
            if (Game is not MEGame.ME3)
            {
                return;
            }
            camPathPackage = MEPackageHandler.OpenMEPackage(LiveEditHelper.CamPathFilePath(Game));
            interpTrackMove = camPathPackage.GetUExport(CamPath_InterpTrackMoveIDX);
            fovTrackExport = camPathPackage.GetUExport(CamPath_FOVTrackIDX);
            ReloadCurveEdExports();

            savedCamsFileWatcher = new FileSystemWatcher(MEDirectories.GetExecutableFolderPath(Game), "savedCams") { NotifyFilter = NotifyFilters.LastWrite };
            savedCamsFileWatcher.Changed += SavedCamsFileWatcher_Changed;
            savedCamsFileWatcher.EnableRaisingEvents = true;

            ReloadCams();
        }

        private void ReloadCurveEdExports()
        {
            Move_CurveEditor.LoadExport(interpTrackMove);
            FOV_CurveEditor.LoadExport(fovTrackExport);
        }

        private void SavedCamsFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            //needs to be on the UI thread 
            Dispatcher.BeginInvoke(new Action(ReloadCams));
        }

        private void ReloadCams() => SavedCams.ReplaceAll(LiveEditHelper.ReadSavedCamsFile());

        private void DisposeCamPath()
        {
            camPathPackage?.Dispose();
            camPathPackage = null;
            savedCamsFileWatcher?.Dispose();
        }

        public ObservableCollectionExtended<POV> SavedCams { get; } = new();

        private void AddSavedCamAsKey(object sender, RoutedEventArgs e)
        {
            string timeStr = PromptDialog.Prompt(this, "Add key at what time?", "", "0", true);

            if (float.TryParse(timeStr, out float time))
            {
                var pov = (POV)((System.Windows.Controls.Button)sender).DataContext;

                var props = interpTrackMove.GetProperties();
                var interpCurvePos = InterpCurveVector.FromStructProperty(props.GetProp<StructProperty>("PosTrack"), Game);
                var interpCurveRot = InterpCurveVector.FromStructProperty(props.GetProp<StructProperty>("EulerTrack"), Game);

                interpCurvePos.AddPoint(time, pov.Position, Vector3.Zero, Vector3.Zero, EInterpCurveMode.CIM_CurveUser);
                interpCurveRot.AddPoint(time, pov.Rotation, Vector3.Zero, Vector3.Zero, EInterpCurveMode.CIM_CurveUser);

                props.AddOrReplaceProp(interpCurvePos.ToStructProperty(Game, "PosTrack"));
                props.AddOrReplaceProp(interpCurveRot.ToStructProperty(Game, "EulerTrack"));
                interpTrackMove.WriteProperties(props);

                var floatTrack = InterpCurveFloat.FromStructProperty(fovTrackExport.GetProperty<StructProperty>("FloatTrack"), Game);
                floatTrack.AddPoint(time, pov.FOV, 0, 0, EInterpCurveMode.CIM_CurveUser);
                fovTrackExport.WriteProperty(floatTrack.ToStructProperty(Game, "FloatTrack"));

                ReloadCurveEdExports();
            }
        }

        #endregion

        private void ClearKeys(object sender, RoutedEventArgs e)
        {
            if (MessageBoxResult.Yes == MessageBox.Show("Are you sure you want to clear all keys from the Curve Editors?", "Clear Keys confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning))
            {
                var props = interpTrackMove.GetProperties();
                props.AddOrReplaceProp(new InterpCurveVector().ToStructProperty(Game, "PosTrack"));
                props.AddOrReplaceProp(new InterpCurveVector().ToStructProperty(Game, "EulerTrack"));
                interpTrackMove.WriteProperties(props);

                fovTrackExport.WriteProperty(new InterpCurveFloat().ToStructProperty(Game, "FloatTrack"));

                ReloadCurveEdExports();
            }
        }
    }
    public class ActorEntry2
    {
        public int ActorListIndex;
        public string FileName;
        public string ActorName { get; set; }

        public int UIndex;
        public bool Moveable;
    }
}