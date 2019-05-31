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
using SharpDX;
using KFreonLib.Debugging;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.Classes
{
    public class SplineActor
    {
        #region Unreal Props

        //Byte Properties

        public int Physics = -1;
        //Bool Properties

        public bool bEdShouldSnap = false;
        public bool bDisableDestination = false;
        //Name Properties

        public int Tag = -1;
        public int UniqueTag = -1;
        //Vector3 Properties

        public Vector3 Rotator;
        public Vector3 location;
        //Array Property
        public byte[] Connections = new byte[0];

        #endregion

        public int MyIndex;
        public ME3Package pcc;
        public byte[] data;
        public List<PropertyReader.Property> Props;
        
        public Vector3 to;
        public int toIdx;
        public List<Vector3> from;
        public List<int> fromIdx;
        public bool isSelected = false;
        public bool isEdited = false;

        public static Vector3 GetLocation(ME3Package Pcc, int Index)
        {
            Vector3 r = new Vector3();
            if (!Pcc.isExport(Index))
                return new Vector3();
            List<PropertyReader.Property> pp = PropertyReader.getPropList(Pcc.Exports[Index]);
            foreach (PropertyReader.Property p in pp)
                switch (Pcc.getNameEntry(p.Name))
                {
                    case "location":
                        r = new Vector3(BitConverter.ToSingle(p.raw, p.raw.Length - 12),
                                        BitConverter.ToSingle(p.raw, p.raw.Length - 8),
                                        BitConverter.ToSingle(p.raw, p.raw.Length - 4));
                        break;
                }
            return r;
        }

        public SplineActor(ME3Package Pcc, int Index)
        {
            pcc = Pcc;
            MyIndex = Index;
            if (pcc.isExport(Index))
                data = pcc.Exports[Index].Data;
            Props = PropertyReader.getPropList(pcc.Exports[Index]);
            
            foreach (PropertyReader.Property p in Props)
                switch (pcc.getNameEntry(p.Name))
                {

                    case "Physics":
                        Physics = p.Value.IntValue;
                        break;
                    case "bEdShouldSnap":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bEdShouldSnap = true;
                        break;
                    case "bDisableDestination":
                        if (p.raw[p.raw.Length - 1] == 1)
                            bDisableDestination = true;
                        break;
                    case "Tag":
                        Tag = p.Value.IntValue;
                        break;
                    case "UniqueTag":
                        UniqueTag = p.Value.IntValue;
                        break;
                    case "Rotation":
                        Rotator = new Vector3(BitConverter.ToInt32(p.raw, p.raw.Length - 12),
                                              BitConverter.ToInt32(p.raw, p.raw.Length - 8),
                                              BitConverter.ToInt32(p.raw, p.raw.Length - 4));
                        break;
                    case "location":
                        location = new Vector3(BitConverter.ToSingle(p.raw, p.raw.Length - 12),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 8),
                                              BitConverter.ToSingle(p.raw, p.raw.Length - 4));
                        if(to == new Vector3())
                            to = location;
                        break;
                    case "Connections" :
                        Connections = p.raw;
                        break;
                }
            ProcessConnections();
        }

        public void ProcessConnections()
        {
            if (Connections.Length == 0)
                return;
            byte[] buff = GetArrayContent(Connections);
            List<PropertyReader.Property> pp = PropertyReader.ReadProp(pcc, buff, 0);
            int f = -1;
            toIdx = -1;
            foreach (PropertyReader.Property p in pp)
                if (pcc.getNameEntry(p.Name) == "ConnectTo")
                    f = p.Value.IntValue - 1;
            if (pcc.isExport(f) && pcc.Exports[f].ClassName == "SplineActor")
            {
                to = SplineActor.GetLocation(pcc, f);
                toIdx = f;
            }
            from = new List<Vector3>();
            fromIdx = new List<int>();
            buff = new byte[0];
            foreach (PropertyReader.Property p in Props)
                if (pcc.getNameEntry(p.Name) == "LinksFrom")
                    buff = GetArrayContent(p.raw);
            
            for (int i = 0; i < buff.Length / 4; i++)
            {
                int Idx = BitConverter.ToInt32(buff, i * 4) - 1;
                fromIdx.Add(Idx);
                from.Add(SplineActor.GetLocation(pcc, Idx));
            }
        }

        public void ApplyTransform(Matrix m, List<SplineActor> others)
        {
            if (isSelected)
            {
                Vector3 NewLoc = location + new Vector3(m.M41, m.M42, m.M43);
                Vector3 d = new Vector3(m.M41, m.M42, m.M43);
                /*for (int i = 0; i < points.Length - 16; i++)
                {
                    if (IsEqual(points[i].Position, location))
                        points[i].Position = NewLoc;
                    if (IsEqual(points_sel[i].Position, location))
                        points_sel[i].Position = NewLoc;
                }
                for (int i = points.Length - 16; i < points.Length; i++)
                {
                    points[i].Position += d;
                    points_sel[i].Position += d;
                }
                foreach (SplineActor o in others)
                {
                    for (int i = 0; i < points.Length - 16; i++)
                    {
                        if (IsEqual(o.points[i].Position, location))
                            o.points[i].Position = NewLoc;
                        if (IsEqual(points_sel[i].Position, location))
                            o.points_sel[i].Position = NewLoc;
                    }
                }*/
                location = NewLoc;
            }
        }

        public bool IsEqual(Vector3 v1, Vector3 v2)
        {
            if (v1.X == v2.X &&
                v1.Y == v2.Y &&
                v1.Z == v2.Z)
                return true;
            return false;
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

        public byte[] GetArrayContent(byte[] raw)
        {
            byte[] buff = new byte[raw.Length - 28];
            for (int i = 0; i < raw.Length - 28; i++)
                buff[i] = raw[i + 28];
            return buff;
        }

        public void ProcessTreeClick(int[] path, bool AutoFocus)
        {
            isSelected = true;
        }

        public void SetSelection(bool Selected)
        {
            isSelected = Selected;
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode(pcc.Exports[MyIndex].ObjectName + "(#" + MyIndex + ")");
            res.Nodes.Add("Physics : " + pcc.getNameEntry(Physics));
            res.Nodes.Add("bEdShouldSnap : " + bEdShouldSnap);
            res.Nodes.Add("bDisableDestination : " + bDisableDestination);
            res.Nodes.Add("Tag : " + pcc.getNameEntry(Tag));
            res.Nodes.Add("UniqueTag : " + pcc.getNameEntry(UniqueTag));
            return res;
        }

    }
}