﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ME3Explorer.SharedUI;
using ME3ExplorerCore;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3ExplorerCore.UnrealScript;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Compiling;
using ME3ExplorerCore.UnrealScript.Compiling.Errors;
using ME3ExplorerCore.UnrealScript.Language.Tree;
using ME3ExplorerCore.UnrealScript.Lexing.Tokenizing;
using ME3ExplorerCore.UnrealScript.Parsing;

namespace ME3Explorer.ME3Script.IDE
{
    /// <summary>
    /// Interaction logic for UnrealScriptIDE.xaml
    /// </summary>
    public partial class UnrealScriptIDE : ExportLoaderControl
    {
        public string ScriptText
        {
            get => Document?.Text;
            set => Dispatcher.Invoke(() =>
            {
                if (Document is not null)
                {
                    Document.TextChanged -= TextChanged;
                }

                if (foldingManager != null)
                {
                    FoldingManager.Uninstall(foldingManager);
                }
                Document = new TextDocument(value);
                foldingManager = FoldingManager.Install(textEditor.TextArea);
                foldingStrategy.UpdateFoldings(foldingManager, Document);
            });
        }

        private ASTNode _rootNode;
        public ASTNode RootNode
        {
            get => _rootNode;
            set => SetProperty(ref _rootNode, value);
        }

        public UnrealScriptIDE() : base("UnrealScript IDE")
        {
            InitializeComponent();
            DataContext = this;
            progressBarTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            progressBarTimer.Tick += ProgressBarTimer_Tick;
            IsBusy = true;
            BusyText = "Initializing Script Compiler";

            textEditor.TextArea.TextEntered += TextAreaOnTextEntered;
            _definitionLinkGenerator = new DefinitionLinkGenerator();
            textEditor.TextArea.TextView.ElementGenerators.Add(_definitionLinkGenerator);
        }

        public override bool CanParse(ExportEntry exportEntry) =>
            exportEntry.Game <= MEGame.ME3 && exportEntry.FileRef.Platform == MEPackage.GamePlatform.PC && (exportEntry.ClassName switch
            {
                "Class" => true,
                "State" => true,
                "Function" => true,
                "Enum" => true,
                "ScriptStruct" => true,
                _ => false
            } || exportEntry.IsDefaultObject);

        public override void LoadExport(ExportEntry export)
        {
            if (CurrentLoadedExport != export)
            {
                UnloadExport();
            }
            CurrentLoadedExport = export;
            if (Pcc != CurrentFileLib?.Pcc)
            {
                FullyInitialized = false;
                IsBusy = true;
                BusyText = "Compiling local classes";
                UnloadFileLib();
                CurrentFileLib = new FileLib(Pcc);
                CurrentFileLib.InitializationStatusChange += CurrentFileLibOnInitialized;
                if (IsVisible)
                {
                    CurrentFileLib?.Initialize();
                }
            }
            else if (CurrentFileLib?.IsInitialized == true)
            {
                FullyInitialized = true;
            }
            else
            {
                FullyInitialized = false;
                IsBusy = true;
                BusyText = "Recompiling local classes";
                if (IsVisible)
                {
                    CurrentFileLib?.Initialize();
                }
            }
            if (!IsBusy)
            {
                Decompile();
            }
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            ScriptText = string.Empty;
            RootNode = null;
            outputListBox.ItemsSource = null;
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new BytecodeEditor(), CurrentLoadedExport)
                {
                    Title = $"Script Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
            if (progressBarTimer is not null)
            {
                progressBarTimer.IsEnabled = false; //Stop timer
                progressBarTimer.Tick -= ProgressBarTimer_Tick;
            }

            if (CurrentFileLib is not null)
            {
                CurrentFileLib.InitializationStatusChange -= CurrentFileLibOnInitialized;
            }

            if (Document is not null)
            {
                Document.TextChanged -= TextChanged;
            }
            textEditor.TextArea.TextEntered -= TextAreaOnTextEntered;
        }


