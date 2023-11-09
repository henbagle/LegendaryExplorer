using System;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests.IntegrationTests;

[TestClass]
public class TextureTests
{
    [TestMethod]
    public void TestTextureOperations()
    {
        GlobalTest.Init();
        var packagesPath = GlobalTest.GetTestTexturesDirectory();
        var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
        foreach (var p in packages)
        {
            if (p.RepresentsPackageFilePath())
            {
                // Do not use package caching in tests
                Console.WriteLine($"Opening package {p}");
                (var game, var platform) = GlobalTest.GetExpectedTypes(p);
                if (platform == MEPackage.GamePlatform.PC)
                {
                    var loadedPackage = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);
                    foreach (var textureExp in loadedPackage.Exports.Where(x => x.IsTexture()))
                    {
                        Texture2D.GetTextureCRC(textureExp);

                        var t2d = new Texture2D(textureExp);
                        var mips = Texture2D.GetTexture2DMipInfos(textureExp, t2d.GetTopMip().TextureCacheName);
                        foreach (var v in t2d.Mips)
                        {
                            var displayStr = v.MipDisplayString;
                            var texCache = v.TextureCacheName;
                            var textureData = Texture2D.GetTextureData(v, v.Export.Game);
                            var imageDataFromInternal = t2d.GetImageBytesForMip(v, v.Export.Game, false, out _);
                            if (!textureData.AsSpan().SequenceEqual(imageDataFromInternal))
                            {
                                Assert.Fail($"Texture data accessed using wrapper and internal method did not match! Export: {textureExp.InstancedFullPath} in {p}. Static size: {textureData.Length} Instance size: {imageDataFromInternal.Length}");
                            }
                        }
                        t2d.RemoveEmptyMipsFromMipList();
                        using MemoryStream ms = MemoryManager.GetMemoryStream();
                        t2d.SerializeNewData(ms);
                    }
                }
            }
        }
    }
}