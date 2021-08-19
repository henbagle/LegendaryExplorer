using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase
{
    public interface IPlotDatabase
    {
        Dictionary<int, PlotBool> Bools { get; set; }
        Dictionary<int, PlotElement> Ints { get; set; }
        Dictionary<int, PlotElement> Floats { get; set; }
        Dictionary<int, PlotConditional> Conditionals { get; set; }
        Dictionary<int, PlotTransition> Transitions { get; set; }
        Dictionary<int, PlotElement> Organizational { get; set; }
        MEGame Game { get; }
        PlotElement Root { get; set; }

        /// <summary>
        /// Turns the plot database into a single dictionary, with the key being ElementID
        /// </summary>
        /// <returns></returns>
        SortedDictionary<int, PlotElement> GetMasterDictionary();

        PlotElement GetElementById(int elementId);
        int GetNextElementId();
    }

    public abstract class PlotDatabase : IPlotDatabase
    {
        public Dictionary<int, PlotBool> Bools { get; set; } = new Dictionary<int, PlotBool>();
        public Dictionary<int, PlotElement> Ints { get; set; } = new Dictionary<int, PlotElement>();
        public Dictionary<int, PlotElement> Floats { get; set; } = new Dictionary<int, PlotElement>();
        public Dictionary<int, PlotConditional> Conditionals { get; set; } = new Dictionary<int, PlotConditional>();
        public Dictionary<int, PlotTransition> Transitions { get; set; } = new Dictionary<int, PlotTransition>();
        public Dictionary<int, PlotElement> Organizational { get; set; } = new Dictionary<int, PlotElement>();

        public MEGame Game { get; }
        
        public PlotElement Root { get; set; }
        

        public PlotDatabase(MEGame refgame)
        {
            this.Game = refgame;
        }

        public PlotDatabase()
        {

        }

        /// <summary>
        /// Turns the plot database into a single dictionary, with the key being ElementID
        /// </summary>
        /// <returns></returns>
        public SortedDictionary<int, PlotElement> GetMasterDictionary()
        {
            try
            {
                var elements = Bools.Values.ToList()
                    .Concat(Ints.Values.ToList())
                    .Concat(Floats.Values.ToList())
                    .Concat(Conditionals.Values.ToList())
                    .Concat(Transitions.Values.ToList())
                    .Concat(Organizational.Values.ToList())
                    .ToDictionary(e => e.ElementId);

                return new SortedDictionary<int, PlotElement>(elements);
            }
            catch //fallback in case saved dictionary has duplicate element ids
            {
                return new SortedDictionary<int, PlotElement>();
            }
        }

        public PlotElement GetElementById(int elementId)
        {
            if (Organizational.ContainsKey(elementId))
            {
                return Organizational[elementId];
            }

            var boolkvp = Bools.FirstOrDefault(e => e.Value.ElementId == elementId);
            if (boolkvp.Value != null)
            {
                return boolkvp.Value;
            }

            var intkvp = Ints.FirstOrDefault(e => e.Value.ElementId == elementId);
            if (intkvp.Value != null)
            {
                return intkvp.Value;
            }

            var cndkvp = Conditionals.FirstOrDefault(e => e.Value.ElementId == elementId);
            if (cndkvp.Value != null)
            {
                return cndkvp.Value;
            }

            var fltkvp = Floats.FirstOrDefault(e => e.Value.ElementId == elementId);
            if (fltkvp.Value != null)
            {
                return fltkvp.Value;
            }

            var trnkvp = Transitions.FirstOrDefault(e => e.Value.ElementId == elementId);
            if (trnkvp.Value != null)
            {
                return trnkvp.Value;
            }

            return null;
        }

        public bool RemoveFromParent(PlotElement child)
        {
            return child.RemoveFromParent();
        }

        public int GetNextElementId()
        {
            var sortedDictionary = GetMasterDictionary();
            return sortedDictionary.Last().Key + 1;
        }
    }

    public class BasegamePlotDatabase : PlotDatabase
    {
        public BasegamePlotDatabase(MEGame game) : base(game) { }
    }

    public class ModPlotDatabase : PlotDatabase
    {
        public ModPlotDatabase(MEGame game) : base(game) { }

        public ModPlotDatabase()
        {
            
        }
        
        public void SaveDatabaseToFile(string folder)
        {
            if (!Directory.Exists(folder))
                return;
            var dbPath = Path.Combine(folder, $"PlotDBMods{Game}.json");

            var serializationObj = new PlotDatabaseJsonFile();
            serializationObj.Bools = Bools.Values.ToList();
            serializationObj.Ints = Ints.Values.ToList();
            serializationObj.Floats = Floats.Values.ToList();
            serializationObj.Conditionals = Conditionals.Values.ToList();
            serializationObj.Transitions = Transitions.Values.ToList();
            serializationObj.Organizational = Organizational.Values.ToList();

            var json = JsonConvert.SerializeObject(serializationObj);

            File.WriteAllText(dbPath, json);
        }
    }
}