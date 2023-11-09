using System.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.PlotDatabase.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.Tests.UnitTests.PlotDatabaseTests
{
    [TestClass]
    public class PlotDBDeserializationTests
    {
        [TestMethod]
        public void SerializedPlotDatabase_LoadsBiowarePlotDBWithoutErrors()
        {
            GlobalTest.Init();
            var le1File =
                new StreamReader(LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("PlotDatabases.zip", "le1.json")).ReadToEnd();
            var db1 = JsonConvert.DeserializeObject<SerializedPlotDatabase>(le1File);
            db1.BuildTree();

            var le2File =
                new StreamReader(LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("PlotDatabases.zip", "le2.json")).ReadToEnd();
            var db2 = JsonConvert.DeserializeObject<SerializedPlotDatabase>(le2File);
            db2.BuildTree();

            var le3File =
                new StreamReader(LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("PlotDatabases.zip", "le3.json")).ReadToEnd();
            var db3 = JsonConvert.DeserializeObject<SerializedPlotDatabase>(le3File);
            db3.BuildTree();
        }
    }
}
