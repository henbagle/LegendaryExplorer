using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorer.Tools.Soundplorer;
using LegendaryExplorer.Tools.FaceFXEditor;
using LegendaryExplorer.Tools.InterpEditor;
using LegendaryExplorer.Tools.AFCCompactorWindow;
using LegendaryExplorer.Tools.AnimationImporterExporter;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.ConditionalsEditor;
using LegendaryExplorer.Tools.Meshplorer;
using LegendaryExplorer.Tools.PathfindingEditor;
using LegendaryExplorer.Tools.WwiseEditor;
using LegendaryExplorer.Tools.TFCCompactor;
using LegendaryExplorer.Tools.MountEditor;
using LegendaryExplorer.ToolsetDev;
using LegendaryExplorer.ToolsetDev.MemoryAnalyzer;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using Newtonsoft.Json;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using System.Text;
using LegendaryExplorer.Tools.ClassViewer;
using LegendaryExplorer.Tools.PlotDatabase;

namespace LegendaryExplorer
{
    public class Tool : DependencyObject
    {
        public string Name { get; init; }
        public ImageSource Icon { get; init; }
        public Action OpenTool { get; init; }
        public List<string> Tags;
        public string Category { get; set; }
        public string Category2 { get; set; }
        public string Description { get; set; }
        public Type Type { get; set; }
        public bool IsFavorited
        {
            get => (bool)GetValue(IsFavoritedProperty);
            set => SetValue(IsFavoritedProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsFavorited.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsFavoritedProperty =
            DependencyProperty.Register(nameof(IsFavorited), typeof(bool), typeof(Tool), new PropertyMetadata(false, OnIsFavoritedChanged));

        private static void OnIsFavoritedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ToolSet.SaveFavorites();
        }
    }

    public static class ToolSet
    {
        //has an invisible no width space at the beginning so it will sort last
        private const string other = "⁣Other";
        private static HashSet<Tool> _items;
        public static event EventHandler FavoritesChanged;

        public static IReadOnlyCollection<Tool> Items => _items;