        private void ExportLoaderControl_Loaded(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);
            if (window is { })
            {
                window.Closed += (o, args) => UnloadFileLib();
            }
        }

        #region Busy variables
        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    IsBusyChanged?.Invoke(this, EventArgs.Empty); //caller will just fetch and update this value
                }
            }
        }

        public event EventHandler IsBusyChanged;

        private bool _busyProgressIndeterminate = true;
        public bool BusyProgressIndeterminate
        {
            get => _busyProgressIndeterminate;
            set => SetProperty(ref _busyProgressIndeterminate, value);
        }

        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        private int _busyProgressBarMax = 100;
        public int BusyProgressBarMax
        {
            get => _busyProgressBarMax;
            set => SetProperty(ref _busyProgressBarMax, value);
        }

        private int _busyProgressBarValue;
        public int BusyProgressBarValue
        {
            get => _busyProgressBarValue;
            set => SetProperty(ref _busyProgressBarValue, value);
        }
        #endregion

        #region ScriptLib Handling

        private bool _fullyInitialized;
        public bool FullyInitialized
        {
            get => _fullyInitialized;
            set => SetProperty(ref _fullyInitialized, value);
        }

        private FileLib CurrentFileLib;

        private readonly DispatcherTimer progressBarTimer;
        private void ProgressBarTimer_Tick(object sender, EventArgs e)
        {
            if (!IsBusy)
            {
                progressBarTimer.Stop();
                BusyProgressBarValue = 0;
            }

            BusyProgressIndeterminate = false;
            if (BusyProgressBarValue == 0)
            {
                BusyProgressBarValue = 20;
            }
            else if (BusyProgressBarValue < BusyProgressBarMax)
            {
                //we're making these values up
                BusyProgressBarValue += Math.Min(15, Math.Max((BusyProgressBarMax - BusyProgressBarValue) / 5, 2));
            }

            if (BusyProgressBarValue >= BusyProgressBarMax)
            {
                BusyProgressIndeterminate = true;
            }
        }

        private void UnloadFileLib()
        {
            if (CurrentFileLib is { })
            {
                CurrentFileLib.Dispose();
                CurrentFileLib.InitializationStatusChange -= CurrentFileLibOnInitialized;
                CurrentFileLib = null;
            }
        }

        private void CurrentFileLibOnInitialized(bool initialized)
        {
            if (initialized)
            {
                if (IsBusy)
                {
                    IsBusy = false;
                    if (CurrentFileLib?.HadInitializationError == true)
                    {
                        FullyInitialized = false;
                        if (MessageBoxResult.Yes == MessageBox.Show("Could not build script database for this file!\n\n" +
                                            "Functionality will be limited to script decompilation.\n\n\n" +
                                            "Do you want to see the compilation error log?", "Script Error", MessageBoxButton.YesNo))
                        {
                            Dispatcher.Invoke(() => new ListDialog(CurrentFileLib.InitializationLog.Content.Select(msg => msg.ToString()), 
                                                                   "Initialization Log", "", Window.GetWindow(this)).Show());
                        }
                    }
                    else
                    {
                        FullyInitialized = CurrentFileLib?.IsInitialized == true;
                    }
                    if (CurrentLoadedExport != null)
                    {
                        Decompile();
                    }
                }
            }
            else
            {
                IsBusy = true;
                BusyText = "Recompiling local classes";
                FullyInitialized = false;
                if (IsVisible)
                {
                    CurrentFileLib?.Initialize();
                }
            }
        }

        private void ExportLoaderControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true && !FullyInitialized)
            {
                if (!progressBarTimer.IsEnabled)
                {
                    progressBarTimer.Start();
                }

                CurrentFileLib?.Initialize();
            }
            else
            {
                IsBusy = false;
            }
        }

        #endregion

        private void outputListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems?.Count == 1 && e.AddedItems[0] is PositionedMessage msg)
            {
                textEditor.Focus();
                textEditor.Select(msg.Start.CharIndex, msg.End.CharIndex - msg.Start.CharIndex);
                textEditor.ScrollToLine(msg.Line);
            }
        }

        private void CompileToBytecode(object sender, RoutedEventArgs e)
        {
            if (ScriptText != null && CurrentLoadedExport != null)
            {
                if (CurrentLoadedExport.ClassName != "Function")
                {
                    outputListBox.ItemsSource = new[] { $"Can only compile functions right now. {(CurrentLoadedExport.IsDefaultObject ? "Defaults" : CurrentLoadedExport.ClassName)} compilation will be added in a future update." };
                    return;
                }

                (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(CurrentLoadedExport, ScriptText, CurrentFileLib);
                outputListBox.ItemsSource = log?.Content;
            }
        }

        private void Decompile_OnClick(object sender, RoutedEventArgs e)
        {
            if (CurrentLoadedExport != null)
            {
                Decompile();
            }
        }

        private void Decompile()
        {
            try
            {
                ASTNode ast = UnrealScriptCompiler.ExportToAstNode(CurrentLoadedExport, CurrentFileLib);
                if (ast is null)
                {
                    (RootNode, ScriptText) = (null, "Could not decompile!");
                    return;
                }
                var codeBuilder = new CodeBuilderVisitor<SyntaxInfoCodeFormatter, (string, SyntaxInfo)>();
                ast.AcceptVisitor(codeBuilder);
                (string text, SyntaxInfo syntaxInfo) = codeBuilder.GetOutput();
                Dispatcher.Invoke(() =>
                {
                    textEditor.SyntaxHighlighting = syntaxInfo;
                });
                _definitionLinkGenerator.Reset();
                Dispatcher.Invoke(() =>
                {
                    if (ast is Function && FullyInitialized && CurrentLoadedExport.Parent is ExportEntry parentExport)
                    {
                        try
                        {
                            (ASTNode func, MessageLog log, TokenStream<string> tokens) = UnrealScriptCompiler.CompileAST(text, CurrentLoadedExport.ClassName, Pcc.Game);

                            if (func is Function function && log.AllErrors.IsEmpty())
                            {
                                //compile body to ast so that symbol tokens will be associated with their definitions
                                UnrealScriptCompiler.CompileNewFunctionBodyAST(parentExport, text, function, log, CurrentFileLib);
                                _definitionLinkGenerator.SetTokens(tokens);
                                outputListBox.ItemsSource = log.Content;
                            }
                        }
                        catch (Exception e)
                        {
                            //
                        }
                    }

                    RootNode = ast;
                    ScriptText = text;
                    if (RootNode is Function {IsNative: false})
                    {
                        textEditor.IsReadOnly = false;
                        Document.TextChanged += TextChanged;
                    }
                    else
                    {
                        textEditor.IsReadOnly = true;
                    }
                });

            }
            catch (Exception e) when (!App.IsDebug)
            {
                (RootNode, ScriptText) = (null, $"Error occured while decompiling {CurrentLoadedExport?.InstancedFullPath}:\n\n{e.FlattenException()}");
            }
        }

        private void TextChanged(object sender, EventArgs e)
        {
            (ASTNode ast, MessageLog log, TokenStream<string> tokens) = UnrealScriptCompiler.CompileAST(ScriptText, CurrentLoadedExport.ClassName, Pcc.Game);
            try
            {

                if (ast != null && log.AllErrors.IsEmpty())
                {
                    if (ast is Function func && FullyInitialized && CurrentLoadedExport.Parent is ExportEntry parentExport)
                    {
                        (ast, _) = UnrealScriptCompiler.CompileNewFunctionBodyAST(parentExport, ScriptText, func, log, CurrentFileLib);

                        //var codeBuilder = new CodeBuilderVisitor<SyntaxInfoCodeFormatter, (string, SyntaxInfo)>();
                        //ast.AcceptVisitor(codeBuilder);
                        //(_, SyntaxInfo syntaxInfo) = codeBuilder.GetOutput();

                        _definitionLinkGenerator.SetTokens(tokens);
                        var syntaxInfo = new SyntaxInfo();
                        if (tokens.Any())
                        {
                            int firstLine = tokens.First().StartPos.Line - 1;
                            int lastLine = tokens.Last().EndPos.Line - 1;
                            //while (lastLine >= firstLine)
                            //{
                            //    syntaxInfo[lastLine].Clear();
                            //    lastLine--;
                            //}

                            int currentLine = firstLine;
                            int currentPos = 0;
                            foreach (Token<string> token in tokens)
                            {
                                int tokLine = token.StartPos.Line - 1;
                                if (tokLine > currentLine)
                                {
                                    currentLine = tokLine;
                                    currentPos = 0;
                                }
                                while (syntaxInfo.Count <= currentLine + 1)
                                {
                                    syntaxInfo.Add(new List<SyntaxSpan>());
                                }
                                int tokStart = token.StartPos.Column;
                                int tokEnd = token.EndPos.Column;
                                if (tokStart > currentPos)
                                {
                                    syntaxInfo[currentLine].Add(new SyntaxSpan(EF.None, tokStart - currentPos));
                                }

                                syntaxInfo[currentLine].Add(new SyntaxSpan(token.SyntaxType, tokEnd - tokStart));
                                currentPos = tokEnd;
                            }
                        }

                        textEditor.SyntaxHighlighting = syntaxInfo;
                    }
                }
            }
            catch (ParseException)
            {
                log.LogError("Parse Failed!");
            }
            catch (Exception exception) when(!ME3ExplorerCoreLib.IsDebug)
            {
                log.LogError($"Exception: {exception.Message}");
            }

            outputListBox.ItemsSource = log.Content;
        }


        CompletionWindow completionWindow;
        private void TextAreaOnTextEntered(object sender, TextCompositionEventArgs e)
        {
            //TODO: code completion
            // if (e.Text == ".")
            // {
            //     completionWindow = new CompletionWindow(textEditor.TextArea);
            //     IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
            //     data.Add(new CompletionData("foo", "baz"));
            //     data.Add(new CompletionData("bar"));
            //     completionWindow.Show();
            //     completionWindow.Closed += delegate {
            //         completionWindow = null;
            //     };
            // }
        }

        private void CompileAST_OnClick(object sender, RoutedEventArgs e)
        {
            if (ScriptText != null)
            {
                MessageLog log;
                (RootNode, log, _) = UnrealScriptCompiler.CompileAST(ScriptText, CurrentLoadedExport.ClassName, Pcc.Game);

                if (RootNode != null && log.AllErrors.IsEmpty())
                {
                    if (RootNode is Function func && FullyInitialized && CurrentLoadedExport.Parent is ExportEntry parentExport)
                    {
                        (RootNode, _) = UnrealScriptCompiler.CompileFunctionBodyAST(parentExport, ScriptText, func, log, CurrentFileLib);
                    }
                    var codeBuilder = new CodeBuilderVisitor<SyntaxInfoCodeFormatter, (string, SyntaxInfo)>();
                    RootNode.AcceptVisitor(codeBuilder);
                    (_, SyntaxInfo syntaxInfo) = codeBuilder.GetOutput();
                    textEditor.SyntaxHighlighting = syntaxInfo;
                }

                outputListBox.ItemsSource = log.Content;
            }
        }



        #region AvalonEditor

        private TextDocument _document;
        public TextDocument Document
        {
            get => _document;
            set => SetProperty(ref _document, value);
        }

        private FoldingManager foldingManager;
        private readonly BraceFoldingStrategy foldingStrategy = new();
        private readonly DefinitionLinkGenerator _definitionLinkGenerator;

        #endregion
    }
}