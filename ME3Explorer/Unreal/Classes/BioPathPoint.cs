﻿//This class was generated by ME3Explorer
//Author: Warranty Voider
//URL: http://sourceforge.net/projects/me3explorer/
//URL: http://me3explorer.freeforums.org/
//URL: http://www.facebook.com/pages/Creating-new-end-for-Mass-Effect-3/145902408865659
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;
using SharpDX;
using KFreonLib.Debugging;

namespace ME3Explorer.Unreal.Classes
{
    public class BioPathPoint
    {
        #region Unreal Props

        //Byte Properties

        public int Physics;
        //Bool Properties

        public bool bHasCrossLevelPaths = false;
        public bool bEnabled = false;
        public bool bBlocked = false;
        public bool bPathsChanged = false;
        public bool bHiddenEdGroup = false;
        public bool bMakeSourceOnly = false;
        //Name Properties

        public int Tag;
        public int Group;
        //Object Properties

        public int CylinderComponent;
        public int Base;
        public int CollisionComponent;
        public int nextNavigationPoint;
        //Float Properties

        public float DrawScale;
        //Integer Properties

        public int visitedWeight;
        public int bestPathWeight;
        public int NetworkID;
        public int ApproximateLineOfFire;
        //Vector3 Properties

        public Vector3 location;

        #endregion

        public int MyIndex;
        public ME3Package pcc;
        public byte[] data;
        public List<PropertyReader.Property> Props;
        public bool isSelected = false;
        public bool isEdited = false;

        public BioPathPoint(ME3Package Pcc, int Index)
        {
            pcc = Pcc;
            MyIndex = Index;
            if (pcc.isExport(Index))
                data = pcc.Exports[Index].Data;
            Props = PropertyReader.getPropList(pcc.Exports[Index]);
            
            foreach (PropertyReader.Property p in Props)
                switch (pcc.getNameEntry(p.Name))
                {
                    #region
                    case "Physics":
                        Physics = p.Value.IntValue;
                        break;
                    case "bHasCrossLevelPaths":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bHasCrossLevelPaths = true;
                        break;
                    case "bEnabled":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bEnabled = true;
                        break;
                    case "bBlocked":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bBlocked = true;
                        break;
                    case "bPathsChanged":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bPathsChanged = true;
                        break;
                    case "bHiddenEdGroup":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bHiddenEdGroup = true;
                        break;
                    case "bMakeSourceOnly":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bMakeSourceOnly = true;
                        break;
                    case "Tag":
                        Tag = p.Value.IntValue;
                        break;
                    case "Group":
                        Group = p.Value.IntValue;
                        break;
                    case "CylinderComponent":
                        CylinderComponent = p.Value.IntValue;
                        break;
                    case "Base":
                        Base = p.Value.IntValue;
                        break;
                    case "CollisionComponent":
                        CollisionComponent = p.Value.IntValue;
                        break;
                    case "nextNavigationPoint":
                        nextNavigationPoint = p.Value.IntValue;
                        break;
                    case "DrawScale":
                        DrawScale = BitConverter.ToSingle(p.raw, p.raw.Length - 4);
                        break;
                    case "visitedWeight":
                        visitedWeight = p.Value.IntValue;
                        break;
                    case "bestPathWeight":
                        bestPathWeight = p.Value.IntValue;
                        break;
                    case "NetworkID":
                        NetworkID = p.Value.IntValue;
                        break;
                    case "ApproximateLineOfFire":
                        ApproximateLineOfFire = p.Value.IntValue;
                        break;
                    case "location":
                        location = new Vector3(BitConverter.ToSingle(p.raw, p.raw.Length - 12),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 8),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 4));
                        break;
                    #endregion
                }
        }

        public void SaveChanges()
        {
            if (isEdited)
            {
                byte[] buff = Vector3ToBuff(location);
                int f = -1;
                for (int i = 0; i < Props.Count; i++)
                    if (pcc.getNameEntry(Props[i].Name) == "location")
                    {
                        f = i;
                        break;
                    };
                if (f != -1)//has prop
                {
                    int off = Props[f].offend - 12;
                    for (int i = 0; i < 12; i++)
                        data[off + i] = buff[i];
                }
                else//have to add prop
                {
                    DebugOutput.PrintLn(MyIndex + " : cant find location property");
                }
                pcc.Exports[MyIndex].Data = data;
            }
        }

        public byte[] Vector3ToBuff(Vector3 v)
        {
            MemoryStream m = new MemoryStream();
            
            m.Write(BitConverter.GetBytes(v.X), 0, 4);
            m.Write(BitConverter.GetBytes(v.Y), 0, 4);
            m.Write(BitConverter.GetBytes(v.Z), 0, 4);
            return m.ToArray();
        }

        public void ProcessTreeClick(int[] path, bool AutoFocus)
        {
            isSelected = true;
        }

        public void ApplyTransform(Matrix m)
        {
            if (isSelected)
            {
                location += new Vector3(m.M41, m.M42, m.M43);
                isEdited = true;
            }
        }

        public void SetSelection(bool Selected)
        {
            isSelected = Selected;
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode(pcc.Exports[MyIndex].ObjectName + "(#" + MyIndex + ")");
            res.Nodes.Add("Physics : " + pcc.getNameEntry(Physics));
            res.Nodes.Add("bHasCrossLevelPaths : " + bHasCrossLevelPaths);
            res.Nodes.Add("bEnabled : " + bEnabled);
            res.Nodes.Add("bBlocked : " + bBlocked);
            res.Nodes.Add("bPathsChanged : " + bPathsChanged);
            res.Nodes.Add("bHiddenEdGroup : " + bHiddenEdGroup);
            res.Nodes.Add("bMakeSourceOnly : " + bMakeSourceOnly);
            res.Nodes.Add("Tag : " + pcc.getNameEntry(Tag));
            res.Nodes.Add("Group : " + pcc.getNameEntry(Group));
            res.Nodes.Add("CylinderComponent : " + CylinderComponent);
            res.Nodes.Add("Base : " + Base);
            res.Nodes.Add("CollisionComponent : " + CollisionComponent);
            res.Nodes.Add("nextNavigationPoint : " + nextNavigationPoint);
            res.Nodes.Add("DrawScale : " + DrawScale);
            res.Nodes.Add("visitedWeight : " + visitedWeight);
            res.Nodes.Add("bestPathWeight : " + bestPathWeight);
            res.Nodes.Add("NetworkID : " + NetworkID);
            res.Nodes.Add("ApproximateLineOfFire : " + ApproximateLineOfFire);
            return res;
        }

    }
}