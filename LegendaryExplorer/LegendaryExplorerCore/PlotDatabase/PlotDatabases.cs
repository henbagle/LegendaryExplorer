using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.PlotDatabase
{
    public static class PlotDatabases
    {
        private static readonly Lazy<BasegamePlotDatabase> lazyLe1Basegame = new (() => PlotDatabaseFactory.CreateBasegamePlotDatabase(MEGame.LE1));
        public static BasegamePlotDatabase Le1PlotDatabase => lazyLe1Basegame.Value;
        
        private static readonly Lazy<BasegamePlotDatabase> lazyLe2Basegame = new (() => PlotDatabaseFactory.CreateBasegamePlotDatabase(MEGame.LE2));
        public static BasegamePlotDatabase Le2PlotDatabase => lazyLe2Basegame.Value;
        
        private static readonly Lazy<BasegamePlotDatabase> lazyLe3Basegame = new (() => PlotDatabaseFactory.CreateBasegamePlotDatabase(MEGame.LE3));
        public static BasegamePlotDatabase Le3PlotDatabase => lazyLe3Basegame.Value;
        
        private static readonly Lazy<ModPlotDatabase> lazyLe1Modded = new (() => LoadModdedPlotDatabase(MEGame.LE1));
        public static ModPlotDatabase Le1ModDatabase => lazyLe1Modded.Value;
        
        private static readonly Lazy<ModPlotDatabase> lazyLe2Modded = new (() => LoadModdedPlotDatabase(MEGame.LE2));
        public static ModPlotDatabase Le2ModDatabase => lazyLe2Modded.Value;
        
        private static readonly Lazy<ModPlotDatabase> lazyLe3Modded = new (() => LoadModdedPlotDatabase(MEGame.LE3));
        public static ModPlotDatabase Le3ModDatabase => lazyLe3Modded.Value;
        
        private static ModPlotDatabase LoadModdedPlotDatabase(MEGame game)
        {
            if (!game.IsLEGame())
            {
                throw new ArgumentException($"Cannot load modded plot database for {game}");
            }

            string modDbJsonPath = null;
            if (modDbJsonPath != null)
            {
                modDbJsonPath = Path.Combine(modDbJsonPath, $"PlotDBMods{game}.json");
            }

            if (modDbJsonPath != null && File.Exists(modDbJsonPath))
            {
                return PlotDatabaseFactory.CreateModdedPlotDatabaseFromJson(game, modDbJsonPath);
            }
            else
            {
                return PlotDatabaseFactory.CreateBlankModdedPlotDatabase(game);
            }
        }

        public static IPlotDatabase GetDatabaseForGame(MEGame game, bool isbioware)
        {
            IPlotDatabase db = game switch
            {
                MEGame.ME1 => Le1PlotDatabase,
                MEGame.ME2 => Le2PlotDatabase,
                MEGame.ME3 => Le3PlotDatabase,
                MEGame.LE1 => isbioware ? Le1PlotDatabase : Le1ModDatabase,
                MEGame.LE2 => isbioware ? Le2PlotDatabase : Le2ModDatabase,
                MEGame.LE3 => isbioware ? Le3PlotDatabase : Le3ModDatabase,
                _ => throw new ArgumentOutOfRangeException($"Game {game} has no plot database")
            };
            return db;
        }

        public static SortedDictionary<int, PlotElement> GetMasterDictionaryForGame(MEGame game, bool isbioware = true)
        {
            var db = GetDatabaseForGame(game, isbioware);
            return db.GetMasterDictionary();
        }

        public static PlotBool FindPlotBoolByID(int id, MEGame game)
        {
            var db = GetDatabaseForGame(game, true);
            if (db.Bools.ContainsKey(id))
            {
                return db.Bools[id];
            }

            if (game.IsLEGame())
            {
                var mdb = GetDatabaseForGame(game, false);
                if (mdb.Bools.ContainsKey(id))
                {
                    return mdb.Bools[id];
                }
            }
            return null;
        }

        public static PlotElement FindPlotIntByID(int id, MEGame game)
        {
            var db = GetDatabaseForGame(game, true);
            if (db.Ints.ContainsKey(id))
            {
                return db.Ints[id];
            }

            if (game.IsLEGame())
            {
                var mdb = GetDatabaseForGame(game, false);
                if (mdb.Ints.ContainsKey(id))
                {
                    return mdb.Ints[id];
                }
            }
            return null;
        }

        public static PlotElement FindPlotFloatByID(int id, MEGame game)
        {
            var db = GetDatabaseForGame(game, true);
            if (db.Floats.ContainsKey(id))
            {
                return db.Floats[id];
            }

            if (game.IsLEGame())
            {
                var mdb = GetDatabaseForGame(game, false);
                if (mdb.Floats.ContainsKey(id))
                {
                    return mdb.Floats[id];
                }
            }
            return null;
        }

        public static PlotConditional FindPlotConditionalByID(int id, MEGame game)
        {
            var db = GetDatabaseForGame(game, true);
            if (db.Conditionals.ContainsKey(id))
            {
                return db.Conditionals[id];
            }

            if (game.IsLEGame())
            {
                var mdb = GetDatabaseForGame(game, false);
                if (mdb.Conditionals.ContainsKey(id))
                {
                    return mdb.Conditionals[id];
                }
            }
            return null;
        }

        public static PlotTransition FindPlotTransitionByID(int id, MEGame game)
        {
            var db = GetDatabaseForGame(game, true);
            if (db.Transitions.ContainsKey(id))
            {
                return db.Transitions[id];
            }

            if (game.IsLEGame())
            {
                var mdb = GetDatabaseForGame(game, false);
                if (mdb.Transitions.ContainsKey(id))
                {
                    return mdb.Transitions[id];
                }
            }
            return null;
        }

        public static PlotElement FindPlotElementFromID(int id, PlotElementType type, MEGame game)
        {
            switch (type)
            {
                case PlotElementType.Flag:
                case PlotElementType.State:
                case PlotElementType.SubState:
                    return FindPlotBoolByID(id, game);
                case PlotElementType.Integer:
                    return FindPlotIntByID(id, game);
                case PlotElementType.Float:
                    return FindPlotFloatByID(id, game);
                case PlotElementType.Conditional:
                    return FindPlotConditionalByID(id, game);
                case PlotElementType.Transition:
                case PlotElementType.Consequence:
                    return FindPlotTransitionByID(id, game);
                default:
                    return null;
            }
        }

        public static string FindPlotPathFromID(int id, PlotElementType type, MEGame game)
        {
            return FindPlotElementFromID(id, type, game)?.Path ?? "";
        }
    }
}
