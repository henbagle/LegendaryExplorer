﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Be.Windows.Forms;
using FontAwesome5;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Tools.Soundplorer;
using LegendaryExplorer.Misc;
using LegendaryExplorer.UnrealExtensions;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorerCore.Audio;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;
using AudioStreamHelper = LegendaryExplorer.UnrealExtensions.AudioStreamHelper;
using WwiseStream = LegendaryExplorerCore.Unreal.BinaryConverters.WwiseStream;
using AudioInfo = LegendaryExplorerCore.Audio.AudioInfo;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for Soundpanel.xaml
    /// </summary>
    public partial class Soundpanel : ExportLoaderControl
    {
        public ObservableCollectionExtended<object> ExportInformationList { get; } = new();
        public ObservableCollectionExtended<HIRCNotableItem> HIRCNotableItems { get; } = new();
        private readonly List<EmbeddedWEMFile> AllWems = new(); //used only for rebuilding soundbank
        WwiseStream wwiseStream;
        public string afcPath = "";
        readonly DispatcherTimer seekbarUpdateTimer = new();
        private bool SeekUpdatingDueToTimer;
        private bool SeekDragging;
        Stream audioStream;
        private HexBox SoundpanelHIRC_Hexbox;
        private ReadOptimizedByteProvider hircHexProvider;

        public ISBankEntry CurrentLoadedISACTEntry { get; private set; }
        public AFCFileEntry CurrentLoadedAFCFileEntry { get; private set; }
        public WwiseBank CurrentLoadedWwisebank { get; private set; }

        /// <summary>
        /// The cached stream source is used to determine if we should unload the current vorbis stream
        /// when pressing play again after playback has been stopped.
        /// </summary>
        private object CachedStreamSource { get; set; }

        private enum PlaybackState
        {
            Playing,
            Stopped,
            Paused
        }

        private PlaybackState _playbackState;
        private bool RestartingDueToLoop;

        private SoundpanelAudioPlayer _audioPlayer;

        #region Dependency Properties

        /// <summary>
        /// The UI host that is hosting this instance of Soundpanel. This is set as busy when replacing audio.
        /// </summary>
        public IBusyUIHost HostingControl
        {
            get => (IBusyUIHost)GetValue(HostingControlProperty);
            set => SetValue(HostingControlProperty, value);
        }

        public static readonly DependencyProperty HostingControlProperty = DependencyProperty.Register(
            nameof(HostingControl), typeof(IBusyUIHost), typeof(Soundpanel));

        public ObservableCollectionExtended<HIRCDisplayObject> HIRCObjects { get; set; } = new();

        /// <summary>
        /// Sets whether audio replacement should be allowed
        /// </summary>
        public bool PlayBackOnlyMode
        {
            get => (bool)GetValue(PlayBackOnlyModeProperty);
            set => SetValue(PlayBackOnlyModeProperty, value);
        }

        public static readonly DependencyProperty PlayBackOnlyModeProperty = DependencyProperty.Register(
            nameof(PlayBackOnlyMode), typeof(bool), typeof(Soundpanel), new PropertyMetadata(default(bool), PlayBackOnlyModeChanged));

        private static void PlayBackOnlyModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Soundpanel)
            {
                //do nothing?
            }
        }

        public int HexBoxMinWidth
        {
            get => (int)GetValue(HexBoxMinWidthProperty);
            set => SetValue(HexBoxMinWidthProperty, value);
        }
        public static readonly DependencyProperty HexBoxMinWidthProperty = DependencyProperty.Register(
            nameof(HexBoxMinWidth), typeof(int), typeof(Soundpanel), new PropertyMetadata(default(int)));

        public int HexBoxMaxWidth
        {
            get => (int)GetValue(HexBoxMaxWidthProperty);
            set => SetValue(HexBoxMaxWidthProperty, value);
        }
        public static readonly DependencyProperty HexBoxMaxWidthProperty = DependencyProperty.Register(
            nameof(HexBoxMaxWidth), typeof(int), typeof(Soundpanel), new PropertyMetadata(default(int)));


        public bool MiniPlayerMode
        {
            get => (bool)GetValue(MiniPlayerModeProperty);
            set => SetValue(MiniPlayerModeProperty, value);
        }
        public static readonly DependencyProperty MiniPlayerModeProperty = DependencyProperty.Register(
            nameof(MiniPlayerMode), typeof(bool), typeof(Soundpanel), new PropertyMetadata(default(bool), MiniPlayerModeChanged));

        private static void MiniPlayerModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Soundpanel sp)
            {
                if ((bool)e.NewValue)
                {
                    // MiniPlayerMode enabled
                    sp.ExportInfoListBox.Visibility = Visibility.Collapsed;
                    foreach (var item in sp.SoundPanel_TabsControl.Items)
                        (item as TabItem).Visibility = Visibility.Collapsed;
                }   
                else
                {
                    // MiniPlayerMode disabled
                    sp.ExportInfoListBox.Visibility = Visibility.Visible;
                    foreach (var item in sp.SoundPanel_TabsControl.Items)
                        (item as TabItem).Visibility = Visibility.Visible;
                }
            }
        }

        #endregion

        #region Constructor and On_Loaded

        public Soundpanel() : base("Soundpanel")
        {
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
            LoadCommands();
            CurrentVolume = 0.65f;
            _playbackState = PlaybackState.Stopped;
            seekbarUpdateTimer.Interval = new TimeSpan(0, 0, 0, 0, 250);
            seekbarUpdateTimer.Tick += UpdateSeekBarPos;
            InitializeComponent();
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new Soundpanel(), CurrentLoadedExport)
                {
                    Title = $"Sound Player - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}",
                    Height = 400,
                    Width = 400
                };
                elhw.Show();
            }
        }

        public override void PoppedOut(MenuItem recentsMenuItem)
        {
            //todo: improve ui layout on popout
        }

        private bool ControlLoaded;

        private void Soundpanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (!ControlLoaded)
            {
                SoundpanelHIRC_Hexbox = (HexBox)HIRC_Hexbox_Host.Child;
                hircHexProvider = new ReadOptimizedByteProvider();

                SoundpanelHIRC_Hexbox.ByteProvider = hircHexProvider;
                SoundpanelHIRC_Hexbox.ByteProvider.Changed += SoundpanelHIRC_Hexbox_BytesChanged;

                this.bind(HexBoxMinWidthProperty, SoundpanelHIRC_Hexbox, nameof(SoundpanelHIRC_Hexbox.MinWidth));
                this.bind(HexBoxMaxWidthProperty, SoundpanelHIRC_Hexbox, nameof(SoundpanelHIRC_Hexbox.MaxWidth));

                ControlLoaded = true;
            }
        }

        #endregion

        #region Binding Vars

        private bool _repeating;
        public bool Repeating
        {
            get => _repeating;
            set => SetProperty(ref _repeating, value);
        }

        private EFontAwesomeIcon _playPauseImageSource;
        public EFontAwesomeIcon PlayPauseIcon
        {
            get => _playPauseImageSource;
            set => SetProperty(ref _playPauseImageSource, value);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private float _currentVolume;
        public float CurrentVolume
        {
            get => _currentVolume;
            set => SetProperty(ref _currentVolume, value);
        }

        private double _currentTrackLength;
        public double CurrentTrackLength
        {
            get => _currentTrackLength;
            set => SetProperty(ref _currentTrackLength, value);
        }

        private double _currentTrackPosition;
        public double CurrentTrackPosition
        {
            get => _currentTrackPosition;
            set
            {
                if (value.Equals(_currentTrackPosition)) return;
                //Debug.WriteLine("trackpos: " + value);
                _currentTrackPosition = value;
                SeekUpdatingDueToTimer = true;
                OnPropertyChanged(nameof(CurrentTrackPosition));
                SeekUpdatingDueToTimer = false;
            }
        }

        private bool _hircHexChanged;
        public bool HIRCHexChanged
        {
            get => _hircHexChanged;
            private set => SetProperty(ref _hircHexChanged, value);
        }

        private string _searchStatusText;
        public string SearchStatusText
        {
            get => _searchStatusText;
            private set => SetProperty(ref _searchStatusText, value);
        }
        
        #endregion

        #region Commands

        public ICommand ReplaceAudioCommand { get; set; }
        public ICommand ExportAudioCommand { get; set; }
        public ICommand StartPlaybackCommand { get; set; }
        public ICommand StopPlaybackCommand { get; set; }

        public ICommand TrackControlMouseDownCommand { get; set; }
        public ICommand TrackControlMouseUpCommand { get; set; }
        public ICommand VolumeControlValueChangedCommand { get; set; }
        public ICommand CommitCommand { get; set; }
        public ICommand SearchHIRCHexCommand { get; private set; }
        public ICommand SaveHIRCHexCommand { get; private set; }
        public RelayCommand PlayHIRCCommand { get; set; }

        private void LoadCommands()
        {
            // Player commands
            ReplaceAudioCommand = new RelayCommand(ReplaceAudio, CanReplaceAudio);
            ExportAudioCommand = new RelayCommand(ExportAudio, CanExportAudio);
            StartPlaybackCommand = new GenericCommand(StartPlayback, CanStartPlayback);
            StopPlaybackCommand = new RelayCommand(StopPlayback, CanStopPlayback);

            // Event commands
            TrackControlMouseDownCommand = new RelayCommand(TrackControlMouseDown, CanTrackControlMouseDown);
            TrackControlMouseUpCommand = new RelayCommand(TrackControlMouseUp, CanTrackControlMouseUp);
            VolumeControlValueChangedCommand = new GenericCommand(VolumeControlValueChanged);

            //WwisebankEditor commands
            CommitCommand = new GenericCommand(CommitBankToFile, CanCommitBankToFile);
            SearchHIRCHexCommand = new GenericCommand(SearchHIRCHex, CanSearchHIRCHex);
            SaveHIRCHexCommand = new GenericCommand(SaveHIRCHex, CanSaveHIRCHex);

            // HIRC commands
            PlayHIRCCommand = new RelayCommand(PlayHIRC, CanPlayHIRC);
        }
        
        private bool CanCommitBankToFile() => HasPendingHIRCChanges;

        private void CommitBankToFile()
        {
            // byte[] dataBefore = CurrentLoadedWwisebank.Export.Data;
            CurrentLoadedWwisebank.HIRCObjects.Clear();
            CurrentLoadedWwisebank.HIRCObjects.AddRange(HIRCObjects.Select(x => new KeyValuePair<uint, WwiseBank.HIRCObject>(x.ID, CreateHircObjectFromHex(x.Data))));

            // We must restore the original wem datas. In preloading entries, the length on the RIFF is the actual full length. But the data on disk is only like .1s long. 
            // wwise does some trickery to load the rest of the audio later but we don't have that kind of code so we interally adjust it for local playback
            CurrentLoadedWwisebank.EmbeddedFiles.ReplaceAll(AllWems.Select(w => new KeyValuePair<uint, byte[]>(w.Id, w.HasBeenFixed ? w.OriginalWemData : w.WemData)));
            CurrentLoadedExport.WriteBinary(CurrentLoadedWwisebank);
            foreach (var hircObject in HIRCObjects)
            {
                hircObject.DataChanged = false;
            }
            //byte[] dataAfter = CurrentLoadedWwisebank.Export.Data;

            //if (dataBefore.Length == dataAfter.Length)
            //{
            //    for (int i = 0; i < dataAfter.Length; i++)
            //    {
            //        if (dataAfter[i] != dataBefore[i])
            //        {
            //            MessageBox.Show($@"Commited data has changed! Change starts at 0x{i:X8}");
            //            break;
            //        }
            //    }
            //}

            //CurrentLoadedWwisebank.Export.Data = dataBefore;
        }

        #endregion

        #region Export Loading (WwiseBank, WwiseStream, SoundNodeWave)

        public override void LoadExport(ExportEntry exportEntry)
        {
            try
            {
                ExportInformationList.ClearEx();
                AllWems.Clear();
                CurrentLoadedWwisebank = null;
                if (exportEntry.ClassName == "WwiseStream")
                {
                    ExportInformationList.Add($"#{exportEntry.UIndex} {exportEntry.ClassName} : {exportEntry.ObjectName.Instanced}");
                    SoundPanel_TabsControl.SelectedItem = SoundPanel_PlayerTab;
                    WwiseStream w = exportEntry.GetBinaryData<WwiseStream>();
                    ExportInformationList.Add($"Filename : {w.Filename ?? "Stored in this package"}");
                    if (!PlayBackOnlyMode)
                    {
                        ExportInformationList.Add($"Data size: {w.DataSize} bytes");
                        ExportInformationList.Add($"Data offset: 0x{w.DataOffset:X8}");
                        string wemId = $"ID: 0x{w.Id:X8}";
                        if (ShouldReverseIDEndianness)
                        {
                            wemId += $" | 0x{ReverseBytes((uint)w.Id):X8} (Reversed)";
                        }

                        ExportInformationList.Add(wemId);
                    }

                    if (w.Filename != null && !PlayBackOnlyMode)
                    {
                        try
                        {
                            var samefolderpath = Directory.GetParent(exportEntry.FileRef.FilePath);
                            string afcPath = Path.Combine(samefolderpath.FullName, w.Filename + ".afc");
                            var headerbytes = new byte[0x56];
                            bool bytesread = false;
                            if (!File.Exists(afcPath))
                            {
                                afcPath = w.GetPathToAFC();
                            }
                            if (File.Exists(afcPath))
                            {
                                using FileStream fs = new FileStream(afcPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                                fs.Seek(w.DataOffset, SeekOrigin.Begin);
                                fs.Read(headerbytes, 0, 0x56);
                                bytesread = true;
                            }

                            if (bytesread)
                            {
                                //Parse it
                                ExportInformationList.Add("---------Referenced Audio Header----------");
                                ASCIIEncoding ascii = new ASCIIEncoding();
                                var riffTag = ascii.GetString(headerbytes, 0, 4);
                                Endian endian = Endian.Little;
                                if (riffTag == "RIFF") endian = Endian.Little;
                                if (riffTag == "RIFX") endian = Endian.Big;

                                ExportInformationList.Add("0x00 RIFF tag: " + riffTag);
                                ExportInformationList.Add("0x04 File size: " + EndianReader.ToInt32(headerbytes, 4, endian) + " bytes");
                                ExportInformationList.Add("0x08 WAVE tag: " + ascii.GetString(headerbytes, 8, 4));
                                ExportInformationList.Add("0x0C Format tag: " + ascii.GetString(headerbytes, 0xC, 4));
                                ExportInformationList.Add("0x10 Format size: " + GetHexForUI(headerbytes, 0x10, 4, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x14 Codec ID: " + GetHexForUI(headerbytes, 0x14, 2, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x16 Channel count: " + GetHexForUI(headerbytes, 0x16, 2, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x18 Sample rate: " + GetHexForUI(headerbytes, 0x18, 4, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x1C Average bits per second: " + GetHexForUI(headerbytes, 0x1C, 2, exportEntry.FileRef.Endian));

                                ExportInformationList.Add("0x20 Unknown 6: " + GetHexForUI(headerbytes, 0x20, 4, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x24 Unknown 7: " + GetHexForUI(headerbytes, 0x24, 2, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x26 Unknown 8: " + GetHexForUI(headerbytes, 0x26, 2, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x28 Unknown 9: " + GetHexForUI(headerbytes, 0x28, 4, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x2C Unknown 10: " + GetHexForUI(headerbytes, 0x2C, 2, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x2E Unknown 11: " + GetHexForUI(headerbytes, 0x2E, 2, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x30 Unknown 12: " + GetHexForUI(headerbytes, 0x30, 4, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x34 Unknown 13: " + GetHexForUI(headerbytes, 0x34, 4, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x38 Unknown 14: " + GetHexForUI(headerbytes, 0x38, 2, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x3A Unknown 15: " + GetHexForUI(headerbytes, 0x3A, 2, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x3C Unknown 16: " + GetHexForUI(headerbytes, 0x3C, 4, exportEntry.FileRef.Endian));

                                ExportInformationList.Add("0x40 Unknown 17: " + GetHexForUI(headerbytes, 0x40, 4, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x44 Unknown 18: " + GetHexForUI(headerbytes, 0x44, 2, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x46 Unknown 19: " + GetHexForUI(headerbytes, 0x46, 2, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x48 Unknown 20: " + GetHexForUI(headerbytes, 0x48, 4, exportEntry.FileRef.Endian));
                                ExportInformationList.Add("0x4C Unknown 21: " + GetHexForUI(headerbytes, 0x4C, 4, exportEntry.FileRef.Endian));

                                ExportInformationList.Add("0x50-56 Fully unknown: " + GetHexForUI(headerbytes, 0x50, 6, exportEntry.FileRef.Endian));
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    CurrentLoadedExport = exportEntry;
                }

                if (exportEntry.ClassName == "WwiseBank")
                {
                    WwiseBank wb = CurrentLoadedWwisebank = exportEntry.GetBinaryData<WwiseBank>();
                    ExportInformationList.Add($"#{exportEntry.UIndex} {exportEntry.ClassName} : {exportEntry.ObjectName.Instanced} (Bank ID 0x{wb.ID:X8})");

                    HIRCObjects.Clear();
                    HIRCObjects.AddRange(wb.HIRCObjects.Values().Select((ho, i) => new HIRCDisplayObject(i, ho, exportEntry.Game)));

                    if (wb.EmbeddedFiles.Count > 0)
                    {
                        int i = 0;
                        foreach ((uint id, byte[] bytes) in wb.EmbeddedFiles)
                        {
                            string wemId = id.ToString("X8");
                            if (ShouldReverseIDEndianness)
                            {
                                wemId = $"{ReverseBytes(id):X8} (Reversed)";
                            }

                            string wemHeader = $"{(char)bytes[0]}{(char)bytes[1]}{(char)bytes[2]}{(char)bytes[3]}";
                            string wemName = $"{i}: Embedded WEM 0x{wemId}";
                            EmbeddedWEMFile wem = new EmbeddedWEMFile(bytes, wemName, exportEntry, id);
                            if (wemHeader == "RIFF" || wemHeader == "RIFX")
                            {
                                ExportInformationList.Add(wem);
                            }
                            else
                            {
                                ExportInformationList.Add($"{wemName} - No RIFF/RIFX header ({wemHeader})");
                            }

                            AllWems.Add(wem);
                            i++;
                        }
                    }
                    else
                    {
                        ExportInformationList.Add("This soundbank has no embedded WEM files");
                    }

                    CurrentLoadedExport = exportEntry;

                    //This makes the hexbox widen by 1 and then shrink by 1
                    //For some rason it won't calculate the scrollbar again unless you do this
                    //which is very annoying.
                    var currentWidth = HIRC_Hexbox_Host.Width;
                    if (currentWidth > 500)
                    {
                        SoundpanelHIRC_Hexbox.Width -= 1;
                        HIRC_Hexbox_Host.UpdateLayout();
                        SoundpanelHIRC_Hexbox.Width += 1;
                    }
                    else
                    {
                        SoundpanelHIRC_Hexbox.Width += 1;
                        HIRC_Hexbox_Host.UpdateLayout();
                        SoundpanelHIRC_Hexbox.Width -= 1;
                    }

                    HIRC_Hexbox_Host.UpdateLayout();
                    SoundpanelHIRC_Hexbox.Select(0, 1);
                    SoundpanelHIRC_Hexbox.ScrollByteIntoView();
                }

                if (exportEntry.ClassName == "SoundNodeWave")
                {
                    ExportInformationList.Add($"#{exportEntry.UIndex} {exportEntry.ClassName} : {exportEntry.ObjectName.Instanced}");
                    var soundNodeWave = exportEntry.GetBinaryData<SoundNodeWave>();
                    if (soundNodeWave.RawData.Length > 0)
                    {
                        ISBank isb = new ISBank(soundNodeWave.RawData);
                        foreach (ISBankEntry isbe in isb.BankEntries)
                        {
                            if (isbe.DataAsStored != null)
                            {
                                ExportInformationList.Add(isbe);
                            }
                            else
                            {
                                ExportInformationList.Add($"{isbe.FileName} - No data - Data Location: 0x{isbe.DataOffset:X8}");
                            }
                        }
                        ExportInfoListBox.SelectedItem = isb.BankEntries.FirstOrDefault();
                    }
                    else
                    {
                        ExportInformationList.Add("This export contains no embedded audio");
                    }

                    CurrentLoadedExport = exportEntry;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error: " + e.Message);
            }
        }

        public override void UnloadExport()
        {
            //throw new NotImplementedException();
            //waveOut.Stop();
            //CurrentVorbisStream.Dispose();
            //_audioPlayer.Dispose();
            //infoTextBox.Text = "Select an export";
            CurrentLoadedExport = null;
        }

        public static bool CanParseStatic(ExportEntry exportEntry)
        {
            return (exportEntry.FileRef.Game.IsGame1() && exportEntry.ClassName == "SoundNodeWave") || 
                   (!exportEntry.FileRef.Game.IsGame1() && (exportEntry.ClassName == "WwiseBank" || exportEntry.ClassName == "WwiseStream"));
        }

        public override bool CanParse(ExportEntry exportEntry) => CanParseStatic(exportEntry);

        #endregion

        #region AFC Entry Loading

        internal void LoadAFCEntry(AFCFileEntry aEntry)
        {
            ExportInformationList.ClearEx();
            AllWems.Clear();

            ExportInformationList.Add($"Audio file in Audio File Cache");
            ExportInformationList.Add($"Filename : {aEntry.AFCPath}");
            ExportInformationList.Add($"Data size: {aEntry.DataSize} bytes");
            ExportInformationList.Add($"Data offset: 0x{aEntry.Offset:X8}");

            byte[] headerbytes = new byte[0x56];
            bool bytesread = false;

            try
            {
                if (File.Exists(aEntry.AFCPath))
                {
                    using (FileStream fs = new FileStream(aEntry.AFCPath, FileMode.Open))
                    {
                        fs.Seek(aEntry.Offset, SeekOrigin.Begin);
                        fs.Read(headerbytes, 0, 0x56);
                        bytesread = true;
                    }
                }

                if (bytesread)
                {
                    //Parse it
                    ExportInformationList.Add($"---------Wwise Audio Header----------");
                    ASCIIEncoding ascii = new ASCIIEncoding();
                    var riffTag = ascii.GetString(headerbytes, 0, 4);
                    Endian endian = Endian.Little;
                    if (riffTag == "RIFF") endian = Endian.Little;
                    if (riffTag == "RIFX") endian = Endian.Big;


                    ExportInformationList.Add("0x00 RIFF tag: " + riffTag);
                    ExportInformationList.Add("0x04 File size: " + EndianReader.ToInt32(headerbytes, 4, endian) + " bytes");
                    ExportInformationList.Add("0x08 WAVE tag: " + ascii.GetString(headerbytes, 8, 4));
                    ExportInformationList.Add("0x0C Format tag: " + ascii.GetString(headerbytes, 0xC, 4));
                    ExportInformationList.Add("0x10 Unknown 1: " + GetHexForUI(headerbytes, 0x10, 4, endian));
                    ExportInformationList.Add("0x14 Unknown 2: " + GetHexForUI(headerbytes, 0x14, 2, endian));
                    ExportInformationList.Add("0x16 Unknown 3: " + GetHexForUI(headerbytes, 0x16, 2, endian));
                    ExportInformationList.Add("0x18 Sample rate: " + GetHexForUI(headerbytes, 0x18, 4, endian));
                    ExportInformationList.Add("0x1C Unknown 5: " + GetHexForUI(headerbytes, 0x1C, 4, endian));

                    ExportInformationList.Add("0x20 Unknown 6: " + GetHexForUI(headerbytes, 0x20, 4, endian));
                    ExportInformationList.Add("0x24 Unknown 7: " + GetHexForUI(headerbytes, 0x24, 2, endian));
                    ExportInformationList.Add("0x26 Unknown 8: " + GetHexForUI(headerbytes, 0x26, 2, endian));
                    ExportInformationList.Add("0x28 Unknown 9: " + GetHexForUI(headerbytes, 0x28, 4, endian));
                    ExportInformationList.Add("0x2C Unknown 10: " + GetHexForUI(headerbytes, 0x2C, 2, endian));
                    ExportInformationList.Add("0x2E Unknown 11: " + GetHexForUI(headerbytes, 0x2E, 2, endian));
                    ExportInformationList.Add("0x30 Unknown 12: " + GetHexForUI(headerbytes, 0x30, 4, endian));
                    ExportInformationList.Add("0x34 Unknown 13: " + GetHexForUI(headerbytes, 0x34, 4, endian));
                    ExportInformationList.Add("0x38 Unknown 14: " + GetHexForUI(headerbytes, 0x38, 2, endian));
                    ExportInformationList.Add("0x3A Unknown 15: " + GetHexForUI(headerbytes, 0x3A, 2, endian));
                    ExportInformationList.Add("0x3C Unknown 16: " + GetHexForUI(headerbytes, 0x3C, 4, endian));

                    ExportInformationList.Add("0x40 Unknown 17: " + GetHexForUI(headerbytes, 0x40, 4, endian));
                    ExportInformationList.Add("0x44 Unknown 18: " + GetHexForUI(headerbytes, 0x44, 2, endian));
                    ExportInformationList.Add("0x46 Unknown 19: " + GetHexForUI(headerbytes, 0x46, 2, endian));
                    ExportInformationList.Add("0x48 Unknown 20: " + GetHexForUI(headerbytes, 0x48, 4, endian));
                    ExportInformationList.Add("0x4C Unknown 21: " + GetHexForUI(headerbytes, 0x4C, 4, endian));

                    ExportInformationList.Add("0x50-56 Fully unknown: " + GetHexForUI(headerbytes, 0x50, 6, endian));
                    CurrentLoadedAFCFileEntry = aEntry;
                }
            }
            catch
            {

            }

        }
        
        internal void UnloadAFCEntry()
        {
            CurrentLoadedAFCFileEntry = null;
        }

        #endregion

        #region ISACT Entry Loading

        internal void LoadISACTEntry(ISBankEntry entry)
        {
            try
            {
                ExportInformationList.Clear();
                AllWems.Clear();

                ExportInformationList.Add(entry.FileName);
                ExportInformationList.Add($"Codec: {entry.getCodecStr()}");
                ExportInformationList.Add($"Datastream size: {entry.DataAsStored.Length} bytes");
                ExportInformationList.Add($"Datastream offset: 0x{entry.DataOffset:X8}");

                CurrentLoadedISACTEntry = entry;
            }
            catch
            {

            }
        }

        internal void UnloadISACTEntry()
        {
            CurrentLoadedISACTEntry = null;
        }

        #endregion

        #region Audio Playback

        /// <summary>
        /// Gets a PCM stream of data (WAV) from either the currently loaded export or selected WEM
        /// </summary>
        /// <param name="forcedWemFile">WEM that we will force to get a stream for</param>
        /// <returns></returns>
        public Stream GetPCMStream(ExportEntry forcedWwiseStreamExport = null, EmbeddedWEMFile forcedWemFile = null)
        {
            if (CurrentLoadedISACTEntry != null)
            {
                return AudioStreamHelper.GetWaveStreamFromISBEntry(CurrentLoadedISACTEntry);
            }

            if (CurrentLoadedAFCFileEntry != null)
            {
                return AudioStreamHelper.CreateWaveStreamFromRaw(CurrentLoadedAFCFileEntry.AFCPath, CurrentLoadedAFCFileEntry.Offset, CurrentLoadedAFCFileEntry.DataSize, CurrentLoadedAFCFileEntry.ME2);
            }

            ExportEntry localCurrentExport = forcedWwiseStreamExport ?? CurrentLoadedExport;
            if (localCurrentExport != null || forcedWemFile != null)
            {
                if (localCurrentExport?.ClassName == "WwiseStream")
                {
                    wwiseStream = localCurrentExport.GetBinaryData<WwiseStream>();

                    if (wwiseStream.IsPCCStored || wwiseStream.GetPathToAFC() != "")
                    {
                        return wwiseStream.CreateWaveStream();
                    }
                }
                else if (localCurrentExport?.ClassName == "SoundNodeWave")
                {
                    if (ExportInfoListBox.SelectedItem is ISBankEntry bankEntry)
                    {
                        return AudioStreamHelper.GetWaveStreamFromISBEntry(bankEntry);
                    }
                }
                else if (forcedWemFile != null || (localCurrentExport?.ClassName == "WwiseBank"))
                {
                    object currentWEMItem = forcedWemFile ?? ExportInfoListBox.SelectedItem;
                    if (currentWEMItem == null || currentWEMItem is string)
                    {
                        return null; //nothing selected, or current wem is not playable
                    }

                    var wemObject = (EmbeddedWEMFile)currentWEMItem;
                    string basePath = $"{Path.GetTempPath()}ME3EXP_SOUND_{Guid.NewGuid()}";
                    var outpath = basePath + ".wem";
                    File.WriteAllBytes(outpath, wemObject.WemData);
                    return AudioStreamHelper.ConvertRIFFToWaveVGMStream(outpath); //use vgmstream
                }
            }

            return null;
        }

        private void PlayHIRC(object obj)
        {
            if (obj is HIRCDisplayObject hirc && hirc.ObjType == 0x2)
            {
                var wems = ExportInformationList.OfType<EmbeddedWEMFile>().ToList();
                foreach (var v in wems.OrderBy(x => x.Id))
                {
                    Debug.WriteLine(v.Id.ToString("X8"));
                }

                var playItem = ExportInformationList.OfType<EmbeddedWEMFile>().FirstOrDefault(x => x.Id == hirc.AudioID);
                if (playItem != null)
                {
                    // Found the matching item
                    ExportInfoListBox.SelectedItem = playItem;
                    StopPlayback(null);
                    if (CanStartPlayback())
                    {
                        StartPlayback();
                    }
                }
            }
        }

        private bool CanPlayHIRC(object obj)
        {
            return obj is HIRCDisplayObject {ObjType: 0x2} hirc && CurrentLoadedWwisebank != null && hirc.SourceID == CurrentLoadedWwisebank.ID;
        }
        
        private void StartPlayback()
        {
            StartOrPausePlaying();
        }

        public void StartOrPausePlaying()
        {
            bool playToggle = true;
            if (_playbackState == PlaybackState.Stopped)
            {
                if (audioStream == null)
                {
                    UpdateAudioStream();
                }
                else
                {
                    if (!RestartingDueToLoop)
                    {
                        if ((CurrentLoadedISACTEntry != null && CachedStreamSource != CurrentLoadedISACTEntry) ||
                            (CurrentLoadedAFCFileEntry != null && CachedStreamSource != CurrentLoadedAFCFileEntry))
                        {
                            //invalidate the cache
                            UpdateAudioStream();
                        }

                        if (CurrentLoadedExport != null)
                        {
                            //check if cached is the same as what we want to play
                            if (CurrentLoadedExport.ClassName == "WwiseStream" && CachedStreamSource != CurrentLoadedExport)
                            {
                                //invalidate the cache
                                UpdateAudioStream();
                            }
                            else if (CurrentLoadedExport.ClassName == "WwiseBank" && CachedStreamSource != ExportInfoListBox.SelectedItem)
                            {
                                //Invalidate the cache
                                UpdateAudioStream();
                            }
                            else if (CurrentLoadedExport.ClassName == "SoundNodeWave" && CachedStreamSource != ExportInfoListBox.SelectedItem)
                            {
                                //Invalidate the cache
                                UpdateAudioStream();
                            }
                        }
                    }
                }

                //check to make sure stream has loaded before we attempt to play it
                if (audioStream != null)
                {
                    try
                    {
                        audioStream.Position = 0;
                        _audioPlayer = new SoundpanelAudioPlayer(audioStream, CurrentVolume)
                        {
                            PlaybackStopType = SoundpanelAudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile
                        };
                        _audioPlayer.PlaybackPaused += _audioPlayer_PlaybackPaused;
                        _audioPlayer.PlaybackResumed += _audioPlayer_PlaybackResumed;
                        _audioPlayer.PlaybackStopped += _audioPlayer_PlaybackStopped;
                        CurrentTrackLength = _audioPlayer.GetLengthInSeconds();
                        playToggle = true;

                        // Start the timer.  Note that this call can be made from any thread.
                        seekbarUpdateTimer.Start();
                        // Timer callback code here...
                    }
                    catch (Exception)
                    {
                        //error playing audio or initializing
                        audioStream = null;
                        playToggle = false;
                    }

                    //_audioPlayer.Play(NAudio.Wave.PlaybackState.Stopped, CurrentVolume);
                    //CurrentlyPlayingTrack = CurrentlySelectedTrack;
                }
                else
                {
                    playToggle = false;
                }
            }

            if (playToggle)
            {
                _audioPlayer.TogglePlayPause(CurrentVolume);
            }
        }

        private void UpdateAudioStream()
        {
            audioStream = GetPCMStream();
            if (CurrentLoadedISACTEntry != null)
            {
                CachedStreamSource = CurrentLoadedISACTEntry;
            }

            if (CurrentLoadedAFCFileEntry != null)
            {
                CachedStreamSource = CurrentLoadedAFCFileEntry;
            }

            if (CurrentLoadedExport != null)
            {
                switch (CurrentLoadedExport.ClassName)
                {
                    case "WwiseStream":
                        CachedStreamSource = CurrentLoadedExport;
                        break;
                    case "WwiseBank":
                    case "SoundNodeWave":
                        CachedStreamSource = ExportInfoListBox.SelectedItem;
                        break;
                }
            }
        }

        private void UpdateSeekBarPos(object state, EventArgs e)
        {
            if (!SeekDragging)
            {
                CurrentTrackPosition = _audioPlayer?.GetPositionInSeconds() ?? 0;
            }
        }


        public bool CanStartPlayback()
        {
            if (audioStream != null) return true; //looping
            if (CurrentLoadedExport == null && CurrentLoadedISACTEntry == null && CurrentLoadedAFCFileEntry == null) return false;
            if (CurrentLoadedISACTEntry != null) return true;
            if (CurrentLoadedAFCFileEntry != null) return true;
            if (CurrentLoadedExport?.ClassName == "WwiseStream") return true;

            if (CurrentLoadedExport?.ClassName == "WwiseBank")
            {
                switch (ExportInfoListBox.SelectedItem)
                {
                    case null:
                    case string _:
                        return false; //nothing selected, or current wem is not playable
                    case EmbeddedWEMFile _:
                        return true;
                }
            }

            if (CurrentLoadedExport?.ClassName == "SoundNodeWave")
            {
                switch (ExportInfoListBox.SelectedItem)
                {
                    case null:
                        return false;
                    case ISBankEntry isbe:
                        return isbe.DataAsStored != null;
                    case EmbeddedWEMFile _:
                        return true;
                }
            }

            return false;
        }

        private void StopPlayback(object p)
        {
            StopPlaying();
        }

        public void StopPlaying()
        {
            seekbarUpdateTimer.Stop();
            if (_audioPlayer != null)
            {

                _audioPlayer.PlaybackStopType = SoundpanelAudioPlayer.PlaybackStopTypes.PlaybackStoppedByUser;
                _audioPlayer.Stop();
            }

            audioStream = null;
        }

        /// <summary>
        /// Stops any playing audio and starts playing the currently selected entry
        /// </summary>
        public void StartPlayingCurrentSelection()
        {
            if(_playbackState == PlaybackState.Stopped)
            {
                StartOrPausePlaying();
            }
            else
            {
                // If there is audio playing, stop it. The new audio entry will start once the PlaybackStopped event triggers.
                seekbarUpdateTimer.Stop();
                if (_audioPlayer != null)
                {

                    _audioPlayer.PlaybackStopType = SoundpanelAudioPlayer.PlaybackStopTypes.PlaybackSwitchedToNewFile;
                    _audioPlayer.Stop();
                }

                audioStream = null;
            }
        }

        private bool CanStopPlayback(object p) => _playbackState == PlaybackState.Playing || _playbackState == PlaybackState.Paused || audioStream != null;

        // Events
        private void TrackControlMouseDown(object p) => _audioPlayer?.Pause();

        private void TrackControlMouseUp(object p)
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.SetPosition(CurrentTrackPosition);
                _audioPlayer.Play(NAudio.Wave.PlaybackState.Paused, CurrentVolume);
            }
        }

        private bool CanTrackControlMouseDown(object p) => _playbackState == PlaybackState.Playing;

        private bool CanTrackControlMouseUp(object p) => _playbackState == PlaybackState.Paused;

        private void VolumeControlValueChanged() => _audioPlayer?.SetVolume(CurrentVolume);

        private void _audioPlayer_PlaybackStopped()
        {
            _playbackState = PlaybackState.Stopped;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;

            CommandManager.InvalidateRequerySuggested();
            CurrentTrackPosition = 0;

            if (_audioPlayer.PlaybackStopType == SoundpanelAudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile && Settings.Soundpanel_LoopAudio)
            {
                RestartingDueToLoop = true;
                StartPlayback();
                RestartingDueToLoop = false;
            }
            else if (_audioPlayer.PlaybackStopType == SoundpanelAudioPlayer.PlaybackStopTypes.PlaybackSwitchedToNewFile)
            {
                StartPlayback();
            }
        }

        private void _audioPlayer_PlaybackResumed()
        {
            _playbackState = PlaybackState.Playing;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Pause;
        }

        private void _audioPlayer_PlaybackPaused()
        {
            UpdateSeekBarPos(null, null);
            _playbackState = PlaybackState.Paused;
            PlayPauseIcon = EFontAwesomeIcon.Solid_Play;
        }

        private void Seekbar_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            SeekDragging = true;
        }

        private void Seekbar_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (!SeekUpdatingDueToTimer)
            {
                if (_audioPlayer != null)
                {
                    _audioPlayer.SetPosition(CurrentTrackPosition);
                    _audioPlayer.Play(NAudio.Wave.PlaybackState.Paused, CurrentVolume);
                }
            }

            SeekDragging = false;
        }

        private void Seekbar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!SeekUpdatingDueToTimer && !SeekDragging)
            {
                if (_audioPlayer != null)
                {
                    _audioPlayer.SetPosition(CurrentTrackPosition);
                    _audioPlayer.Play(NAudio.Wave.PlaybackState.Paused, CurrentVolume);
                }
            }
        }

        private void RepeatingButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Soundpanel_LoopAudio = !Settings.Soundpanel_LoopAudio;
            Settings.Save();
        }

        public void FreeAudioResources()
        {
            StopPlaying();
            _audioPlayer?.Dispose();
        }

        #endregion

        #region Audio Exporting

        private void ExportAudio(object p)
        {
            if (CurrentLoadedExport != null)
            {
                if (CurrentLoadedExport.ClassName == "WwiseStream")
                {
                    SaveFileDialog d = new SaveFileDialog
                    {
                        Filter = "Wave PCM File|*.wav",
                        FileName = CurrentLoadedExport.ObjectName + ".wav"
                    };
                    if (d.ShowDialog() == true)
                    {
                        WwiseStream w = CurrentLoadedExport.GetBinaryData<WwiseStream>();
                        string wavPath = w.CreateWave();
                        if (wavPath != null && File.Exists(wavPath))
                        {
                            File.Copy(wavPath, d.FileName, true);
                        }

                        MessageBox.Show("Done.");
                    }
                }

                if (CurrentLoadedExport.ClassName == "WwiseBank")
                {
                    EmbeddedWEMFile currentWEMItem = (EmbeddedWEMFile)ExportInfoListBox.SelectedItem;
                    SaveFileDialog d = new SaveFileDialog
                    {
                        Filter = "Wave PCM|*.wav",
                        FileName = $"{CurrentLoadedExport.ObjectName}_0x{currentWEMItem.Id:X8}.wav"
                    };
                    if (d.ShowDialog() == true)
                    {
                        Stream ms = GetPCMStream();
                        ms.Seek(0, SeekOrigin.Begin);
                        using (FileStream fs = new FileStream(d.FileName, FileMode.OpenOrCreate))
                        {
                            ms.CopyTo(fs);
                            fs.Flush();
                        }

                        MessageBox.Show("Done.");
                    }
                }

                if (CurrentLoadedExport.ClassName == "SoundNodeWave" && ExportInfoListBox.SelectedItem is ISBankEntry bankEntry)
                {
                    SaveFileDialog d = new SaveFileDialog
                    {
                        Filter = "Wave PCM File|*.wav",
                        FileName = CurrentLoadedExport.ObjectName.Instanced.GetPathWithoutInvalids() + ".wav"
                    };
                    if (d.ShowDialog() == true)
                    {
                        MemoryStream waveStream = AudioStreamHelper.GetWaveStreamFromISBEntry(bankEntry);
                        waveStream.Seek(0, SeekOrigin.Begin);
                        using (FileStream fs = new FileStream(d.FileName, FileMode.OpenOrCreate))
                        {
                            waveStream.CopyTo(fs);
                            fs.Flush();
                        }

                        MessageBox.Show("Done.");
                    }
                }
            }

            if (CurrentLoadedISACTEntry != null)
            {
                SaveFileDialog d = new SaveFileDialog
                {
                    Filter = "Wave PCM File|*.wav",
                    FileName = CurrentLoadedISACTEntry.FileName
                };
                if (d.ShowDialog() == true)
                {
                    MemoryStream waveStream = AudioStreamHelper.GetWaveStreamFromISBEntry(CurrentLoadedISACTEntry);
                    waveStream.Seek(0, SeekOrigin.Begin);
                    using (FileStream fs = new FileStream(d.FileName, FileMode.OpenOrCreate))
                    {
                        waveStream.CopyTo(fs);
                        fs.Flush();
                    }

                    MessageBox.Show("Done.");
                }
            }

            if (CurrentLoadedAFCFileEntry != null)
            {
                string presetfilename = $"{Path.GetFileNameWithoutExtension(CurrentLoadedAFCFileEntry.AFCPath)}_{CurrentLoadedAFCFileEntry.Offset}.wav";
                SaveFileDialog d = new SaveFileDialog
                {
                    Filter = "Wave PCM File|*.wav",
                    FileName = presetfilename
                };
                if (d.ShowDialog() == true)
                {
                    Stream s = AudioStreamHelper.CreateWaveStreamFromRaw(CurrentLoadedAFCFileEntry.AFCPath, CurrentLoadedAFCFileEntry.Offset, CurrentLoadedAFCFileEntry.DataSize, CurrentLoadedAFCFileEntry.ME2);
                    using (var fileStream = File.Create(d.FileName))
                    {
                        s.Seek(0, SeekOrigin.Begin);
                        s.CopyTo(fileStream);
                    }

                    MessageBox.Show("Done.");
                }
            }

        }

        private bool CanExportAudio(object p)
        {
            if (CurrentLoadedExport == null && CurrentLoadedISACTEntry == null && CurrentLoadedAFCFileEntry == null) return false;
            if (CurrentLoadedISACTEntry != null) return true;
            if (CurrentLoadedAFCFileEntry != null) return true;
            if (CurrentLoadedExport != null)
            {
                switch (CurrentLoadedExport.ClassName)
                {
                    case "WwiseStream":
                        return true;
                    case "WwiseBank":
                        return ExportInfoListBox.SelectedItem is EmbeddedWEMFile;
                    case "SoundNodeWave":
                        return ExportInfoListBox.SelectedItem is ISBankEntry {DataAsStored: not null};
                }
            }

            return false;
        }
        

        #endregion

        #region Audio Replacement

        private bool CanReplaceAudio(object obj)
        {
            if (CurrentLoadedExport == null) return false;
            if (CurrentLoadedExport.ClassName == "WwiseStream")
            {
                return CurrentLoadedExport.FileRef.Game is MEGame.ME3 or MEGame.LE2 or MEGame.LE3;
            }

            if (CurrentLoadedExport.ClassName == "WwiseBank")
            {
                object currentWEMItem = ExportInfoListBox.SelectedItem;
                bool result = currentWEMItem != null && currentWEMItem is EmbeddedWEMFile && CurrentLoadedExport.FileRef.Game is MEGame.ME3 or MEGame.LE2 or MEGame.LE3;
                return result;
            }

            return false;
        }

        private async void ReplaceAudio(object obj)
        {
            if (CurrentLoadedExport == null) return;
            if (CurrentLoadedExport.ClassName == "WwiseStream")
            {
                await ReplaceAudioFromWave();
            }

            if (CurrentLoadedExport.ClassName == "WwiseBank")
            {
                ReplaceEmbeddedWEMFromWave();
            }
        }

        /// <summary>
        /// Checks the Wwise path for the selected game, and shows the WwisePathDialog if it is incorrect
        /// </summary>
        /// <param name="game"></param>
        public static void CheckWwisePathForGame(MEGame game)
        {
            var path = WwiseCliHandler.GetWwiseCliPath(game);
            if (string.IsNullOrEmpty(path) || !WwiseVersions.IsCorrectWwiseVersion(game, path))
            {
                SetWwisePathDialog swpd = new ();
                swpd.ShowDialog();
            }
        }

        private async void ReplaceEmbeddedWEMFromWave(string sourceFile = null, WwiseConversionSettingsPackage conversionSettings = null)
        {
            if (ExportInfoListBox.SelectedItem is EmbeddedWEMFile wemToReplace && (CurrentLoadedExport.FileRef.Game.IsGame3() || CurrentLoadedExport.FileRef.Game == MEGame.LE2))
            {
                if (sourceFile == null)
                {
                    CheckWwisePathForGame(CurrentLoadedExport.FileRef.Game);
                    OpenFileDialog d = new OpenFileDialog { Filter = "Wave PCM|*.wav" };
                    bool? res = d.ShowDialog();
                    if (res.HasValue && res.Value)
                    {
                        sourceFile = d.FileName;
                    }
                    else
                    {
                        return;
                    }

                    if (conversionSettings == null)
                    {
                        SoundReplaceOptionsDialog srod = new SoundReplaceOptionsDialog(Window.GetWindow(this), false, Pcc.Game);
                        if (srod.ShowDialog().Value)
                        {
                            conversionSettings = srod.ChosenSettings;
                        }
                        else
                        {
                            return; //user didn't choose any settings
                        }
                    }
                }

                //Convert and replace
                ReplaceEmbeddedWEMFromWwiseEncodedFile(await WwiseCliHandler.RunWwiseConversion(Pcc.Game, sourceFile, conversionSettings), wemToReplace);
            }
        }

        /// <summary>
        /// Rewrites the soundbank export with new data from the Wwise Encoded Audio file (.ogg or .wem)
        /// </summary>
        /// <param name="oggPath"></param>
        /// <param name="wem"></param>
        private void ReplaceEmbeddedWEMFromWwiseEncodedFile(string oggPath, EmbeddedWEMFile wem)
        {
            if (oggPath == null)
            {
                OpenFileDialog d = new OpenFileDialog { Filter = Pcc.Game is MEGame.ME3 ? "Wwise Encoded Ogg|*.ogg" : "Wwise Wem File|*.wem" };
                bool? res = d.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    oggPath = d.FileName;
                }
                else
                {
                    return;
                }
            }
            StopPlaying();
            MemoryStream convertedStream = new MemoryStream();
            using (var fileStream = new FileStream(oggPath, FileMode.Open))
            {
                if (Pcc.Game is MEGame.ME3)
                {
                    //Convert wwiseoggstream
                    AudioStreamHelper.ConvertWwiseOggToME3Ogg(fileStream);
                }
                else
                {
                    fileStream.CopyToEx(convertedStream, (int)fileStream.Length);
                }
            }

            //Update the EmbeddedWEMFile. As this is an object it will be updated in the references.
            if (wem.HasBeenFixed)
            {
                wem.OriginalWemData = convertedStream.ToArray();
            }
            else
            {
                wem.WemData = convertedStream.ToArray();
            }
            CurrentLoadedWwisebank.EmbeddedFiles.ReplaceAll(AllWems.Select(w => new KeyValuePair<uint, byte[]>(w.Id, w.HasBeenFixed ? w.OriginalWemData : w.WemData)));
            CurrentLoadedExport.WriteBinary(CurrentLoadedWwisebank);
            File.Delete(oggPath);
            UpdateAudioStream();
        }

        public async Task ReplaceAudioFromWave(string sourceFile = null, ExportEntry forcedExport = null, WwiseConversionSettingsPackage conversionSettings = null)
        {
            if (sourceFile == null)
            {
                CheckWwisePathForGame(Pcc.Game);
                OpenFileDialog d = new OpenFileDialog { Filter = "Wave PCM|*.wav" };
                if (d.ShowDialog() == true)
                {
                    sourceFile = d.FileName;
                }
                else
                {
                    return;
                }
            }

            if (conversionSettings == null)
            {
                SoundReplaceOptionsDialog srod = new SoundReplaceOptionsDialog(Window.GetWindow(this), Pcc.Game.IsGame3(), Pcc.Game);
                if (srod.ShowDialog() == true)
                {
                    conversionSettings = srod.ChosenSettings;
                }
                else
                {
                    return; //user didn't choose any settings
                }
            }

            //Convert and replace
            if (HostingControl != null)
            {
                HostingControl.BusyText = "Converting and replacing audio";
                HostingControl.IsBusy = true;
            }

            await Task.Run(async () =>
            {
                var conversion = await WwiseCliHandler.RunWwiseConversion(Pcc.Game, sourceFile, conversionSettings);
                ReplaceAudioFromWwiseEncodedFile(conversion, forcedExport, conversionSettings?.UpdateReferencedEvents ?? false);

            }).ContinueWithOnUIThread((a) =>
            {
                if (HostingControl != null)
                {
                    HostingControl.IsBusy = false;
                }
            });
        }

        /// <summary>
        /// Replaces the audio in the current loaded export, or the forced export. Will prompt user for a Wwise Encoded Audio file. (.ogg for ME3, .wem otherwise)
        /// </summary>
        /// <param name="forcedExport">Export to update. If null, the currently loaded one is used instead.</param>
        /// <param name="updateReferencedEvents">If true will find all WwiseEvents referencing this export and update their Duration property</param>
        public void ReplaceAudioFromWwiseEncodedFile(string filePath = null, ExportEntry forcedExport = null, bool updateReferencedEvents = false)
        {
            StopPlaying();
            ExportEntry exportToWorkOn = forcedExport ?? CurrentLoadedExport;
            if (exportToWorkOn != null && exportToWorkOn.ClassName == "WwiseStream")
            {
                WwiseStream w = exportToWorkOn.GetBinaryData<WwiseStream>();
                if (filePath == null)
                {
                    OpenFileDialog d = new OpenFileDialog { Filter = Pcc.Game is MEGame.ME3 ? "Wwise Encoded Ogg|*.ogg" : "Wwise Wem File|*.wem" };
                    bool? res = d.ShowDialog();
                    if (res.HasValue && res.Value)
                    {
                        filePath = d.FileName;
                    }
                    else
                    {
                        return;
                    }
                }

                w.ImportFromFile(filePath, w.GetPathToAFC());
                exportToWorkOn.WriteBinary(w);
                UpdateAudioStream();

                if(updateReferencedEvents)
                {
                    var ms = (float)w.GetAudioInfo().GetLength().TotalMilliseconds;
                    UpdateReferencedWwiseEventLengths(exportToWorkOn, ms);
                }
            }
        }

        /// <summary>
        /// Update the DurationMilliseconds property on all WwiseEvents that reference the given WwiseStream
        /// </summary>
        /// <param name="wwiseStreamExport"></param>
        /// <param name="streamLengthInMs">Value to update DurationMilliseconds to</param>
        public void UpdateReferencedWwiseEventLengths(ExportEntry wwiseStreamExport, float streamLengthInMs)
        {
            // LE2 has the DurationSeconds property but does not appear to be on any events, so we do nothing. I think.

            if (wwiseStreamExport.Game is MEGame.ME3)
            {
                var durationProperty = new FloatProperty(streamLengthInMs, "DurationMilliseconds");

                // Find referenced WwiseEvent exports and update the property
                var referencedExports = wwiseStreamExport.GetEntriesThatReferenceThisOne();
                foreach (ExportEntry re in referencedExports.Select(e => e.Key)
                                                            .Where(e => e.ClassName == "WwiseEvent"))
                {
                    re.WriteProperty(durationProperty);
                }
            }
            // Finding all WwiseEvent references in LE games will return several WwiseExports, some incorrect
            // so we have to look up the WwiseEvent by TLK ID
            else if (wwiseStreamExport.Game is MEGame.LE3)
            {
                var durationProperty = new FloatProperty(streamLengthInMs/1000, "DurationSeconds");
                
                var splits = wwiseStreamExport.ObjectName.Name.Split('_', ',');
                int tlkId = 0;
                bool specifyByGender = false;
                bool isFemaleStream = false;
                for (int i = splits.Length - 1; i > 0; i--)
                {
                    //backwards is faster
                    if (int.TryParse(splits[i], out var parsed))
                    {
                        tlkId = parsed;
                        specifyByGender = wwiseStreamExport.ObjectName.Name.Contains("player_", StringComparison.OrdinalIgnoreCase);
                        isFemaleStream = splits[i + 1] == "f";
                    }
                }
                if (tlkId == 0) return;

                var referencedExports = wwiseStreamExport.GetEntriesThatReferenceThisOne()
                    .Select(e => e.Key)
                    .Where(e => e.ClassName == "WwiseEvent")
                    .Where(e =>
                    {
                        if (!e.ObjectName.Name.StartsWith("VO", StringComparison.OrdinalIgnoreCase)) return false;

                        var splits = e.ObjectName.Name.Split("_");
                        if (specifyByGender)
                        {
                            return splits[1] == tlkId.ToString() && (isFemaleStream == (splits[2] == "f"));
                        }
                        else return splits[1] == tlkId.ToString();
                    });
                foreach (ExportEntry re in referencedExports)
                {
                    re.WriteProperty(durationProperty);
                }
            }
        }

        #endregion

        #region Listbox Events

        private void WEMItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e is KeyEventArgs ke)
            {
                switch (ke.Key)
                {
                    case Key.Space:
                        if (CanStartPlayback())
                        {
                            StartOrPausePlaying();
                        }
                        ke.Handled = true;
                        break;
                    case Key.Escape:
                        StopPlaying();
                        ke.Handled = true;
                        break;
                }
            }
        }

        private void ExportInfoListBox_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            object currentSelectedItem = ExportInfoListBox.SelectedItem;
            if (currentSelectedItem is EmbeddedWEMFile)
            {
                StartPlayingCurrentSelection();
            }

            if (currentSelectedItem is ISBankEntry bankEntry && bankEntry.DataAsStored != null)
            {
                StartPlayingCurrentSelection();
            }
        }

        private void ExportInfoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object currentSelectedItem = ExportInfoListBox.SelectedItem;
            if (Settings.Soundpanel_LoopAudio)
            {
                if (currentSelectedItem is EmbeddedWEMFile)
                {
                    StartPlayingCurrentSelection();
                }

                if (currentSelectedItem is ISBankEntry bankEntry && bankEntry.DataAsStored != null)
                {
                    StartPlayingCurrentSelection();
                }
            }
        }

        #endregion
         
        #region HIRC Panel

        public event Action<uint> HIRCObjectSelected;

        private bool CanSaveHIRCHex() => HIRCHexChanged;

        private void SaveHIRCHex()
        {
            int idx = HIRC_ListBox.SelectedIndex;
            if (idx != -1)
            {
                //var dataBefore = hircHexProvider.Bytes.ToArray();
                HIRCObjects[idx] = new HIRCDisplayObject(idx, CreateHircObjectFromHex(hircHexProvider.Span.ToArray()), Pcc.Game)
                {
                    DataChanged = true
                };
                HIRCHexChanged = false;
                OnPropertyChanged(nameof(HIRCHexChanged));
                //var dataAfter = HIRCObjects[idx].Data;
                //if (dataBefore.Length == dataAfter.Length)
                //{
                //    for (int i = 0; i < dataAfter.Length; i++)
                //    {
                //        if (dataAfter[i] != dataBefore[i])
                //        {
                //            MessageBox.Show($@"Committed data has changed! Change starts at 0x{i:X8}");
                //            break;
                //        }
                //    }
                //}
            }
        }

        private WwiseBank.HIRCObject CreateHircObjectFromHex(byte[] bytes)
        {
            return WwiseBank.HIRCObject.Create(new SerializingContainer2(new MemoryStream(bytes), Pcc, true));
        }

        private bool CanSearchHIRCHex()
        {
            string hexString = SearchHIRCHex_TextBox.Text.Replace(" ", string.Empty);
            if (hexString.Length == 0)
                return false;
            if (!IsHexString(hexString))
            {
                return false;
            }

            if (hexString.Length % 2 != 0)
            {
                return false;
            }

            return true;
        }

        private void SearchHIRCHex()
        {
            if (CurrentLoadedWwisebank == null)
                return;
            int currentSelectedHIRCIndex = HIRC_ListBox.SelectedIndex;
            if (currentSelectedHIRCIndex == -1)
                currentSelectedHIRCIndex = 0;
            string hexString = SearchHIRCHex_TextBox.Text.Replace(" ", string.Empty);
            if (hexString.Length == 0)
                return;
            if (!IsHexString(hexString))
            {
                SearchStatusText = "Illegal characters in Hex String";
                return;
            }

            if (hexString.Length % 2 != 0)
            {
                SearchStatusText = "Odd number of characters in Hex String";
                return;
            }

            byte[] buff = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length / 2; i++)
            {
                buff[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }

            int count = HIRCObjects.Count;
            int hexboxIndex = (int)SoundpanelHIRC_Hexbox.SelectionStart + 1;
            for (int i = 0; i < count; i++)
            {
                byte[] hirc = HIRCObjects[(i + currentSelectedHIRCIndex) % count].Data; //search from selected index, and loop back around
                int indexIn = hirc.IndexOfArray(buff, hexboxIndex);
                if (indexIn > -1)
                {
                    HIRC_ListBox.SelectedIndex = (i + currentSelectedHIRCIndex) % count;
                    SoundpanelHIRC_Hexbox.Select(indexIn, buff.Length);
                    //searchHexStatus.Text = "";
                    return;
                }

                hexboxIndex = 0;
            }

            SearchStatusText = "Hex not found";
        }

        private void HIRC_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HIRCNotableItems.ClearEx();
            if (HIRC_ListBox.SelectedItem is HIRCDisplayObject h)
            {
                HIRC_ListBox.ScrollIntoView(h);

                OriginalHIRCHex = h.Data;
                hircHexProvider.ReplaceBytes(OriginalHIRCHex);
                SoundpanelHIRC_Hexbox.Refresh();

                int start = 0x0;
                HIRCNotableItems.Add(new HIRCNotableItem
                {
                    Offset = start,
                    Header = $"Type: 0x{h.ObjType:X2}",
                    Length = (Pcc?.Game == MEGame.ME2 ? 4 : 1)
                });

                start += (Pcc?.Game == MEGame.ME2 ? 4 : 1);
                HIRCNotableItems.Add(new HIRCNotableItem
                {
                    Offset = start,
                    Header = $"Size: 0x{h.Data.Length - 5:X8}",
                    Length = 4
                });

                start += 4;
                HIRCNotableItems.Add(new HIRCNotableItem
                {
                    Offset = start,
                    Header = $"Object ID: 0x{h.ID:X8}",
                    Length = 4
                });

                start += 4;

                switch ((HIRCType)h.ObjType)
                {
                    case HIRCType.SoundSXFSoundVoice:
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"Unknown 4 bytes: 0x{h.unk1:X8}",
                            Length = 4
                        });

                        start += 4;
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"State: {h.State:X8}",
                            Length = 4
                        });

                        start += 4;
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"Audio ID: {h.AudioID:X8}",
                            Length = 4
                        });

                        start += 4;
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"Source ID: 0x{h.SourceID:X8}",
                            Length = 4
                        });

                        start += 4;
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"Sound Type: {h.SoundType}",
                            Length = 4
                        });
                        break;
                    case HIRCType.Event:
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"# of event actions to fire: {h.EventIDs.Count}",
                            Length = 4
                        });
                        start += 4;
                        foreach (uint eventid in h.EventIDs)
                        {
                            HIRCNotableItems.Add(new HIRCNotableItem
                            {
                                Offset = start,
                                Header = $"Event action to fire: 0x{eventid:X8}",
                                Length = 4
                            });
                            start += 4;
                        }

                        break;
                }
                HIRCObjectSelected?.Invoke(h.ID);
            }
            else
            {
                HIRCNotableItems.Add(new HIRCNotableItem
                {
                    Header = "Select a HIRC object"
                });

                OriginalHIRCHex = null;
                hircHexProvider.Clear();
                SoundpanelHIRC_Hexbox.Refresh();
            }
        }

        private void SoundpanelHIRC_Hexbox_BytesChanged(object sender, EventArgs e)
        {
            if (OriginalHIRCHex != null)
            {
                HIRCHexChanged = !hircHexProvider.Span.SequenceEqual(OriginalHIRCHex);
            }
        }

        private void Soundpanel_HIRCHexbox_SelectionChanged(object sender, EventArgs e)
        {
            if (CurrentLoadedExport != null)
            {
                ReadOptimizedByteProvider hbp = (ReadOptimizedByteProvider)SoundpanelHIRC_Hexbox.ByteProvider;
                var memory = hbp.Span;
                int start = (int)SoundpanelHIRC_Hexbox.SelectionStart;
                int len = (int)SoundpanelHIRC_Hexbox.SelectionLength;
                int size = (int)SoundpanelHIRC_Hexbox.ByteProvider.Length;
                try
                {
                    if (memory.Length > 0 && start != -1 && start < size)
                    {
                        string s = $"Byte: {memory[start]}"; //if selection is same as size this will crash.
                        if (start <= memory.Length - 4)
                        {
                            int val = EndianReader.ToInt32(memory, start, Pcc.Endian);
                            float fval = EndianReader.ToSingle(memory, start, Pcc.Endian);
                            s += $", Int: {val} (0x{val:X8}) Float: {fval}";
                            var referencedHIRCbyID = HIRCObjects.FirstOrDefault(x => x.ID == val);

                            if (referencedHIRCbyID != null)
                            {
                                s += $", HIRC Object (by ID) Index: {referencedHIRCbyID.Index}";
                            }

                            EmbeddedWEMFile referencedWEMbyID = AllWems.FirstOrDefault(x => x.Id == val);

                            if (referencedWEMbyID != null)
                            {
                                s += $", Embedded WEM Object (by ID): {referencedWEMbyID.DisplayString}";
                            }

                            //if (CurrentLoadedExport.FileRef.getEntry(val) is ExportEntry exp)
                            //{
                            //    s += $", Export: {exp.ObjectName}";
                            //}
                            //else if (CurrentLoadedExport.FileRef.getEntry(val) is ImportEntry imp)
                            //{
                            //    s += $", Import: {imp.ObjectName}";
                            //}
                        }

                        s += $" | Start=0x{start:X8} ";
                        if (len > 0)
                        {
                            s += $"Length=0x{len:X8} ";
                            s += $"End=0x{(start + len - 1):X8}";
                        }

                        HIRCStatusBar_LeftMostText.Text = s;
                    }
                    else
                    {
                        HIRCStatusBar_LeftMostText.Text = "Nothing Selected";
                    }
                }
                catch (Exception)
                {
                }

                SoundpanelHIRC_Hexbox.Refresh();
            }
        }

        public bool HasPendingHIRCChanges => HIRCObjects.Any(x => x.DataChanged);

        private byte[] OriginalHIRCHex;
        private static bool ShouldReverseIDEndianness => Settings.Soundplorer_ReverseIDDisplayEndianness;

        private void HIRC_ToggleHexboxWidth_Click(object sender, RoutedEventArgs e)
        {
            GridLength len = HexboxColumnDefinition.Width;
            if (len.Value < HexboxColumnDefinition.MaxWidth)
            {
                HexboxColumnDefinition.Width = new GridLength(HexboxColumnDefinition.MaxWidth);
            }
            else
            {
                HexboxColumnDefinition.Width = new GridLength(HexboxColumnDefinition.MinWidth);
            }
        }

        private void Searchbox_OnKeyUpHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && CanSearchHIRCHex())
            {
                SearchHIRCHex();
            }
        }

        private void CloneHIRCObject(object sender, RoutedEventArgs e)
        {
            if (HIRC_ListBox.SelectedItem is HIRCDisplayObject h)
            {
                WwiseBank.HIRCObject clone = CreateHircObjectFromHex(h.Data).Clone();
                HIRCObjects.Add(new HIRCDisplayObject(HIRCObjects.Count, clone, Pcc.Game)
                {
                    DataChanged = true
                });
                HIRC_ListBox.ScrollIntoView(clone);
                HIRC_ListBox.SelectedItem = clone;
            }
        }

        public class HIRCNotableItem
        {
            public int Offset { get; set; }
            public string Header { get; set; }
            public int Length { get; internal set; }
            public override string ToString() => $"0x{Offset:X6}: {Header}";
        }

        private void HIRCNotableItems_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundpanelHIRC_Hexbox.UnhighlightAll();
            if (HIRCNotableItems_ListBox.SelectedItem is HIRCNotableItem h)
            {
                SoundpanelHIRC_Hexbox.Highlight(h.Offset, h.Length);
                SoundpanelHIRC_Hexbox.SelectionStart = h.Offset;
                SoundpanelHIRC_Hexbox.SelectionLength = 1;
            }
        }

        #endregion

        #region Soundpanel Closing

        private void Soundpanel_Unloaded(object sender, RoutedEventArgs e)
        {
            seekbarUpdateTimer?.Stop();
        }

        /// <summary>
        /// Call this method when the soundpanel is being destroyed to release the audio and stop playback.
        /// </summary>
        public void Soundpanel_Unload()
        {
            StopPlaying();
            _audioPlayer?.Dispose();
        }

        public override void Dispose()
        {
            FreeAudioResources();
            SoundpanelHIRC_Hexbox = null;
            HIRC_Hexbox_Host.Child.Dispose();
            HIRC_Hexbox_Host.Dispose();
            CurrentLoadedWwisebank = null;
        }

        #endregion

        #region Helpers

        private static string GetHexForUI(byte[] bytes, int startoffset, int length, Endian endian)
        {
            string ret = "";

            if (length == 2)
            {
                ret += EndianReader.ToInt16(bytes, startoffset, endian);
            }
            else if (length == 4)
            {
                ret += EndianReader.ToInt32(bytes, startoffset, endian);
            }

            ret += " (";
            for (int i = 0; i < length; i++)
            {
                ret += bytes[startoffset + i].ToString("X2") + " ";
            }

            ret = ret.Trim();
            ret += ")";
            return ret;
        }

        public static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFU) << 24) | ((value & 0x0000FF00U) << 8) |
                   ((value & 0x00FF0000U) >> 8) | ((value & 0xFF000000U) >> 24);
        }

        public static bool IsHexString(string s)
        {
            const string hexChars = "0123456789abcdefABCDEF";
            return s.All(c => hexChars.Contains(c));
        }

        private void ExtractISBEToWav(object sender, RoutedEventArgs e)
        {
            //todo: standard extraction

        }

        private void ExtractISBERaw(object sender, RoutedEventArgs e)
        {
            object currentSelectedItem = ExportInfoListBox.SelectedItem;
            if (!(currentSelectedItem is ISBankEntry isbe) || isbe.DataAsStored == null || isbe.FullData == null)
            {
                return; //nothing selected, or current item is not playable
            }

            var bankEntry = (ISBankEntry)currentSelectedItem;
            SaveFileDialog d = new SaveFileDialog
            {
                //ISBS is not a real extension, but I set it to prevent people from trying to load a single sample into
                //soundplorer and breaking things as the headers are different for real banks.
                Filter = "ISACT Single Sample|*.isbs",
                FileName = Path.GetFileNameWithoutExtension(isbe.FileName) + ".isbs"
            };
            if (d.ShowDialog() == true)
            {
                File.WriteAllBytes(d.FileName, bankEntry.FullData);
                MessageBox.Show("Done");
            }
        }

        public static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (true)
            {
                //get parent item
                DependencyObject parentObject = VisualTreeHelper.GetParent(child);

                switch (parentObject)
                {
                    //we've reached the end of the tree
                    case null:
                        return null;
                    //check if the parent matches the type we're looking for
                    case T parent:
                        return parent;
                    default:
                        child = parentObject;
                        break;
                }
            }
        }
        #endregion
    }

    public class EmbeddedWEMFile
    {
        public uint Id;
        public bool HasBeenFixed;
        public MEGame Game;

        public EmbeddedWEMFile(byte[] WemData, string DisplayString, ExportEntry export, uint Id = 0)
        {
            this.export = export;
            this.Id = Id;
            this.Game = export.Game;
            this.WemData = WemData;
            this.DisplayString = DisplayString;


            int size = EndianReader.ToInt32(WemData, 4, export.FileRef.Endian);
            int subchunk2size = EndianReader.ToInt32(WemData, 0x5A, export.FileRef.Endian);

            if (size != WemData.Length - 8)
            {
                OriginalWemData = WemData.ArrayClone(); //store copy of the original data in the event the user rewrites a WEM

                //Some clips in ME3 are just the intro to the audio. The raw data is literally cutoff and the first ~.5 seconds are inserted into the soundbank.
                //In order to attempt to even listen to these we have to fix the headers for size and subchunk2size.
                size = WemData.Length - 8;
                HasBeenFixed = true;
                this.DisplayString += " - Preloading";
                int offset = 4;

                if (export.FileRef.Endian == Endian.Little)
                {
                    WemData[offset] = (byte)size; // fourth byte
                    WemData[offset + 1] = (byte)(size >> 8); // third byte
                    WemData[offset + 2] = (byte)(size >> 16); // second byte
                    WemData[offset + 3] = (byte)(size >> 24); // last byte

                    offset = 0x5A; //Subchunk2 size offset
                    size = WemData.Length - 94; //size of data to follow
                    WemData[offset] = (byte)size; // fourth byte
                    WemData[offset + 1] = (byte)(size >> 8); // third byte
                    WemData[offset + 2] = (byte)(size >> 16); // second byte
                    WemData[offset + 3] = (byte)(size >> 24); // last byte
                }
                else
                {
                    WemData[offset + 3] = (byte)size; // fourth byte
                    WemData[offset + 2] = (byte)(size >> 8); // third byte
                    WemData[offset + 1] = (byte)(size >> 16); // second byte
                    WemData[offset] = (byte)(size >> 24); // last byte

                    offset = 0x5A; //Subchunk2 size offset
                    size = WemData.Length - 94; //size of data to follow
                    WemData[offset + 3] = (byte)size; // fourth byte
                    WemData[offset + 2] = (byte)(size >> 8); // third byte
                    WemData[offset + 1] = (byte)(size >> 16); // second byte
                    WemData[offset] = (byte)(size >> 24); // last byte
                }

                var audioLen = GetAudioInfo(WemData)?.GetLength();
                if (audioLen != null && audioLen.Value != TimeSpan.Zero)
                {
                    this.DisplayString += $" ({ audioLen.Value.ToString(@"mm\:ss\:fff")})";
                }
                
                if(App.IsDebug)
                {
                    var audioData = GetAudioInfo(WemData);
                    this.DisplayString += $" (Size { audioData.AudioDataSize.ToString()})";
                    this.DisplayString += $" (BitsPerSample { audioData.BitsPerSample.ToString()})";
                    this.DisplayString += $" (Channels { audioData.Channels.ToString()})";
                    this.DisplayString += $" (CodecID { audioData.CodecID.ToString()})";
                    this.DisplayString += $" (Codec { audioData.CodecName.ToString()})";
                    this.DisplayString += $" (SampleCount { audioData.SampleCount.ToString()})";
                    this.DisplayString += $" (SampleRate { audioData.SampleRate.ToString()})";
                }
            }
        }

        private ExportEntry export;
        public byte[] WemData { get; set; }
        public byte[] OriginalWemData { get; set; }
        public string DisplayString { get; set; }

        public AudioInfo GetAudioInfo(byte[] dataOverride = null)
        {
            // Similar to WwiseStream
            try
            {
                AudioInfo ai = new AudioInfo();
                Stream dataStream = new MemoryStream(dataOverride ?? WemData);

                EndianReader er = new EndianReader(dataStream);
                var header = er.ReadStringASCII(4);
                if (header == "RIFX") er.Endian = Endian.Big;
                if (header == "RIFF") er.Endian = Endian.Little;
                // Position 4

                // This info seems wrong. Needs to be updated. Probably for 5.1.


                er.Seek(0xC, SeekOrigin.Current); // Post 'fmt ', get fmt size
                var fmtSize = er.ReadInt32();
                var postFormatPosition = er.Position;
                ai.CodecID = er.ReadUInt16();

                switch (ai.CodecID)
                {
                    case 0xFFFF:
                        ai.CodecName = "Vorbis";
                        break;
                    default:
                        ai.CodecName = $"Unknown codec ID {ai.CodecID}";
                        break;
                }

                ai.Channels = er.ReadUInt16();
                ai.SampleRate = er.ReadUInt32();
                er.ReadInt32(); //Average bits per second
                er.ReadUInt16(); //Alignment. VGMStream shows this is 16bit but that doesn't seem right
                ai.BitsPerSample = er.ReadUInt16(); //Bytes per sample. For vorbis this is always 0!
                var extraSize = er.ReadUInt16();
                if (extraSize == 0x30)
                {
                    er.Seek(postFormatPosition + 0x18, SeekOrigin.Begin);
                    ai.SampleCount = er.ReadUInt32();
                }
                else
                {
                    // Find audio sample data chunk size
                    er.Seek(0x10 + fmtSize, SeekOrigin.Begin);
                    var chunkName = er.ReadStringASCII(4);
                    while (!chunkName.Equals("data", StringComparison.InvariantCultureIgnoreCase))
                    {
                        er.Seek(er.ReadInt32(), SeekOrigin.Current);
                        chunkName = er.ReadStringASCII(4);
                    }
                    ai.AudioDataSize = er.ReadUInt32();
                }

                // We don't care about the rest.
                return ai;
            }
            catch
            {
                return null;
            }
        }
    }

    public class ImportExportSoundEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}