        public static void Initialize()
        {
            HashSet<Tool> set = new();

            #region Toolset Devs

#if DEBUG
            set.Add(new Tool
            {
                Name = "AutoTOC",
                Type = typeof(Tools.AutoTOC.AutoTOCWindow),
                Icon = Application.Current.FindResource("iconAutoTOC") as ImageSource,
                OpenTool = () =>
                {
                    (new Tools.AutoTOC.AutoTOCWindow()).Show();
                },
                Tags = new List<string> { "user", "toc", "tocing", "crash", "infinite", "loop", "loading" },
                Category = "Toolset Devs",
                Description = "AutoTOC is a tool for ME3 and LE that updates and/or creates the PCConsoleTOC.bin files associated with the base game and each DLC."
            });

            set.Add(new Tool
            {
                Name = "Memory Analyzer",
                Type = typeof(MemoryAnalyzerUI),
                Icon = Application.Current.FindResource("iconMemoryAnalyzer") as ImageSource,
                OpenTool = () =>
                {
                    (new MemoryAnalyzerUI()).Show();
                },
                Tags = new List<string> { "utility", "toolsetdev" },
                Category = "Toolset Devs",
                Description = "Memory Analyzer allows you to track references to objects to help trace memory leaks."
            });

            set.Add(new Tool
            {
                Name = "File Hex Analyzer",
                Type = typeof(FileHexViewer),
                Icon = Application.Current.FindResource("iconFileHexAnalyzer") as ImageSource,
                OpenTool = () =>
                {
                    (new FileHexViewer()).Show();
                },
                Tags = new List<string> { "utility", "toolsetdev", "hex" },
                Category = "Toolset Devs",
                Description = "File Hex Analyzer is a package hex viewer that shows references in the package hex. It also works with non-package files, but won't show any references, obviously."
            });

            set.Add(new Tool
            {
                Name = "SFAR Explorer",
                Type = typeof(Tools.SFARExplorer.SFARExplorerWindow),
                Icon = Application.Current.FindResource("iconSFARExplorer") as ImageSource,
                OpenTool = () =>
                {
                    (new Tools.SFARExplorer.SFARExplorerWindow()).Show();
                },
                Tags = new List<string> { "developer", "dlc" },
                Category = "Toolset Devs",
                Description = "SFAR Explorer allows you to explore and extract ME3 DLC archive files (SFAR).",
            });
#endif
            #endregion

            #region Utilities
            set.Add(new Tool
            {
                Name = "Animation Viewer",
                Type = typeof(Tools.AnimationViewer.AnimationViewerWindow),
                Icon = Application.Current.FindResource("iconAnimViewer") as ImageSource,
                OpenTool = () =>
                {
                    if (Tools.AnimationViewer.AnimationViewerWindow.Instance == null)
                    {
                        (new Tools.AnimationViewer.AnimationViewerWindow()).Show();
                    }
                    else
                    {
                        Tools.AnimationViewer.AnimationViewerWindow.Instance.RestoreAndBringToFront();
                    }
                },
                Tags = new List<string> { "utility", "animation", "gesture" },
                Category = "Cinematic Tools",
                Category2 = "Utilities",
                Description = "Animation Viewer allows you to preview any animation in Mass Effect 3"
            });

#if DEBUG
            set.Add(new Tool
            {
                Name = "Class Hierarchy Viewer",
                Type = typeof(Tools.ClassViewer.ClassViewerWindow),
                Icon = Application.Current.FindResource("iconAnimViewer") as ImageSource,
                OpenTool = () =>
                {
                    new ClassViewerWindow().Show();
                },
                Tags = new List<string> { "utility", "class", "property" },
                Category = "Utilities",
                Description = "Class Hierarchy Viewer shows you how classes and properties inherit from each other, and where some override."
            });
#endif
            set.Add(new Tool
            {
                Name = "Live Level Editor",
                Type = typeof(Tools.LiveLevelEditor.LiveLevelEditorWindow),
                Icon = Application.Current.FindResource("iconLiveLevelEditor") as ImageSource,
                OpenTool = () =>
                {
                    var gameStr = InputComboBoxWPF.GetValue(null, "Choose game you want to use Live Level Editor with.", "Live Level Editor game selector",
                                              new[] { "ME3", "ME2", "LE1" }, "ME3");

                    if (Enum.TryParse(gameStr, out MEGame game))
                    {
                        if (Tools.LiveLevelEditor.LiveLevelEditorWindow.Instance(game) is { } instance)
                        {
                            instance.RestoreAndBringToFront();
                        }
                        else
                        {
                            (new Tools.LiveLevelEditor.LiveLevelEditorWindow(game)).Show();
                        }
                    }
                },
                Tags = new List<string> { "utility" },
                Category = "Utilities",
                Description = "Live Level Editor allows you to preview the effect of property changes to Actors in game, to reduce iteration times. It also has a Camera Path Editor, which lets you make camera pans quickly."
            });
            set.Add(new Tool
            {
                Name = "AFC Compactor",
                Type = typeof(AFCCompactorWindow),
                Icon = Application.Current.FindResource("iconAFCCompactor") as ImageSource,
                OpenTool = () =>
                {
                    (new AFCCompactorWindow()).Show();
                },
                Tags = new List<string> { "utility", "deployment", "audio", },
                Category = "Audio Tools",
                Category2 = "Utilities",
                Description = "AFC Compactor can compact your ME2 or ME3 Audio File Cache (AFC) files by removing unreferenced chunks. It also can be used to reduce or remove AFC dependencies so users do not have to have DLC installed for certain audio to work.",
            });
            //            set.Add(new Tool
            //            {
            //                name = "ASI Manager",
            //                type = typeof(ASI.ASIManager),
            //                icon = Application.Current.FindResource("iconASIManager") as ImageSource,
            //                open = () =>
            //                {
            //                    (new ASI.ASIManager()).Show();
            //                },
            //                tags = new List<string> { "utility", "asi", "debug", "log" },
            //                category = "Debugging",
            //                description = "ASI Manager allows you to install and uninstall ASI mods for all three Mass Effect Trilogy games. ASI mods allow you to run native mods that allow you to do things such as kismet logging or function call monitoring."
            //            });
            set.Add(new Tool
            {
                Name = "Audio Localizer",
                Type = typeof(Tools.AudioLocalizer.AudioLocalizerWindow),
                Icon = Application.Current.FindResource("iconAudioLocalizer") as ImageSource,
                OpenTool = () =>
                {
                    (new Tools.AudioLocalizer.AudioLocalizerWindow()).Show();
                },
                Tags = new List<string> { "utility", "localization", "LOC_INT", "translation" },
                Category = "Audio Tools",
                Category2 = "Utilities",
                Description = "Audio Localizer allows you to copy the afc offsets and filenames from localized files to your mods LOC_INT files."
            });
            //            set.Add(new Tool
            //            {
            //                name = "Bik Movie Extractor",
            //                type = typeof(BIKExtract),
            //                icon = Application.Current.FindResource("iconBikExtractor") as ImageSource,
            //                open = () =>
            //                {
            //                    (new BIKExtract()).Show();
            //                },
            //                tags = new List<string> { "utility", "bik", "movie", "bink", "video", "tfc" },
            //                category = "Extractors + Repackers",
            //                description = "BIK Movie Extractor is a utility for extracting BIK videos from the ME3 Movies.tfc. This file contains small resolution videos played during missions, such as footage of Miranda in Sanctuary.",
            //            });
            set.Add(new Tool
            {
                Name = "Coalesced Compiler",
                Type = typeof(Tools.CoalescedCompiler.CoalescedCompilerWindow),
                Icon = Application.Current.FindResource("iconCoalescedCompiler") as ImageSource,
                OpenTool = () =>
                {
                    (new Tools.CoalescedCompiler.CoalescedCompilerWindow()).Show();
                },
                Tags = new List<string> { "utility", "coal", "ini", "bin" },
                Category = "Extractors + Repackers",
                Category2 = "Utilities",
                Description = "Coalesced Compiler converts between XML and BIN formats for coalesced files. These are key game files that help control a large amount of content.",
            });
            set.Add(new Tool
            {
                Name = "DLC Unpacker",
                Type = typeof(Tools.DLCUnpacker.DLCUnpackerWindow),
                Icon = Application.Current.FindResource("iconDLCUnpacker") as ImageSource,
                OpenTool = () =>
                {
                    if (ME3Directory.DefaultGamePath != null)
                    {
                        new Tools.DLCUnpacker.DLCUnpackerWindow().Show();
                    }
                    else
                    {
                        MessageBox.Show("DLC Unpacker only works with Mass Effect 3.");
                    }
                },
                Tags = new List<string> { "utility", "dlc", "sfar", "unpack", "extract" },
                Category = "Extractors + Repackers",
                Description = "DLC Unpacker allows you to extract Mass Effect 3 OT DLC SFAR files, allowing you to access their contents for modding. This unpacker is based on MEM code, which is very fast and is compatible with the ALOT texture mod.",
            });
            set.Add(new Tool
            {
                Name = "Hex Converter",
                Icon = Application.Current.FindResource("iconHexConverter") as ImageSource,
                OpenTool = () =>
                {
                    if (File.Exists(AppDirectories.HexConverterPath))
                    {
                        Process.Start(AppDirectories.HexConverterPath);
                    }
                    else
                    {
                        new HexConverter.MainWindow().Show();
                    }
                },
                Tags = new List<string> { "utility", "code", "endian", "convert", "integer", "float" },
                Category = "Utilities",
                Description = "Hex Converter is a utility that converts among floats, signed/unsigned integers, and hex code in big/little endian.",
            });
            set.Add(new Tool
            {
                Name = "Interp Editor",
                Type = typeof(InterpEditorWindow),
                Icon = Application.Current.FindResource("iconInterpEditor") as ImageSource,
                OpenTool = () =>
                {
                    (new InterpEditorWindow()).Show();
                },
                Tags = new List<string> { "utility", "dialogue", "matinee", "cutscene", "animcutscene", "interpdata" },
                Category = "Cinematic Tools",
                Description = "Interp Editor is a simplified version of UDK’s Matinee Editor. It loads interpdata objects and displays their children as tracks on a timeline, allowing the user to visualize the game content associated with a specific scene."
            });
            set.Add(new Tool
            {
                Name = "Mesh Explorer",
                Type = typeof(MeshplorerWindow),
                Icon = Application.Current.FindResource("iconMeshplorer") as ImageSource,
                OpenTool = () =>
                {
                    (new MeshplorerWindow()).Show();
                },
                Tags = new List<string> { "developer", "mesh", "meshplorer" },
                Category = "Meshes + Textures",
                Description = "Mesh Explorer loads and displays all meshes within a file. The tool skins most meshes with its associated texture. This tool works with all three games."
            });
            set.Add(new Tool
            {
                Name = "Animation Importer/Exporter",
                Type = typeof(AnimationImporterExporterWindow),
                Icon = Application.Current.FindResource("iconAnimationImporter") as ImageSource,
                OpenTool = () =>
                {
                    (new AnimationImporterExporterWindow()).Show();
                },
                Tags = new List<string> { "developer", "animation", "psa", "animset", "animsequence" },
                Category = "Extractors + Repackers",
                Description = "Import and Export AnimSequences from/to PSA and UDK"
            });
            set.Add(new Tool
            {
                Name = "TLK Editor",
                Type = typeof(TLKEditorExportLoader),
                Icon = Application.Current.FindResource("iconTLKEditor") as ImageSource,
                OpenTool = () =>
                {
                    var elhw = new ExportLoaderHostedWindow(new TLKEditorExportLoader())
                    {
                        Title = $"TLK Editor"
                    };
                    elhw.Show();
                },
                Tags = new List<string> { "utility", "dialogue", "subtitle", "text", "string" },
                Category = "Core Editors",
                Category2 = "Utilities",
                Description = "TLK Editor is an editor for localized text, located in TLK files. These files are embedded in package files in Mass Effect 1 and stored externally in Mass Effect 2 and 3.",
            });
            set.Add(new Tool
            {
                Name = "Package Dumper",
                Type = typeof(Tools.PackageDumper.PackageDumperWindow),
                Icon = Application.Current.FindResource("iconPackageDumper") as ImageSource,
                OpenTool = () =>
                {
                    (new Tools.PackageDumper.PackageDumperWindow()).Show();
                },
                Tags = new List<string> { "utility", "package", "pcc", "text", "dump" },
                Category = "Utilities",
                Category2 = "Extractors + Repackers",
                Description = "Package Dumper is a utility for dumping package information to files that can be searched with tools like GrepWin. Names, Imports, Exports, Properties and more are dumped."
            });
            set.Add(new Tool
            {
                Name = "Dialogue Dumper",
                Type = typeof(Tools.DialogueDumper.DialogueDumperWindow),
                Icon = Application.Current.FindResource("iconDialogueDumper") as ImageSource,
                OpenTool = () =>
                {
                    (new Tools.DialogueDumper.DialogueDumperWindow()).Show();
                },
                Tags = new List<string> { "utility", "convo", "dialogue", "text", "dump" },
                Category = "Utilities",
                Category2 = "Extractors + Repackers",
                Description = "Dialogue Dumper is a utility for dumping conversation strings from games into an excel file. It shows the actor that spoke the line and which file the line is taken from. It also produces a table of who owns which conversation, for those that the owner is anonymous."
            });
            set.Add(new Tool
            {
                Name = "Asset Database",
                Type = typeof(AssetDatabaseWindow),
                Icon = Application.Current.FindResource("iconAssetDatabase") as ImageSource,
                OpenTool = () =>
                {
                    (new AssetDatabaseWindow()).Show();
                },
                Tags = new List<string> { "utility", "mesh", "material", "class", "animation" },
                Category = "Utilities",
                Description = "Scans games and creates a database of classes, animations, materials, textures, particles and meshes. Individual assets can be opened directly from the interface with tools for editing."
            });
            set.Add(new Tool
            {
                Name = "TFC Compactor",
                Type = typeof(TFCCompactorWindow),
                Icon = Application.Current.FindResource("iconTFCCompactor") as ImageSource,
                OpenTool = () =>
                {
                    (new TFCCompactorWindow()).Show();
                },
                Tags = new List<string> { "utility", "deployment", "textures", "compression" },
                Category = "Meshes + Textures",
                Category2 = "Utilities",
                Description = "TFC Compactor can compact your DLC mod TFC file by effectively removing unreferenced chunks and compressing the referenced textures. It can also reduce or remove TFC dependencies so users do not have to have DLC installed for certain textures to work.",
            });
            #endregion

            #region Core Tools
            set.Add(new Tool
            {
                Name = "Conditionals Editor",
                Type = typeof(ConditionalsEditorWindow),
                Icon = Application.Current.FindResource("iconConditionalsEditor") as ImageSource,
                OpenTool = () =>
                {
                    (new ConditionalsEditorWindow()).Show();
                },
                Tags = new List<string> { "developer", "conditional", "plot", "boolean", "flag", "int", "integer", "cnd" },
                Category = "Core Editors",
                Description = "Conditionals Editor is used to create and edit ME3/LE3 files with the .cnd extension. CND files control game story by checking for specific combinations of plot events.",
            });
            set.Add(new Tool
            {
                Name = "Dialogue Editor",
                Type = typeof(DialogueEditor.DialogueEditorWindow),
                Icon = Application.Current.FindResource("iconDialogueEditor") as ImageSource,
                OpenTool = () =>
                {
                    (new DialogueEditor.DialogueEditorWindow()).Show();
                },
                Tags = new List<string> { "developer", "me1", "me2", "me3", "cutscene" },
                Category = "Core Editors",
                Category2 = "Cinematic Tools",
                Description = "Dialogue Editor is a visual tool used to edit in-game conversations for all games.",
            });
            set.Add(new Tool
            {
                Name = "FaceFX Editor",
                Type = typeof(FaceFXEditorWindow),
                Icon = Application.Current.FindResource("iconFaceFXEditor") as ImageSource,
                OpenTool = () =>
                {
                    (new FaceFXEditorWindow()).Show();
                },
                Tags = new List<string> { "developer", "fxa", "facefx", "lipsync", "fxe", "bones", "animation", "me3", "me3" },
                Category = "Cinematic Tools",
                Category2 = "Core Editors",
                Description = "FaceFX Editor is the toolset’s highly-simplified version of FaceFX Studio. With this tool modders can edit FaceFX AnimSets (FXEs) for all three games.",
            });
            set.Add(new Tool
            {
                Name = "Mount Editor",
                Type = typeof(MountEditorWindow),
                Icon = Application.Current.FindResource("iconMountEditor") as ImageSource,
                OpenTool = () =>
                {
                    new MountEditorWindow().Show();
                },
                Tags = new List<string> { "developer", "mount", "dlc", "me2", "me3" },
                Category = "Utilities",
                Category2 = "Core Editors",
                Description = "Mount Editor allows you to create or modify mount.dlc files, which are used in DLC for Mass Effect 2 and Mass Effect 3."
            });
            set.Add(new Tool
            {
                Name = "TLK Manager",
                Type = typeof(TLKManagerWPF),
                Icon = Application.Current.FindResource("iconTLKManager") as ImageSource,
                OpenTool = () =>
                {
                    new TLKManagerWPF().Show();
                },
                Tags = new List<string> { "developer", "dialogue", "subtitle", "text", "string", "localize", "language" },
                Category = "Core Editors",
                Category2 = "Utilities",
                Description = "TLK Manager manages loaded TLK files that are used to display string data in editor tools. You can also use it to extract and recompile TLK files."
            });
            set.Add(new Tool
            {
                Name = "Package Editor",
                Type = typeof(PackageEditorWindow),
                Icon = Application.Current.FindResource("iconPackageEditor") as ImageSource,
                OpenTool = () =>
                {
                    new PackageEditorWindow().Show();
                },
                Tags = new List<string> { "user", "developer", "pcc", "cloning", "import", "export", "sfm", "upk", ".u", "me2", "me1", "me3", "name" },
                Category = "Core Editors",
                Description = "Package Editor is Legendary Explorer's general purpose editing tool for Unreal package files in all games. " +
                              "Edit files in a single window with easy access to external tools such as Curve Editor and Sound Explorer."
            });
            set.Add(new Tool
            {
                Name = "Pathfinding Editor",
                Type = typeof(PathfindingEditorWindow),
                Icon = Application.Current.FindResource("iconPathfindingEditor") as ImageSource,
                OpenTool = () =>
                {
                    (new PathfindingEditorWindow()).Show();
                },
                Tags = new List<string> { "user", "developer", "path", "ai", "combat", "spline", "spawn", "map", "path", "node", "cover", "level" },
                Category = "Core Editors",
                Description = "Pathfinding Editor allows you to modify pathing nodes so squadmates and enemies can move around a map. You can also edit placement of several different types of level objects such as StaticMeshes, Splines, CoverSlots, and more.",
            });
            set.Add(new Tool
            {
                Name = "Plot Editor",
                Type = typeof(Tools.PlotEditor.PlotEditorWindow),
                Icon = Application.Current.FindResource("iconPlotEditor") as ImageSource,
                OpenTool = () =>
                {
                    var plotEd = new Tools.PlotEditor.PlotEditorWindow();
                    plotEd.Show();
                },
                Tags = new List<string> { "developer", "codex", "state transition", "quest", "natives" },
                Category = "Core Editors",
                Description = "Plot Editor is used to examine, edit, and search plot maps in all 3 games for quests, state events, and codex entries."
            });
            set.Add(new Tool
            {
                Name = "Plot Database",
                Type = typeof(PlotManagerWindow),
                Icon = Application.Current.FindResource("iconPlotDatabase") as ImageSource,
                OpenTool = () =>
                {
                    var plotMan = new PlotManagerWindow();
                    plotMan.Show();
                },
                Tags = new List<string> { "developer", "codex", "state transition", "quest", "plots", "database", "conditional" },
                Category = "Core Editors",
                Description = "Plot Database is used to view and create databases of plot elements from all three games. This tool is for reference only, and affects nothing in game."
            });
            set.Add(new Tool
            {
                Name = "Sequence Editor",
                Type = typeof(SequenceEditorWPF),
                Icon = Application.Current.FindResource("iconSequenceEditor") as ImageSource,
                OpenTool = () =>
                {
                    (new SequenceEditorWPF()).Show();
                },
                Tags = new List<string> { "user", "developer", "kismet", "me1", "me2", "me3" },
                Category = "Core Editors",
                Description = "Sequence Editor is the toolset’s version of UDK’s UnrealKismet. With this cross-game tool, users can edit and create new sequences that control gameflow within and across levels.",
            });
            set.Add(new Tool
            {
                Name = "Texture Studio",
                Type = typeof(Tools.TextureStudio.TextureStudioWindow),
                Icon = Application.Current.FindResource("iconTextureStudio") as ImageSource,
                OpenTool = () =>
                {
                    (new Tools.TextureStudio.TextureStudioWindow()).Show();
                },
                Tags = new List<string> { "texture", "developer", "studio", "graphics" },
                Category = "Meshes + Textures",
                Description = "THIS TOOL IS NOT COMPLETE AND MAY BREAK MODS. Texture Studio is a tool designed for texture editing files in a directory of files, such as a DLC mod. It is not the same as other tools such as Mass Effect Modder, which is a game wide replacement tool.",
            });
            set.Add(new Tool
            {
                Name = "Sound Explorer",
                Type = typeof(SoundplorerWPF),
                Icon = Application.Current.FindResource("iconSoundplorer") as ImageSource,
                OpenTool = () =>
                {
                    (new SoundplorerWPF()).Show();
                },
                Tags = new List<string> { "user", "developer", "audio", "dialogue", "music", "wav", "ogg", "sound", "afc", "wwise", "bank", "soundplorer" },
                Category = "Audio Tools",
                Description = "Extract and play audio from all 3 games, and replace audio directly in Mass Effect 3 and Mass Effect 2 LE.",
            });
            set.Add(new Tool
            {
                Name = "Wwise Graph Editor",
                Type = typeof(WwiseEditorWindow),
                Icon = Application.Current.FindResource("iconWwiseGraphEditor") as ImageSource,
                OpenTool = () =>
                {
                    (new WwiseEditorWindow()).Show();
                },
                Tags = new List<string> { "developer", "audio", "music", "sound", "wwise", "bank" },
                Category = "Audio Tools",
                Description = "Wwise Graph Editor currently has no editing functionality. " +
                "It can be used to help visualize the relationships between HIRC objects as well as their connection to WwiseEvent and WwiseStream Exports."
            });
            #endregion

            _items = set;

            loadFavorites();
        }

        private static void loadFavorites()
        {
            try
            {
                var favorites = new HashSet<string>(Misc.AppSettings.Settings.MainWindow_Favorites.Split(';'));
                foreach (var tool in _items)
                {
                    if (favorites.Contains(tool.Name))
                    {
                        tool.IsFavorited = true;
                    }
                }
            }
            catch
            {
                return;
            }
        }

        public static void SaveFavorites()
        {
            if (FavoritesChanged != null)
            {
                FavoritesChanged.Invoke(null, EventArgs.Empty);
                try
                {
                    var favorites = new StringBuilder();
                    foreach (var tool in _items)
                    {
                        if (tool.IsFavorited)
                        {
                            favorites.Append(tool.Name + ";");
                        }
                    }
                    if (favorites.Length > 0) favorites.Remove(favorites.Length - 1, 1);
                    Misc.AppSettings.Settings.MainWindow_Favorites = favorites.ToString();
                }
                catch
                {
                    return;
                }
            }
        }
    }
}