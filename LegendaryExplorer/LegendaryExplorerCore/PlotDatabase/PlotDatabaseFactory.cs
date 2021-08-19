using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase
{
    internal static class PlotDatabaseFactory
    {
        private static readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
            { NullValueHandling = NullValueHandling.Ignore };

        public static BasegamePlotDatabase CreateBasegamePlotDatabase(MEGame game)
        {
            var jsonDb = JsonConvert.DeserializeObject<PlotDatabaseJsonFile>(
                LegendaryExplorerCoreUtilities.LoadStringFromCompressedResource("PlotDatabases.zip",
                    LegendaryExplorerCoreLib.CustomPlotFileName(game)), _jsonSerializerSettings);

            if (jsonDb is null) throw new Exception("Unable to deserialize basegame Plot Database");
            
            jsonDb.BuildTree();
            
            var pdb = new BasegamePlotDatabase(game);
            SetTables(pdb, jsonDb);
            SetRoot(pdb);
            return pdb;
        }

        public static ModPlotDatabase CreateModdedPlotDatabaseFromJson(MEGame game, string dbPath)
        {
            if (dbPath is null || !File.Exists(dbPath)) throw new ArgumentException("Invalid plot database path");
            StreamReader sr = new StreamReader(dbPath);

            var jsonDb = JsonConvert.DeserializeObject<PlotDatabaseJsonFile>(sr.ReadToEnd(), _jsonSerializerSettings);
            if (jsonDb is null) throw new Exception("Unable to deserialize modded Plot Database");
            
            jsonDb.BuildTree();
            
            var pdb = new ModPlotDatabase(game);
            SetTables(pdb, jsonDb);
            SetRoot(pdb);
            return pdb;
        }

        public static ModPlotDatabase CreateBlankModdedPlotDatabase(MEGame game)
        {
            var pdb = new ModPlotDatabase(game.ToLEVersion());
            string modsRootLabel = $"{game.ToLEVersion()}/{game.ToOTVersion()} Mods";
            var modsRoot = new PlotElement(0, 100000, modsRootLabel, PlotElementType.Region, 0,
                new List<PlotElement>());
            pdb.Organizational.Add(100000, modsRoot);
            pdb.Root = modsRoot;

            return pdb;
        }
        
        /// <summary>
        /// Loads the plot tables from the PDB Json into the IPlotDatabase
        /// </summary>
        /// <param name="pdb">Database to load plot elements into</param>
        /// <param name="jsonDb">Deserialized DB file containing the plot elements</param>
        private static void SetTables(IPlotDatabase pdb, PlotDatabaseJsonFile jsonDb)
        {
            pdb.Bools = jsonDb.Bools.ToDictionary((b) => b.PlotId);
            pdb.Ints = jsonDb.Ints.ToDictionary((b) => b.PlotId);
            pdb.Floats = jsonDb.Floats.ToDictionary((b) => b.PlotId);
            pdb.Conditionals = jsonDb.Conditionals.ToDictionary((b) => b.PlotId);
            pdb.Transitions = jsonDb.Transitions.ToDictionary((b) => b.PlotId);
            pdb.Organizational = jsonDb.Organizational.ToDictionary((b) => b.ElementId);
        }
        
        /// <summary>
        /// Finds and sets the root node property of an IPlotDatabase
        /// </summary>
        /// <param name="pdb">Plot Database to set root node</param>
        private static void SetRoot(IPlotDatabase pdb)
        {
            PlotElement element = pdb.Organizational.First().Value;
            while (element.Parent != null) element = element.Parent;
            pdb.Root = element;
        }
    }
}