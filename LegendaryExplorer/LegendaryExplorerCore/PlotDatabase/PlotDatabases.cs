﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.PlotDatabase
{
    public static class PlotDatabases
    {
        public static PlotDatabase Le1PlotDatabase;
        public static PlotDatabase Le2PlotDatabase;
        public static PlotDatabase Le3PlotDatabase;

        public static PlotDatabase GetDatabaseForGame(MEGame game)
        {
            var db = game switch
            {
                MEGame.ME1 => Le1PlotDatabase,
                MEGame.ME2 => Le2PlotDatabase,
                MEGame.ME3 => Le3PlotDatabase,
                MEGame.LE1 => Le1PlotDatabase,
                MEGame.LE2 => Le2PlotDatabase,
                MEGame.LE3 => Le3PlotDatabase,
                _ => throw new ArgumentOutOfRangeException($"Game {game} has no plot database")
            };
            return db;
        }

        public static SortedDictionary<int, PlotElement> GetMasterDictionaryForGame(MEGame game)
        {
            EnsureDatabaseLoaded(game);
            var db = GetDatabaseForGame(game);
            return db.GetMasterDictionary();
        }

        public static PlotBool FindPlotBoolByID(int id, MEGame game)
        {
            EnsureDatabaseLoaded(game);
            var db = GetDatabaseForGame(game);
            return db.Bools[id];
        }

        public static PlotElement FindPlotIntByID(int id, MEGame game)
        {
            EnsureDatabaseLoaded(game);
            var db = GetDatabaseForGame(game);
            return db.Ints[id];
        }

        public static PlotElement FindPlotFloatByID(int id, MEGame game)
        {
            EnsureDatabaseLoaded(game);
            var db = GetDatabaseForGame(game);
            return db.Floats[id];
        }

        public static PlotConditional FindPlotConditionalByID(int id, MEGame game)
        {
            EnsureDatabaseLoaded(game);
            var db = GetDatabaseForGame(game);
            return db.Conditionals[id];
        }

        public static PlotTransition FindPlotTransitionByID(int id, MEGame game)
        {
            EnsureDatabaseLoaded(game);
            var db = GetDatabaseForGame(game);
            return db.Transitions[id];
        }

        private static void EnsureDatabaseLoaded(MEGame game)
        {
            if (GetDatabaseForGame(game) == null)
            {
                ReloadDatabase(game, isbioware: true);
            }
        }

        public static void ReloadDatabase(MEGame game, bool isbioware)
        {
            var db = new PlotDatabase(game, isbioware);
            db.LoadPlotsFromJSON(game, isbioware);
            switch (game)
            {
                case MEGame.ME1:
                case MEGame.LE1:
                    Le1PlotDatabase = db;
                    break;
                case MEGame.ME2:
                case MEGame.LE2:
                    Le2PlotDatabase = db;
                    break;
                case MEGame.ME3:
                case MEGame.LE3:
                    Le3PlotDatabase = db;
                    break;
            }
        }
    }
}
