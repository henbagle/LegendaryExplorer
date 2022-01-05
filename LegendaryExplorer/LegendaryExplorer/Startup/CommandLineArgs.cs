using System;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;
using LegendaryExplorer.DialogueEditor;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.ConditionalsEditor;
using LegendaryExplorer.Tools.FaceFXEditor;
using LegendaryExplorer.Tools.Meshplorer;
using LegendaryExplorer.Tools.MountEditor;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.PathfindingEditor;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.Tools.Soundplorer;
using LegendaryExplorer.UserControls.ExportLoaderControls;

namespace LegendaryExplorer.Startup
{
    public static class CommandLineArgs
    {
        // Documentation: 
        // https://github.com/dotnet/command-line-api
        public static RootCommand CreateCLIHandler()
        {
            var root = new RootCommand()
            {
                new Option<FileInfo>(new [] {"--open", "-o"}, "A file you would like to open in the toolset"),
                new Option<string>(new [] {"--tool", "-t"}, "The tool to open"),
                new Option<int>("--UIndex", "UIndex of the file to open")
            };

            root.Handler = CommandHandler.Create<FileInfo, string, int>(HandleCLIArgs);
            return root;
        }

        // Method parameters MUST match option names in the RootCommand
        private static void HandleCLIArgs(FileInfo open, string tool, int uIndex)
        {
            var file = open;

            // Handle file opening
            if (file?.Exists ?? false)
            {
                switch(tool)
                {
                    case "SequenceEditor":
                        ToolOpener.OpenInTool<SequenceEditorWPF>(file.FullName);
                        break;
                    case "DialogueEditor":
                        ToolOpener.OpenInTool<DialogueEditorWindow>(file.FullName);
                        break;
                    case "SoundExplorer":
                    case "Soundplorer":
                        ToolOpener.OpenInTool<SoundplorerWPF>(file.FullName);
                        break;
                    default:
                        switch(file.Extension.ToLower())
                        {
                            case ".pcc":
                            case ".sfm":
                            case ".upk":
                            case ".u":
                            case ".udk":
                                OpenTool<PackageEditorWindow>((p) => p.LoadFile(file.FullName, uIndex));
                                break;
                            case ".tlk":
                                var elhw = new ExportLoaderHostedWindow(new TLKEditorExportLoader(), file.FullName)
                                {
                                    Title = $"TLK Editor"
                                };
                                elhw.Show();
                                break;
                            case ".dlc":
                                var me = new MountEditorWindow();
                                me.Show();
                                me.LoadFile(file.FullName);
                                break;
                            case ".isb":
                            case ".afc":
                                ToolOpener.OpenInTool<SoundplorerWPF>(file.FullName);
                                break;
                            case ".cnd":
                                ToolOpener.OpenInTool<ConditionalsEditorWindow>(file.FullName);
                                break;

                        }
                        break;

                }
            }

            // Handle tool opening without a file
            else
            {
                // Tool must have a parameterless constructor for this to work
                switch (tool)
                {
                    case "PackageEditor":
                        ToolOpener.OpenTool<PackageEditorWindow>();
                        break;
                    case "SequenceEditor":
                        ToolOpener.OpenTool<SequenceEditorWPF>();
                        break;
                    case "Soundplorer":
                        ToolOpener.OpenTool<SoundplorerWPF>();
                        break;
                    case "DialogueEditor":
                        ToolOpener.OpenTool<DialogueEditorWindow>();
                        break;
                    case "PathfindingEditor":
                        ToolOpener.OpenTool<PathfindingEditorWindow>();
                        break;
                    case "Meshplorer":
                        ToolOpener.OpenTool<MeshplorerWindow>();
                        break;
                    case "FaceFXEditor":
                        ToolOpener.OpenTool<FaceFXEditorWindow>();
                        break;
                    case "AssetDB":
                        ToolOpener.OpenTool<AssetDatabaseWindow>();
                        break;
                }
            }
        }

        private static void OpenTool<T>(Action<T> toolAction = null)
            where T : WPFBase, new()
        {
            var editor = new T();
            editor.Show();
            toolAction?.Invoke(editor);
            editor.Activate();
        }
    }
}
