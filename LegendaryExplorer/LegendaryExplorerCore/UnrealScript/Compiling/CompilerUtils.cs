﻿using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorerCore.UnrealScript.Compiling
{
    public static class CompilerUtils
    {
        public static bool TryGetTrash<T>(this IMEPackage pcc, out T entry) where T : class, IEntry
        {
            entry = pcc.FindEntry(UnrealPackageFile.TrashPackageName)?.GetChildren<T>().LastOrDefault();
            return entry is not null;
        }

        public static IEntry ResolveSymbol(ASTNode node, IMEPackage pcc) =>
            node switch
            {
                Class cls => ResolveClass(cls, pcc),
                Struct strct => ResolveStruct(strct, pcc),
                State state => ResolveState(state, pcc),
                Function func => ResolveFunction(func, pcc),
                Enumeration @enum => ResolveEnum(@enum, pcc),
                StaticArrayType statArr => ResolveSymbol(statArr.ElementType, pcc),
                _ => throw new ArgumentOutOfRangeException(nameof(node))
            };

        public static IEntry ResolveEnum(Enumeration e, IMEPackage pcc) => pcc.getEntryOrAddImport($"{ResolveSymbol(e.Outer, pcc).InstancedFullPath}.{e.Name}", "Enum");
        public static IEntry ResolveStruct(Struct s, IMEPackage pcc) => pcc.getEntryOrAddImport($"{ResolveSymbol(s.Outer, pcc).InstancedFullPath}.{s.Name}", "ScriptStruct");
        public static IEntry ResolveFunction(Function f, IMEPackage pcc) => pcc.getEntryOrAddImport($"{ResolveSymbol(f.Outer, pcc).InstancedFullPath}.{f.Name}", "Function");
        public static IEntry ResolveState(State s, IMEPackage pcc) => pcc.getEntryOrAddImport($"{ResolveSymbol(s.Outer, pcc).InstancedFullPath}.{s.Name}", "State");

        public static IEntry ResolveClass(Class c, IMEPackage pcc) =>
            EntryImporter.EnsureClassIsInFile(pcc, c.Name, RelinkResultsAvailable: relinkResults =>
                throw new Exception($"Unable to resolve class '{c.Name}'! There were relinker errors: {string.Join("\n\t", relinkResults.Select(pair => pair.Message))}"));
    }
}