﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using SharpDX;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public sealed class Polys : ObjectBinary
    {
        public int PolyCount
        {
            get => Elements?.Length ?? 0;
            set => Array.Resize(ref Elements, value);
        }
        public int PolyMax;
        public UIndex Owner;
        public Poly[] Elements;

        protected override void Serialize(SerializingContainer2 sc)
        {
            int polyCount = PolyCount;
            sc.Serialize(ref polyCount);
            PolyCount = polyCount;
            sc.Serialize(ref PolyMax);
            sc.Serialize(ref Owner);

            for (int i = 0; i < PolyCount; i++)
            {
                sc.Serialize(ref Elements[i]);
            }
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            return new List<(UIndex, string)> { (Owner, "Owner") };
        }
    }
    public class Poly
    {
        public Vector3 Base;
        public Vector3 Normal;
        public Vector3 TextureU;
        public Vector3 TextureV;
        public Vector3[] Vertices;
        public int PolyFlags;
        public UIndex Actor;
        public NameReference ItemName;
        public UIndex Material;
        public int iLink;
        public int iBrushPoly;
        public float ShadowMapScale;
        public int LightingChannels;
        public LightmassPrimitiveSettings LightmassSettings; //ME3 only
        public NameReference RulesetVariation; //ME3 only
    }

    public class LightmassPrimitiveSettings
    {
        public bool bUseTwoSidedLighting;
        public bool bShadowIndirectOnly;
        public float FullyOccludedSamplesFraction;
        public bool bUseEmissiveForStaticLighting;
        public float EmissiveLightFalloffExponent;
        public float EmissiveLightExplicitInfluenceRadius;
        public float EmissiveBoost;
        public float DiffuseBoost;
        public float SpecularBoost;
    }
}

namespace ME3Explorer
{
    using Unreal.BinaryConverters;

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref LightmassPrimitiveSettings lps)
        {
            if (sc.IsLoading)
            {
                lps = new LightmassPrimitiveSettings();
            }
            sc.Serialize(ref lps.bUseTwoSidedLighting);
            sc.Serialize(ref lps.bShadowIndirectOnly);
            sc.Serialize(ref lps.FullyOccludedSamplesFraction);
            sc.Serialize(ref lps.bUseEmissiveForStaticLighting);
            sc.Serialize(ref lps.EmissiveLightFalloffExponent);
            sc.Serialize(ref lps.EmissiveLightExplicitInfluenceRadius);
            sc.Serialize(ref lps.EmissiveBoost);
            sc.Serialize(ref lps.DiffuseBoost);
            sc.Serialize(ref lps.SpecularBoost);
        }
        public static void Serialize(this SerializingContainer2 sc, ref Poly poly)
        {
            if (sc.IsLoading)
            {
                poly = new Poly();
            }
            sc.Serialize(ref poly.Base);
            sc.Serialize(ref poly.Normal);
            sc.Serialize(ref poly.TextureU);
            sc.Serialize(ref poly.TextureV);
            sc.Serialize(ref poly.Vertices, Serialize);
            sc.Serialize(ref poly.PolyFlags);
            sc.Serialize(ref poly.Actor);
            sc.Serialize(ref poly.ItemName);
            sc.Serialize(ref poly.Material);
            sc.Serialize(ref poly.iLink);
            sc.Serialize(ref poly.iBrushPoly);
            sc.Serialize(ref poly.ShadowMapScale);
            sc.Serialize(ref poly.LightingChannels);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref poly.LightmassSettings);
                sc.Serialize(ref poly.RulesetVariation);
            }
            else if (sc.IsLoading)
            {
                //defaults that won't break the lighting completely
                poly.LightmassSettings = new LightmassPrimitiveSettings
                {
                    FullyOccludedSamplesFraction = 1,
                    EmissiveLightFalloffExponent = 2,
                    DiffuseBoost = 1,
                    EmissiveBoost = 1,
                    SpecularBoost = 1,
                };
                poly.RulesetVariation = "None";
            }
        }

    }
}
