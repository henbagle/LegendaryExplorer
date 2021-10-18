﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorer.Tools.TFCCompactor;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.Win32;
using Image = LegendaryExplorerCore.Textures.Image;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for TextureViewerExportLoader.xaml
    /// </summary>
    public partial class TextureViewerExportLoader : ExportLoaderControl
    {
        public ObservableCollectionExtended<Texture2DMipInfo> MipList { get; } = new ObservableCollectionExtended<Texture2DMipInfo>();
        private string CurrentLoadedFormat;
        private string CurrentLoadedCacheName;
        private string CurrentLoadedBasePackageName;

        public ObservableCollectionExtended<string> AvailableTFCNames { get; } = new ObservableCollectionExtended<string>();

        private string _cannotShowTextureText;
        public string CannotShowTextureText
        {
            get => _cannotShowTextureText;
            set => SetProperty(ref _cannotShowTextureText, value);
        }

        private bool _setAlphaToBlack = true;
        public bool SetAlphaToBlack
        {
            get => _setAlphaToBlack;
            set
            {
                SetProperty(ref _setAlphaToBlack, value);
                if (Mips_ListBox.SelectedIndex >= 0)
                {
                    LoadMip(MipList[Mips_ListBox.SelectedIndex]); // reload
                }
            }
        }

        private Visibility _cannotShowTextureTextVisibility;
        public Visibility CannotShowTextureTextVisibility
        {
            get => _cannotShowTextureTextVisibility;
            set => SetProperty(ref _cannotShowTextureTextVisibility, value);
        }

        private Stretch _imageStretchOption = Stretch.Uniform;
        public Stretch ImageStretchOption
        {
            get => _imageStretchOption;
            set => SetProperty(ref _imageStretchOption, value);
        }

        private uint _textureCRC;
        public uint TextureCRC
        {
            get => _textureCRC;
            set => SetProperty(ref _textureCRC, value);
        }

        public bool ViewerModeOnly
        {
            get => (bool)GetValue(ViewerModeOnlyProperty);
            set => SetValue(ViewerModeOnlyProperty, value);
        }
        /// <summary>
        /// Set to true to hide all of the editor controls
        /// </summary>
        public static readonly DependencyProperty ViewerModeOnlyProperty = DependencyProperty.Register(
            "ViewerModeOnly", typeof(bool), typeof(TextureViewerExportLoader), new PropertyMetadata(false, ViewerModeOnlyCallback));

        public IBusyUIHost HostingControl
        {
            get => (IBusyUIHost)GetValue(HostingControlProperty);
            set => SetValue(HostingControlProperty, value);
        }

        public static readonly DependencyProperty HostingControlProperty = DependencyProperty.Register(
            nameof(HostingControl), typeof(IBusyUIHost), typeof(TextureViewerExportLoader));

        private const string CREATE_NEW_TFC_STRING = "Create new TFC";
        private const string STORE_EXTERNALLY_STRING = "Store externally in new TFC";
        private const string PACKAGE_STORED_STRING = "Package stored";

        private static void ViewerModeOnlyCallback(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            TextureViewerExportLoader i = (TextureViewerExportLoader)obj;
            i.OnPropertyChanged(nameof(ViewerModeOnly));
        }

        public TextureViewerExportLoader() : base("Texture Viewer")
        {
            DataContext = this;

            if (!ViewerModeOnly)
                CannotShowTextureText = "Select a mip to view";
            CannotShowTextureTextVisibility = Visibility.Visible;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand ExportToPNGCommand { get; set; }
        public ICommand ReplaceFromPNGCommand { get; set; }
        public ICommand DropMipCommand { get; set; }
        private void LoadCommands()
        {
            ExportToPNGCommand = new GenericCommand(ExportToPNG, NonEmptyMipSelected);
            ReplaceFromPNGCommand = new GenericCommand(ReplaceFromFile, CanReplaceTexture);
            DropMipCommand = new GenericCommand(DropTopMip, CanDropTopMip);
        }

        private void DropTopMip()
        {
            var props = CurrentLoadedExport.GetProperties();

            // Note: This will not remove TFC data if streamed mips are all removed.
            // Hopefully a dev doesn't do this
            // I'm sure someday I will read this comment again and regret it
            // -Mgamerz
            var tex = ObjectBinary.From<UTexture2D>(CurrentLoadedExport);
            tex.Mips.RemoveAt(Mips_ListBox.SelectedIndex);
            props.GetProp<IntProperty>("SizeX").Value = tex.Mips[0].SizeX;
            props.GetProp<IntProperty>("SizeY").Value = tex.Mips[0].SizeY;
            props.GetProp<IntProperty>("MipTailBaseIdx").Value = tex.Mips.Count - 1; // 0 based
            CurrentLoadedExport.WritePropertiesAndBinary(props, tex);
        }

        private bool CanDropTopMip()
        {
            // There must be at least 1 mip
            return CanReplaceTexture() && MipList.Count > 1 /*&& Mips_ListBox.SelectedIndex == 0*/;
        }

        private bool CanReplaceTexture()
        {
            return CurrentLoadedExport != null && CurrentLoadedExport.FileRef.CanReconstruct() && !ViewerModeOnly;
        }

        private void ReplaceFromFile()
        {
            var selectedTFCName = (string)TextureCacheComboBox.SelectedItem;
            if (MEDirectories.BasegameTFCs(CurrentLoadedExport.Game).Contains(selectedTFCName, StringComparer.InvariantCultureIgnoreCase) || MEDirectories.OfficialDLC(CurrentLoadedExport.Game).Any(x => $"Textures_{x}".Equals(selectedTFCName, StringComparison.InvariantCultureIgnoreCase)))
            {
                MessageBox.Show("Cannot replace textures into a TFC provided by BioWare. Choose a different target TFC from the list.");
                return;
            }

            OpenFileDialog selectDDS = new OpenFileDialog
            {
                Title = "Select texture file",
#if WINDOWS
                Filter = "All supported types|*.png;*.dds;*.tga|PNG files (*.png)|*.png|DDS files (*.dds)|*.dds|TGA files (*.tga)|*.tga"
#else
                Filter = "Texture (DDS PNG BMP TGA)|*.dds;*.png;*.bmp;*.tga"
#endif
            };
            var result = selectDDS.ShowDialog();
            if (result.HasValue && result.Value)
            {
                if (HostingControl != null)
                {
                    HostingControl.IsBusy = true;
                    HostingControl.BusyText = "Replacing textures";
                }

                if (selectedTFCName == CREATE_NEW_TFC_STRING || selectedTFCName == STORE_EXTERNALLY_STRING)
                {
                    string defaultTfcName = "Textures_DLC_MOD_YourModFolderNameHere";
                    //attempt to lookup name.
                    var containingFolderInfo = Directory.GetParent(CurrentLoadedExport.FileRef.FilePath);
                    if (Path.GetFileName(containingFolderInfo.FullName).StartsWith("CookedPC"))
                    {
                        //Check next level up.
                        containingFolderInfo = containingFolderInfo.Parent;
                        if (containingFolderInfo != null &&
                            Path.GetFileName(containingFolderInfo.FullName).StartsWith("DLC_"))
                        {
                            var possibleDLCName = Path.GetFileName(containingFolderInfo.FullName);
                            if (!MEDirectories.OfficialDLC(CurrentLoadedExport.Game).Contains(possibleDLCName))
                            {
                                defaultTfcName = $"Textures_{possibleDLCName}";
                            }
                        }

                    }
                    PromptDialog p = new PromptDialog("Enter name for a new TFC. It must start with Textures_DLC_MOD_, and will be created in the local directory of this package file.", "Enter new name for TFC", defaultTfcName, true, "Textures_DLC_MOD_".Length) { Owner = Window.GetWindow(this) };
                    var hasResult = p.ShowDialog();
                    if (hasResult.HasValue && hasResult.Value)
                    {
                        if (p.ResponseText.StartsWith("Textures_DLC_MOD_") && p.ResponseText.Length > 14)
                        {
                            //Check TFC name isn't in list
                            CurrentLoadedExport.FileRef.FindNameOrAdd(p.ResponseText);
                            selectedTFCName = p.ResponseText;
                        }
                        else
                        {
                            MessageBox.Show(
                                "Error: Name must start with Textures_DLC_, and must have at least one additional character.\nThe named should match your DLC's foldername.");
                            return;
                        }
                    }
                    else
                    {
                        if (HostingControl != null)
                        {
                            HostingControl.IsBusy = false;
                        }
                        return;
                    }
                }

                Task.Run(() =>
                {
                    //Check aspect ratios
                    var props = CurrentLoadedExport.GetProperties();
                    var listedWidth = props.GetProp<IntProperty>("SizeX")?.Value ?? 0;
                    var listedHeight = props.GetProp<IntProperty>("SizeY")?.Value ?? 0;

                    Image image;
                    try
                    {
#if WINDOWS
                        image = Image.LoadFromFile(selectDDS.FileName, LegendaryExplorerCore.Textures.PixelFormat.ARGB);
#else
                    image = new Image(selectDDS.FileName);
#endif
                    }
                    catch (TextureSizeNotPowerOf2Exception)
                    {
                        MessageBox.Show("The width and height of a texture must both be a power of 2\n" +
                                        "(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 (LE only))", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Error: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (image.mipMaps[0].origWidth / image.mipMaps[0].origHeight != listedWidth / listedHeight)
                    {
                        MessageBox.Show("Cannot replace texture: Aspect ratios must be the same.");
                        return;
                    }

                    bool isPackageStored = selectedTFCName == PACKAGE_STORED_STRING;
                    if (isPackageStored) selectedTFCName = null;
                    ReplaceTextures(image, props, selectDDS.FileName, selectedTFCName, isPackageStored);

                    // MER: Dump to disk
                    //var binName = Path.Combine(Directory.GetParent(selectDDS.FileName).FullName, Path.GetFileNameWithoutExtension(selectDDS.FileName) + ".bin");
                    //File.WriteAllBytes(binName, CurrentLoadedExport.GetBinaryData());
                })
                .ContinueWithOnUIThread((a) =>
                {
                    if (HostingControl != null) HostingControl.IsBusy = false;

                });
            }
        }

        private void ExportToPNG()
        {
            SaveFileDialog d = new SaveFileDialog
            {
#if WINDOWS
                Filter = "PNG files (*.png)|*.png|DDS files (*.dds)|*.dds|TGA files (*.tga)|*.tga",
#else
                Filter = "PNG files|*.png",
#endif
                FileName = CurrentLoadedExport.ObjectName.Instanced + ".png"
            };
            if (d.ShowDialog() == true)
            {
                Texture2D t2d = new Texture2D(CurrentLoadedExport);
#if WINDOWS
                t2d.ExportToFile(d.FileName);
#else
                t2d.ExportToPNG(d.FileName);
#endif
            }

        }

        private bool NonEmptyMipSelected()
        {
            if (MipList.Count > 0 && Mips_ListBox.SelectedIndex >= 0)
            {
                return MipList[Mips_ListBox.SelectedIndex].storageType != StorageTypes.empty;
            }
            return false;
        }

        public override bool CanParse(ExportEntry exportEntry) => !exportEntry.IsDefaultObject && exportEntry.IsTexture();

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new TextureViewerExportLoader(), CurrentLoadedExport)
                {
                    Title = $"Texture Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {Pcc.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            TextureImage.Source = null;
            try
            {
                MipList.ClearEx();
                AvailableTFCNames.ClearEx();
                PropertyCollection properties = exportEntry.GetProperties();
                var format = properties.GetProp<EnumProperty>("Format");
                var cache = properties.GetProp<NameProperty>("TextureFileCacheName");
                if (cache != null)
                {
                    CurrentLoadedCacheName = cache.Value.Name;
                }

                var neverStream = properties.GetProp<BoolProperty>("NeverStream") ?? false;
                AvailableTFCNames.AddRange(exportEntry.FileRef.Names.Where(x => x.StartsWith("Textures_DLC_")));

                //Populate list first in event loading fails, so user has way to still try to fix texture.
                if (cache != null)
                {
                    if (!AvailableTFCNames.Contains(cache.Value))
                    {
                        AvailableTFCNames.Add(cache.Value);
                    }
                    TextureCacheComboBox.SelectedIndex = AvailableTFCNames.IndexOf(cache.Value);
                    AvailableTFCNames.Add(CREATE_NEW_TFC_STRING);
                }

                List<Texture2DMipInfo> mips = Texture2D.GetTexture2DMipInfos(exportEntry, CurrentLoadedCacheName);
                CurrentLoadedExport = exportEntry;

                if (mips.Any())
                {
                    var topmip = mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);

                    // Some textures list a tfc but are stored locally still
                    // so the tfc is never actually used
                    AvailableTFCNames.Insert(0, PACKAGE_STORED_STRING);

                    if (topmip is { storageType: StorageTypes.pccLZO or StorageTypes.pccZlib or StorageTypes.pccOodle or StorageTypes.pccUnc })
                    {
                        TextureCacheComboBox.SelectedIndex = 0; // Set to Package Stored
                    }

                    if (cache == null && exportEntry.Game > MEGame.ME1)
                    {
                        AvailableTFCNames.Add(STORE_EXTERNALLY_STRING);
                        TextureCacheComboBox.SelectedIndex = 0; // Set to Package Stored
                    }



                    if (exportEntry.FileRef.Game == MEGame.ME1)
                    {
                        string baseName = exportEntry.FileRef.FollowLink(exportEntry.idxLink).Split('.')[0].ToUpper();
                        if (mips.Exists(s => s.storageType == StorageTypes.extLZO) ||
                            mips.Exists(s => s.storageType == StorageTypes.extZlib) ||
                            mips.Exists(s => s.storageType == StorageTypes.extUnc))
                        {
                            CurrentLoadedBasePackageName = baseName;
                        }
                        else
                        {
                            if (baseName != "" && !neverStream)
                            {
                                List<string> gameFiles =
                                    MEDirectories.EnumerateGameFiles(MEGame.ME1, ME1Directory.DefaultGamePath);
                                if (gameFiles.Exists(s =>
                                    Path.GetFileNameWithoutExtension(s).ToUpperInvariant() == baseName))
                                {
                                    CurrentLoadedBasePackageName = baseName;
                                }
                            }
                        }
                    }

                    CurrentLoadedFormat = format.Value.Name;
                    MipList.ReplaceAll(mips);
                    TextureCRC = Texture2D.GetTextureCRC(exportEntry);
                    if (Settings.TextureViewer_AutoLoadMip || ViewerModeOnly)
                    {
                        Mips_ListBox.SelectedIndex = MipList.IndexOf(topmip);
                    }
                }

            }
            catch (Exception e)
            {
                //Error loading texture
                CannotShowTextureText = e.Message;
            }
        }



        private void LoadMip(Texture2DMipInfo mipToLoad)
        {
            if (mipToLoad == null)
            {
                TextureImage.Source = null;
                if (!ViewerModeOnly)
                    CannotShowTextureText = "Select a mip to view";
                CannotShowTextureTextVisibility = Visibility.Visible;
                return;
            }

            if (mipToLoad.storageType == StorageTypes.empty)
            {
                TextureImage.Source = null;
                CannotShowTextureText = "Selected mip is null/empty";
                CannotShowTextureTextVisibility = Visibility.Visible;
                return;
            }
            if (mipToLoad.width == 1 && mipToLoad.height == 1)
            {
                TextureImage.Source = null;
                CannotShowTextureText = "Selected mip too small to display"; // This will crash the toolset if we show it
                CannotShowTextureTextVisibility = Visibility.Visible;
                return;
            }
            TextureImage.Source = null;
            try
            {
                var imagebytes = Texture2D.GetTextureData(mipToLoad, mipToLoad.Export.Game);
                CannotShowTextureTextVisibility = Visibility.Collapsed;

                // NOTE: Set 'ClearAlpha' to false to make image support transparency!
                var bitmap = Image.convertRawToBitmapARGB(imagebytes, mipToLoad.width, mipToLoad.height, Image.getPixelFormatType(CurrentLoadedFormat), SetAlphaToBlack);
                //var bitmap = DDSImage.ToBitmap(imagebytes, fmt, mipToLoad.width, mipToLoad.height, CurrentLoadedExport.FileRef.Platform.ToString());
                var memory = new MemoryStream(bitmap.Height * bitmap.Width * 4 + 54);
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                TextureImage.Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(memory);
            }
            catch (Exception e)
            {
                TextureImage.Source = null;
                CannotShowTextureText = e.Message;
                CannotShowTextureTextVisibility = Visibility.Visible;
            }
        }

        public override void UnloadExport()
        {
            TextureImage.Source = null;
            CurrentLoadedFormat = null;
            MipList.ClearEx();
            CurrentLoadedExport = null;
        }

        private void MipList_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MipList.Count > 0 && Mips_ListBox.SelectedIndex >= 0)
            {
                Debug.WriteLine($"Loading mip: {Mips_ListBox.SelectedIndex}");
                LoadMip(MipList[Mips_ListBox.SelectedIndex]);
            }
        }

        public string ReplaceTextures(Image image, PropertyCollection props, string fileSourcePath = null, string forcedTFCName = null, bool isPackageStored = false)
        {
            var texture = new Texture2D(CurrentLoadedExport);
            return texture.Replace(image, props, fileSourcePath, forcedTFCName, isPackageStored: isPackageStored);
        }

        public override void Dispose()
        {
            //Nothing to dispose
        }

        private void ScalingTurnOff(object sender, RoutedEventArgs e)
        {
            ImageStretchOption = Stretch.None;
        }

        private void ScalingTurnOn(object sender, RoutedEventArgs e)
        {
            ImageStretchOption = Stretch.Uniform;
        }
        private void CRC_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (TextureCRC != 0 && e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 2)
            {
                Clipboard.SetText(TextureCRC.ToString("X8"));
            }
        }
    }
}
