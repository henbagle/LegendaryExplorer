﻿using System;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Decompiling;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript
{
    public static class UnrealScriptCompiler
    {
        public static (ASTNode node, string text) DecompileExport(ExportEntry export, FileLib lib, PackageCache packageCache = null)
        {
            try
            {
                ASTNode astNode = ExportToAstNode(export, lib, packageCache);

                if (astNode != null)
                {
                    var codeBuilder = new CodeBuilderVisitor();
                    astNode.AcceptVisitor(codeBuilder);
                    return (astNode, codeBuilder.GetOutput());
                }
            }
            catch (Exception e) when (!LegendaryExplorerCoreLib.IsDebug)
            {
                return (null, $"Error occured while decompiling {export?.InstancedFullPath}:\n\n{e.FlattenException()}");
            }

            return (null, "Could not decompile!");
        }

        public static ASTNode ExportToAstNode(ExportEntry export, FileLib lib, PackageCache packageCache)
        {
            ASTNode astNode;
            switch (export.ClassName)
            {
                case "Class":
                    astNode = ScriptObjectToASTConverter.ConvertClass(export.GetBinaryData<UClass>(packageCache), true, lib, packageCache);
                    break;
                case "Function":
                    astNode = ScriptObjectToASTConverter.ConvertFunction(export.GetBinaryData<UFunction>(packageCache), lib: lib, packageCache: packageCache);
                    break;
                case "State":
                    astNode = ScriptObjectToASTConverter.ConvertState(export.GetBinaryData<UState>(packageCache), lib: lib, packageCache: packageCache);
                    break;
                case "Enum":
                    astNode = ScriptObjectToASTConverter.ConvertEnum(export.GetBinaryData<UEnum>(packageCache));
                    break;
                case "ScriptStruct":
                    astNode = ScriptObjectToASTConverter.ConvertStruct(export.GetBinaryData<UScriptStruct>(packageCache), packageCache, lib);
                    break;
                default:
                    if (export.ClassName.EndsWith("Property") && ObjectBinary.From(export, packageCache) is UProperty uProp)
                    {
                        astNode = ScriptObjectToASTConverter.ConvertVariable(uProp, packageCache);
                    }
                    else
                    {
                        astNode = ScriptObjectToASTConverter.ConvertDefaultProperties(export, lib, packageCache);
                    }
                    break;
            }
            return astNode;
        }

        public static (ASTNode ast, MessageLog log, TokenStream<string> tokens) CompileAST(string script, string type, MEGame game, bool isDefaultObject = false)
        {
            var log = new MessageLog();
            var tokens = new TokenStream<string>(new StringLexer(script, log));
            var parser = new ClassOutlineParser(tokens, game, log);
            try
            {
                ASTNode ast = isDefaultObject ? parser.TryParseDefaultProperties() : parser.ParseDocument(type);
                if (ast is null)
                {
                    log.LogError("Parse failed!");
                }
                else
                {
                    log.LogMessage("Parsed!");
                }
                return (ast, log, tokens);
            }
            catch (Exception e)
            {
                log.LogError($"Parse failed! Exception: {e}");
                return (null, log, tokens);
            }

        }

        //Used by M3. Do not change signature without good cause
        public static (ASTNode astNode, MessageLog log) CompileFunction(ExportEntry export, string scriptText, FileLib lib)
        {
            (ASTNode astNode, MessageLog log, _) = CompileAST(scriptText, export.ClassName, export.Game);
            if (astNode != null && log.AllErrors.IsEmpty())
            {
                if (astNode is Function func && lib.IsInitialized && export.Parent is ExportEntry parent)
                {
                    if (func.IsNative)
                    {
                        log.LogMessage("Cannot edit native functions!");
                        return (astNode, log);
                    }
                    try
                    {
                        astNode = CompileNewFunctionBodyAST(parent, func, log, lib);
                        if (log.AllErrors.Count > 0)
                        {
                            log.LogError("Parse failed!");
                            return (astNode, log);
                        }
                    }
                    catch (ParseException)
                    {
                        log.LogError("Parse failed!");
                        return (astNode, log);
                    }
                    catch (Exception exception)
                    {
                        log.LogError($"Parse failed! Exception: {exception}");
                        return (astNode, log);
                    }

                    if (astNode is Function funcFullAST)
                    {
                        try
                        {
                            ScriptObjectCompiler.Compile(funcFullAST, parent, export.GetBinaryData<UFunction>());
                            log.LogMessage("Compiled!");
                            return (astNode, log);
                        }
                        catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                        {
                            log.LogError($"Compilation failed! Exception: {exception}");
                            return (astNode, log);
                        }
                    }
                }
            }

            return (null, log);
        }

        public static (ASTNode astNode, MessageLog log) CompileState(ExportEntry export, string scriptText, FileLib lib)
        {
            (ASTNode astNode, MessageLog log, _) = CompileAST(scriptText, export.ClassName, export.Game);
            if (log.AllErrors.IsEmpty())
            {
                if (astNode is not State state)
                {
                    log.LogError("Tried to parse a State, but no State was found!");
                    return (null, log);
                }
                if (!lib.IsInitialized)
                {
                    log.LogError("FileLib not initialized!");
                    return (null, log);
                }
                if (export.Parent is not ExportEntry {IsClass: true} parent)
                {
                    log.LogError(export.InstancedFullPath + " does not have a Class Export as a parent!");
                    return (null, log);
                }

                try
                {
                    astNode = CompileNewStateBodyAST(parent, state, log, lib);
                    if (astNode is null || log.AllErrors.Count > 0)
                    {
                        log.LogError("Parse failed!");
                        return (astNode, log);
                    }
                }
                catch (ParseException)
                {
                    log.LogError("Parse failed!");
                    return (astNode, log);
                }
                catch (Exception exception)
                {
                    log.LogError($"Parse failed! Exception: {exception}");
                    return (astNode, log);
                }
                try
                {
                    ScriptObjectCompiler.Compile(astNode, parent, export.GetBinaryData<UState>());
                    log.LogMessage("Compiled!");
                    return (astNode, log);
                }
                catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    log.LogError($"Compilation failed! Exception: {exception}");
                    return (astNode, log);
                }
            }

            return (null, log);
        }

        public static (ASTNode astNode, MessageLog log) CompileStruct(ExportEntry export, string scriptText, FileLib lib, PackageCache packageCache = null)
        {
            (ASTNode astNode, MessageLog log, _) = CompileAST(scriptText, export.ClassName, export.Game);
            if (log.AllErrors.IsEmpty())
            {
                if (astNode is not Struct strct)
                {
                    log.LogError("Tried to parse a Struct, but no Struct was found!");
                    return (null, log);
                }
                if (!lib.IsInitialized)
                {
                    log.LogError("FileLib not initialized!");
                    return (null, log);
                }
                if (export.Parent is not ExportEntry { ClassName: "Class" or "ScriptStruct" } parent)
                {
                    log.LogError(export.InstancedFullPath + " does not have a Class or ScriptStruct Export as a parent!");
                    return (null, log);
                }

                try
                {
                    astNode = CompileNewStructAST(parent, strct, log, lib);
                    if (astNode is null || log.AllErrors.Count > 0)
                    {
                        log.LogError("Parse failed!");
                        return (astNode, log);
                    }
                }
                catch (ParseException)
                {
                    log.LogError("Parse failed!");
                    return (astNode, log);
                }
                catch (Exception exception)
                {
                    log.LogError($"Parse failed! Exception: {exception}");
                    return (astNode, log);
                }
                try
                {
                    ScriptObjectCompiler.Compile(astNode, parent, export.GetBinaryData<UScriptStruct>(), packageCache);
                    log.LogMessage("Compiled!");
                    return (astNode, log);
                }
                catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    log.LogError($"Compilation failed! Exception: {exception}");
                    return (astNode, log);
                }
            }

            return (null, log);
        }

        public static State CompileNewStateBodyAST(ExportEntry parentExport, State state, MessageLog log, FileLib lib)
        {
            var symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();

            if (parentExport.IsClass && symbols.TryGetType(parentExport.ObjectName.Instanced, out Class containingClass))
            {
                if (!containingClass.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(((Class)containingClass.Parent).GetInheritanceString());
                    symbols.PushScope(containingClass.Name);
                }

                int stateIdx = containingClass.States.FindIndex(s => s.Name.CaseInsensitiveEquals(state.Name));
                if (stateIdx == -1)
                {
                    containingClass.States.Add(state);
                }
                else
                {
                    symbols.RemoveSymbol(state.Name);
                    containingClass.States[stateIdx] = state;
                }

                state.Outer = containingClass;
                ClassValidationVisitor.RunAllPasses(state, log, symbols);
                CodeBodyParser.ParseState(state, parentExport.Game, symbols, log);

                return state;
            }

            return null;
        }

        public static Function CompileNewFunctionBodyAST(ExportEntry parentExport, Function func, MessageLog log, FileLib lib)
        {
            var symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();

            IContainsFunctions stateOrClass;

            if (parentExport.IsClass && symbols.TryGetType(parentExport.ObjectName.Instanced, out Class containingClass))
            {
                if (!containingClass.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(((Class)containingClass.Parent).GetInheritanceString());
                    symbols.PushScope(containingClass.Name);
                }

                stateOrClass = containingClass;
            }
            //in state
            else if (parentExport.Parent is ExportEntry { IsClass: true } classExport && symbols.TryGetType(classExport.ObjectNameString, out Class cls) &&
                cls.States.FirstOrDefault(s => s.Name.CaseInsensitiveEquals(parentExport.ObjectNameString)) is State state)
            {
                symbols.GoDirectlyToStack(((Class)cls.Parent).GetInheritanceString());
                symbols.PushScope(cls.Name);
                symbols.PushScope(state.Name);

                stateOrClass = state;
            }
            else
            {
                return null;
            }

            int funcIdx = stateOrClass.Functions.FindIndex(fun => fun.Name.CaseInsensitiveEquals(func.Name));
            if (funcIdx == -1)
            {
                symbols.AddSymbol(func.Name, func);
                stateOrClass.Functions.Add(func);
            }
            else
            {
                symbols.ReplaceSymbol(func.Name, func, true);
                stateOrClass.Functions[funcIdx] = func;
            }

            func.Outer = (ASTNode)stateOrClass;
            var validator = new ClassValidationVisitor(log, symbols, ValidationPass.ClassAndStructMembersAndFunctionParams);
            validator.VisitNode(func);
            validator.Pass = ValidationPass.BodyPass;
            validator.VisitNode(func);

            CodeBodyParser.ParseFunction(func, parentExport.Game, symbols, log);
            return func;
        }

        public static (ASTNode astNode, MessageLog log) CompileDefaultProperties(ExportEntry export, string scriptText, FileLib lib, PackageCache packageCache = null)
        {
            (ASTNode astNode, MessageLog log, _) = CompileAST(scriptText, export.ClassName, export.Game, true);
            if (log.AllErrors.IsEmpty())
            {
                if (astNode is not DefaultPropertiesBlock propBlock)
                {
                    log.LogError($"Tried to parse a {DEFAULTPROPERTIES}, but no {DEFAULTPROPERTIES} was found!");
                    return (null, log);
                }
                if (!lib.IsInitialized)
                {
                    log.LogError("FileLib not initialized!");
                    return (null, log);
                }
                if (export.Class is not ExportEntry { IsClass: true } classExport)
                {
                    log.LogError(export.InstancedFullPath + " does not have a Class Export!");
                    return (null, log);
                }

                try
                {
                    astNode = CompileDefaultPropertiesAST(classExport, propBlock, log, lib);
                    if (astNode is null || log.AllErrors.Count > 0)
                    {
                        log.LogError("Parse failed!");
                        return (astNode, log);
                    }
                }
                catch (ParseException)
                {
                    log.LogError("Parse failed!");
                    return (astNode, log);
                }
                catch (Exception exception)
                {
                    log.LogError($"Parse failed! Exception: {exception}");
                    return (astNode, log);
                }
                try
                {
                    ScriptPropertiesCompiler.CompileDefault__Object(propBlock, classExport, ref export, packageCache);
                    log.LogMessage("Compiled!");
                    return (astNode, log);
                }
                catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    log.LogError($"Compilation failed! Exception: {exception}");
                    return (astNode, log);
                }
            }

            return (null, log);
        }

        public static DefaultPropertiesBlock CompileDefaultPropertiesAST(ExportEntry classExport, DefaultPropertiesBlock propBlock, MessageLog log, FileLib lib)
        {
            SymbolTable symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();


            if (classExport.IsClass && symbols.TryGetType(classExport.ObjectName.Instanced, out Class propsClass))
            {
                if (!propsClass.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(((Class)propsClass.Parent).GetInheritanceString());
                    symbols.PushScope(propsClass.Name);
                }

                propsClass.DefaultProperties = propBlock;
                propBlock.Outer = propsClass;

                PropertiesBlockParser.Parse(propBlock, classExport.FileRef, symbols, log);

                return propBlock;
            }

            return null;
        }

        public static Struct CompileNewStructAST(ExportEntry parentExport, Struct strct, MessageLog log, FileLib lib)
        {
            var symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();
            if (symbols.TryGetType(parentExport.ObjectName.Instanced, out ObjectType containingObject))
            {
                if (!containingObject.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(containingObject.GetScope());
                }
                symbols.RemoveSymbol(strct.Name);
                symbols.RemoveType(strct.Name);
                foreach (VariableType innerStruct in strct.TypeDeclarations)
                {
                    symbols.RemoveType(innerStruct.Name);
                }

                int stateIdx = containingObject.TypeDeclarations.FindIndex(s => s is Struct && s.Name.CaseInsensitiveEquals(strct.Name));
                if (stateIdx == -1)
                {
                    containingObject.TypeDeclarations.Add(strct);
                }
                else
                {
                    containingObject.TypeDeclarations[stateIdx] = strct;
                }

                strct.Outer = containingObject;
                ClassValidationVisitor.RunAllPasses(strct, log, symbols);
                ParseDefaults(strct);

                return strct;

                void ParseDefaults(Struct s)
                {
                    symbols.PushScope(s.Name);
                    foreach (Struct innerStruct in s.TypeDeclarations.OfType<Struct>())
                    {
                        ParseDefaults(innerStruct);
                    }
                    var defaults = s.DefaultProperties;
                    if (defaults.Tokens is not null)
                    {
                        PropertiesBlockParser.Parse(defaults, parentExport.FileRef, symbols, log);
                    }
                    symbols.PopScope();
                }
            }

            return null;
        }
    }
}
