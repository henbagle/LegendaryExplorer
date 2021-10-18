﻿using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Compiling
{
    public static class ScriptObjectCompiler
    {
        public static void Compile(ASTNode node, IEntry parent, UField existingObject = null, PackageCache packageCache = null)
        {
            switch (node)
            {
                case Class classAST:
                    if (existingObject is null or UClass)
                    {
                        UClass uClass = (UClass)existingObject;
                        Compile(classAST, parent, ref uClass);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UClass)}!");
                    }
                case Const constAST:
                    if (existingObject is null or UConst)
                    {
                        UConst uConst = (UConst)existingObject;
                        Compile(constAST, parent, ref uConst);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UConst)}!");
                    }
                case Enumeration enumAST:
                    if (existingObject is null or UEnum)
                    {
                        UEnum uEnum = (UEnum)existingObject;
                        Compile(enumAST, parent, ref uEnum);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UEnum)}!");
                    }
                case Function funcAST:
                    if (existingObject is null or UFunction)
                    {
                        UFunction uFunction = (UFunction)existingObject;
                        Compile(funcAST, parent, ref uFunction);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UFunction)}!");
                    }
                case State stateAST:
                    if (existingObject is null or UState)
                    {
                        UState uState = (UState)existingObject;
                        Compile(stateAST, parent, ref uState);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UState)}!");
                    }
                case Struct structAST:
                    if (existingObject is null or UScriptStruct)
                    {
                        UScriptStruct uScriptStruct = (UScriptStruct)existingObject;
                        Compile(structAST, parent, ref uScriptStruct);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UScriptStruct)}!");
                    }
                case VariableDeclaration varDeclAST:
                    if (existingObject is null or UProperty)
                    {
                        UProperty uProp = (UProperty)existingObject;
                        Compile(varDeclAST, parent, ref uProp);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Expected {nameof(existingObject)} to be of type {nameof(UProperty)}!");
                    }
            }
            throw new ArgumentOutOfRangeException(nameof(node));
        }

        public static void Compile(Class classAST, IEntry parent, ref UClass classObj)
        {
            throw new NotImplementedException();
        }

        public static void Compile(State stateAST, IEntry parent, ref UState stateObj)
        {
            IEntry super = null;
            if (stateAST.Parent is not null)
            {
                super = CompilerUtils.ResolveState(stateAST.Parent, parent.FileRef);
            }

            var stateName = NameReference.FromInstancedString(stateAST.Name);
            ExportEntry stateExport;
            if (stateObj is null)
            {
                stateExport = CreateNewExport(stateName, "State", parent, new UState { ScriptBytes = Array.Empty<byte>(), LocalFunctionMap = new()}, super);
                stateObj = stateExport.GetBinaryData<UState>();
            }
            else
            {
                stateExport = stateObj.Export;
                if (stateExport.SuperClass != super)
                {
                    stateExport.SuperClass = super;
                }
                if (stateExport.ObjectName != stateName)
                {
                    stateExport.ObjectName = stateName;
                }
            }

            stateObj.StateFlags = stateAST.Flags;
            stateObj.ProbeMask = 0;
            stateObj.IgnoreMask = stateAST.IgnoreMask;
            stateObj.SuperClass = super?.UIndex ?? 0;


            //calculate probemask
            State curState = stateAST;
            while (curState is not null)
            {
                foreach (Function stateFunc in curState.Functions)
                {
                    if (Enum.TryParse(stateFunc.Name, true, out EProbeFunctions enumVal))
                    {
                        stateObj.ProbeMask |= enumVal;
                    }
                }
                curState = curState.Parent;
            }

            stateObj.LocalFunctionMap.Clear();

            UField prevFunc = null;
            var existingFuncs = GetMembers<UFunction>(stateObj).ToDictionary(uFunc => uFunc.Export.ObjectName.Instanced);
            stateObj.Children = 0;
            foreach (Function member in stateAST.Functions)
            {
                existingFuncs.Remove(member.Name, out UFunction childFunc);
                Compile(member, stateExport, ref childFunc);
                prevFunc = AdvanceField(prevFunc, childFunc, stateObj);
                stateObj.LocalFunctionMap.Add(childFunc.Export.ObjectName, childFunc.Export.UIndex);
            }
            foreach (UFunction removedFunc in existingFuncs.Values)
            {
                EntryPruner.TrashEntryAndDescendants(removedFunc.Export);
            }

            if (prevFunc is not null)
            {
                prevFunc.Next = 0;
                prevFunc.Export.WriteBinary(prevFunc);
            }

            ByteCodeCompilerVisitor.Compile(stateAST, stateObj);
        }

        public static void Compile(Function funcAST, IEntry parent, ref UFunction funcObj)
        {
            IEntry super = null;
            if (funcAST.SuperFunction is not null)
            {
                super = CompilerUtils.ResolveFunction(funcAST.SuperFunction, parent.FileRef);
            }

            var functionName = NameReference.FromInstancedString(funcAST.Name);
            ExportEntry funcExport;
            if (funcObj is null)
            {
                funcExport = CreateNewExport(functionName, "Function", parent, new UFunction { ScriptBytes = Array.Empty<byte>(), FriendlyName = functionName}, super);
                funcObj = funcExport.GetBinaryData<UFunction>();
            }
            else
            {
                funcExport = funcObj.Export;
                if (funcExport.SuperClass != super)
                {
                    funcExport.SuperClass = super;
                }
                if (funcExport.ObjectName != functionName)
                {
                    funcExport.ObjectName = functionName;
                }
            }

            funcObj.FriendlyName = functionName;
            funcObj.FunctionFlags = funcAST.Flags;
            funcObj.NativeIndex = (ushort)funcAST.NativeIndex;
            funcObj.OperatorPrecedence = funcAST.OperatorPrecedence;
            funcObj.SuperClass = super?.UIndex ?? 0;

            var newMembers = new List<VariableDeclaration>();
            newMembers.AddRange(funcAST.Parameters);
            if (funcAST.ReturnValueDeclaration is not null)
            {
                newMembers.Add(funcAST.ReturnValueDeclaration);
            }
            newMembers.AddRange(funcAST.Locals);

            UField prevProp = null;
            using (var existingEnumerator = GetMembers<UField>(funcObj).GetEnumerator())
            {
                funcObj.Children = 0;
                foreach (VariableDeclaration member in newMembers)
                {
                    UProperty childProp = null;
                    while (existingEnumerator.MoveNext())
                    {
                        UField current = existingEnumerator.Current;
                        if (current.Export.ClassName == ByteCodeCompilerVisitor.PropertyTypeName(member.VarType))
                        {
                            childProp = (UProperty)current;
                            break;
                        }
                        EntryPruner.TrashEntryAndDescendants(current.Export);
                    }
                    Compile(member, funcExport, ref childProp);
                    prevProp = AdvanceField(prevProp, childProp, funcObj);
                }
                while (existingEnumerator.MoveNext())
                {
                    EntryPruner.TrashEntryAndDescendants(existingEnumerator.Current.Export);
                }
            }

            if (prevProp is not null)
            {
                prevProp.Next = 0;
                prevProp.Export.WriteBinary(prevProp);
            }

            ByteCodeCompilerVisitor.Compile(funcAST, funcObj);
        }

        public static void Compile(Struct structAST, IEntry parent, ref UScriptStruct structObj, PackageCache packageCache = null)
        {
            IMEPackage pcc = parent.FileRef;
            IEntry super = null;
            if (structAST.Parent is Struct parentStruct)
            {
                super = CompilerUtils.ResolveStruct(parentStruct, pcc);
            }

            var structName = NameReference.FromInstancedString(structAST.Name);
            ExportEntry structExport;
            if (structObj is null)
            {
                structExport = CreateNewExport(structName, "ScriptStruct", parent, UScriptStruct.Create(), super);
                structObj = structExport.GetBinaryData<UScriptStruct>();
            }
            else
            {
                structExport = structObj.Export;
                structExport.SuperClass = super;
                structExport.ObjectName = structName;
            }
            structObj.StructFlags = structAST.Flags;
            structObj.SuperClass = super?.UIndex ?? 0;

            (CaseInsensitiveDictionary<UScriptStruct> existingSubStructs, CaseInsensitiveDictionary<UProperty> existingProps) = GetMembers<UScriptStruct, UProperty>(structObj);
            structObj.Children = 0;

            var subStructs = new List<UScriptStruct>();
            foreach (Struct subStructAST in structAST.TypeDeclarations.OfType<Struct>())
            {
                existingSubStructs.Remove(subStructAST.Name, out UScriptStruct subStruct);
                Compile(subStructAST, structExport, ref subStruct);
                subStructs.Add(subStruct);
            }
            foreach (UScriptStruct removedSubStruct in existingSubStructs.Values)
            {
                EntryPruner.TrashEntryAndDescendants(removedSubStruct.Export);
            }

            UField prevField = null;
            if (pcc.Game <= MEGame.ME2)
            {
                foreach (UScriptStruct current in subStructs)
                {
                    prevField = AdvanceField(prevField, current, structObj);
                }
            }
            foreach (VariableDeclaration member in structAST.VariableDeclarations)
            {
                existingProps.Remove(member.Name, out UProperty current);
                if (current is not null && !current.Export.ClassName.CaseInsensitiveEquals(ByteCodeCompilerVisitor.PropertyTypeName(member.VarType)))
                {
                    EntryPruner.TrashEntryAndDescendants(current.Export);
                    current = null;
                }
                Compile(member, structExport, ref current);
                prevField = AdvanceField(prevField, current, structObj);
            }
            if (pcc.Game > MEGame.ME2)
            {
                foreach (UScriptStruct current in subStructs)
                {
                    prevField = AdvanceField(prevField, current, structObj);
                }
            }
            if (prevField is not null)
            {
                prevField.Next = 0;
                prevField.Export.WriteBinary(prevField);
            }
            structObj.Defaults = structAST.GetDefaultPropertyCollection(pcc, false, packageCache);
            structObj.Export.WriteBinary(structObj);
        }

        public static void Compile(Enumeration enumAST, IEntry parent, ref UEnum enumObj)
        {
            throw new NotImplementedException();
        }

        public static void Compile(VariableDeclaration varDeclAST, IEntry parent, ref UProperty propObj)
        {
            IMEPackage pcc = parent.FileRef;
            VariableType varType = varDeclAST.VarType;
            if (varType is StaticArrayType staticArrayType)
            {
                varType = staticArrayType.ElementType;
            }

            NameReference propName = NameReference.FromInstancedString(varDeclAST.Name);
            if (propObj is null)
            {
                string className = ByteCodeCompilerVisitor.PropertyTypeName(varType);
                UProperty tmp = className switch
                {
                    "BioMask4Property" => new UBioMask4Property(),
                    "ByteProperty" => new UByteProperty(),
                    "IntProperty" => new UIntProperty(),
                    "BoolProperty" => new UBoolProperty(),
                    "FloatProperty" => new UFloatProperty(),
                    "ClassProperty" => new UClassProperty(),
                    "ComponentProperty" => new UComponentProperty(),
                    "ObjectProperty" => new UObjectProperty(),
                    "NameProperty" => new UNameProperty(),
                    "DelegateProperty" => new UDelegateProperty(),
                    "InterfaceProperty" => new UInterfaceProperty(),
                    "StructProperty" => new UStructProperty(),
                    "StrProperty" => new UStrProperty(),
                    "MapProperty" => new UMapProperty(),
                    "StringRefProperty" => new UStringRefProperty(),
                    "ArrayProperty" => new UArrayProperty(),
                    _ => throw new ArgumentOutOfRangeException(nameof(className), className, "")
                };
                tmp.Category = "None";
                propObj = (UProperty)ObjectBinary.From(CreateNewExport(propName, className, parent, tmp));
            }
            else
            {
                if (propObj.Export.ObjectName != propName)
                {
                    propObj.Export.ObjectName = propName;
                }
            }

            propObj.ArraySize = varDeclAST.ArrayLength;
            propObj.PropertyFlags = varDeclAST.Flags;
            propObj.Category = NameReference.FromInstancedString(varDeclAST.Category);


            switch (propObj)
            {
                case UByteProperty uByteProperty:
                    uByteProperty.Enum = varType is Enumeration ? CompilerUtils.ResolveSymbol(varType, pcc).UIndex : 0;
                    break;
                case UClassProperty uClassProperty:
                    uClassProperty.ObjectRef = pcc.getEntryOrAddImport("Core.Class").UIndex;
                    uClassProperty.ClassRef = CompilerUtils.ResolveSymbol(((ClassType)varType).ClassLimiter, pcc).UIndex;
                    break;
                case UDelegateProperty uDelegateProperty:
                    uDelegateProperty.Function = CompilerUtils.ResolveFunction(((DelegateType)varType).DefaultFunction, pcc).UIndex;
                    string parentClassName = parent.ClassName;
                    if (parentClassName.CaseInsensitiveEquals("ArrayProperty"))
                    {
                        parentClassName = parent.Parent.ClassName;
                    }
                    uDelegateProperty.Delegate = parentClassName.CaseInsensitiveEquals("Function") ? uDelegateProperty.Function : 0;
                    break;
                case UMapProperty uMapProperty:
                    uMapProperty.KeyType = 0;
                    uMapProperty.ValueType = 0;
                    break;
                case UObjectProperty uObjectProperty:
                    uObjectProperty.ObjectRef = CompilerUtils.ResolveSymbol(varType, pcc).UIndex;
                    break;
                case UStructProperty uStructProperty:
                    uStructProperty.Struct = CompilerUtils.ResolveSymbol(varType, pcc).UIndex;
                    break;
                case UArrayProperty uArrayProperty:
                    UProperty child = null;
                    var dynArrType = (DynamicArrayType)varType;
                    VariableType elementType = dynArrType.ElementType;
                    if (pcc.TryGetUExport(uArrayProperty.ElementType ?? 0, out ExportEntry childExp))
                    {
                        if (childExp.ClassName == ByteCodeCompilerVisitor.PropertyTypeName(elementType))
                        {
                            child = (UProperty)ObjectBinary.From(pcc.GetUExport(uArrayProperty.ElementType));
                        }
                        else
                        {
                            EntryPruner.TrashEntryAndDescendants(childExp);
                        }
                    }
                    Compile(new VariableDeclaration(elementType, dynArrType.ElementPropertyFlags, propName), uArrayProperty.Export, ref child);
                    child.Export.WriteBinary(child);
                    uArrayProperty.ElementType = child.Export.UIndex;
                    break;

                //have no properties beyond the base class
                //case UIntProperty uIntProperty:
                //    break;
                //case UBoolProperty uBoolProperty:
                //    break;
                //case UFloatProperty uFloatProperty:
                //    break;
                //case UNameProperty uNameProperty:
                //    break;
                //case UStringRefProperty uStringRefProperty:
                //    break;
                //case UStrProperty uStrProperty:
                //    break;

                //have no properties of their own, handled by their base classes above
                //case UComponentProperty uComponentProperty:
                //    break;
                //case UInterfaceProperty uInterfaceProperty:
                //    break;
                //case UBioMask4Property uBioMask4Property:
                //    break;
            }

        }

        public static void Compile(Const constAST, IEntry parent, ref UConst constObj)
        {
            throw new NotImplementedException();
        }

        private static UField AdvanceField(UField field, UField current, UStruct uStruct)
        {
            if (field is null)
            {
                uStruct.Children = current.Export;
            }
            else
            {
                field.Next = current.Export;
                field.Export.WriteBinary(field);
            }
            return current;
        }

        public static List<T> GetMembers<T>(UStruct obj) where T : UField
        {
            IMEPackage pcc = obj.Export.FileRef;

            var members = new List<T>();

            var nextItem = obj.Children;

            while (nextItem is not null && pcc.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild);
                switch (objBin)
                {
                    case T field:
                        nextItem = field.Next;
                        members.Add(field);
                        break;
                    case UField field:
                        nextItem = field.Next;
                        break;
                    default:
                        nextItem = null;
                        break;
                }
            }
            return members;
        }

        public static (CaseInsensitiveDictionary<T>, CaseInsensitiveDictionary<U>) GetMembers<T, U>(UStruct obj) where T : UField where U : UField
        {
            IMEPackage pcc = obj.Export.FileRef;

            var membersT = new CaseInsensitiveDictionary<T>();
            var membersU = new CaseInsensitiveDictionary<U>();

            var nextItem = obj.Children;

            while (nextItem is not null && pcc.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild);
                switch (objBin)
                {
                    case T tField:
                        nextItem = tField.Next;
                        membersT.Add(tField.Export.ObjectName.Instanced, tField);
                        break;
                    case U uField:
                        nextItem = uField.Next;
                        membersU.Add(uField.Export.ObjectName.Instanced, uField);
                        break;
                    case UField field:
                        nextItem = field.Next;
                        break;
                    default:
                        nextItem = null;
                        break;
                }
            }
            return (membersT, membersU);
        }

        private static ExportEntry CreateNewExport(NameReference name, string className, IEntry parent, UField binary = null, IEntry super = null)
        {
            IMEPackage pcc = parent.FileRef;

            //reuse trash exports
            if (pcc.TryGetTrash(out ExportEntry trashExport))
            {
                trashExport.ObjectName = name;
                trashExport.Class = EntryImporter.EnsureClassIsInFile(pcc, className);
                trashExport.SuperClass = super;
                trashExport.Parent = parent;
                trashExport.WritePrePropsAndPropertiesAndBinary(new byte[4], new PropertyCollection(), (ObjectBinary)binary ?? new GenericObjectBinary(new byte[0]));
                return trashExport;
            }

            var exp = new ExportEntry(pcc, parent, name, binary: binary, isClass: binary is UClass)
            {
                Class = EntryImporter.EnsureClassIsInFile(pcc, className),
                SuperClass = super
            };
            pcc.AddExport(exp);
            return exp;
        }
    }
}
