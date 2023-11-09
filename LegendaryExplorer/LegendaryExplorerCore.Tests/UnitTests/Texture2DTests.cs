using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests.UnitTests
{
    [TestClass]
    public class Texture2DTests
    {
        [TestMethod]
        public void CalculateStorageType_ConvertsCorrectGameStorageType()
        {
            // Empty and LZMA always return the same
            Assert.AreEqual(StorageTypes.empty, Texture2D.CalculateStorageType(StorageTypes.empty, MEGame.ME3, false));
            Assert.AreEqual(StorageTypes.empty, Texture2D.CalculateStorageType(StorageTypes.empty, MEGame.ME3, true));
            Assert.AreEqual(StorageTypes.extLZMA, Texture2D.CalculateStorageType(StorageTypes.extLZMA, MEGame.ME3, false));

            // ME3 - Following storage types should become Zlib
            Assert.AreEqual(StorageTypes.extZlib, Texture2D.CalculateStorageType(StorageTypes.extLZO, MEGame.ME3, false));
            Assert.AreEqual(StorageTypes.extZlib, Texture2D.CalculateStorageType(StorageTypes.extUnc, MEGame.ME3, false));
            Assert.AreEqual(StorageTypes.extZlib, Texture2D.CalculateStorageType(StorageTypes.pccLZO, MEGame.ME3, false));
            Assert.AreEqual(StorageTypes.extZlib, Texture2D.CalculateStorageType(StorageTypes.pccUnc, MEGame.ME3, false));

            // ME2 - Following storage types should become LZO
            Assert.AreEqual(StorageTypes.extLZO, Texture2D.CalculateStorageType(StorageTypes.extZlib, MEGame.ME2, false));
            Assert.AreEqual(StorageTypes.extLZO, Texture2D.CalculateStorageType(StorageTypes.extUnc, MEGame.ME2, false));
            Assert.AreEqual(StorageTypes.extLZO, Texture2D.CalculateStorageType(StorageTypes.pccZlib, MEGame.ME2, false));
            Assert.AreEqual(StorageTypes.extLZO, Texture2D.CalculateStorageType(StorageTypes.pccUnc, MEGame.ME2, false));

            // LE - Following storage types should become Oodle
            Assert.AreEqual(StorageTypes.extOodle, Texture2D.CalculateStorageType(StorageTypes.extUnc, MEGame.LE3, false));
            Assert.AreEqual(StorageTypes.extOodle, Texture2D.CalculateStorageType(StorageTypes.pccUnc, MEGame.LE3, false));
        }

        [TestMethod]
        public void CalculateStorageType_HandlesIsPackageStored()
        {
            // extLZMA not included as it is only for console games
            var packageStored = new List<StorageTypes>()
                { StorageTypes.pccOodle, StorageTypes.pccUnc, StorageTypes.pccZlib, StorageTypes.pccLZO };
            
            var externalStored = new List<StorageTypes>()
                { StorageTypes.extOodle, StorageTypes.extUnc, StorageTypes.extZlib, StorageTypes.extLZO };
            
            // All external types should become package stored when isPackageStored is true
            foreach (var t in externalStored)
            {
                Assert.IsTrue(packageStored.Contains(Texture2D.CalculateStorageType(t, MEGame.LE3, true)));
            }
            
            // All package stored types should become external stored types when isPackageStored is false
            foreach (var t in packageStored)
            {
                Assert.IsTrue(externalStored.Contains(Texture2D.CalculateStorageType(t, MEGame.LE3, false)));
            }
        }
    }
}