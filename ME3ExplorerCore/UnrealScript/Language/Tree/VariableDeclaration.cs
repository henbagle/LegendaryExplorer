﻿using System.Collections.Generic;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class VariableDeclaration : Statement, IHasFileReference
    {
        public UnrealFlags.EPropertyFlags Flags;

        public VariableType VarType;

        public string Category;
        public string Name { get; }

        public int ArrayLength;

        public bool IsStaticArray => ArrayLength > 1;

        public VariableDeclaration(VariableType type, UnrealFlags.EPropertyFlags flags,
                                   string name, int arrayLength = 0, string category = null, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.VariableDeclaration, start, end)
        {
            Flags = flags;
            Name = name;
            ArrayLength = arrayLength;
            Category = category;
            VarType = IsStaticArray  && !(type is StaticArrayType) ? new StaticArrayType(type, ArrayLength) : type;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return VarType;
            }
        }

        public int GetSize() => VarType?.Size ?? 0;

        public string FilePath { get; init; }
        public int UIndex { get; set; }
    }
}
