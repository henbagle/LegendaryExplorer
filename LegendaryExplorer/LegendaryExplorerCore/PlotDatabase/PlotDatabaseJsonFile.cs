using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SixLabors.ImageSharp.PixelFormats;

namespace LegendaryExplorerCore.PlotDatabase
{
    /// <summary>
    /// A class representing the JSON serialized plot database file
    /// </summary>
    public class PlotDatabaseJsonFile
    {
        // TODO: Change the JSON serialization to dictionary
        [JsonProperty("bools")]
        public List<PlotBool> Bools;

        [JsonProperty("conditionals")]
        public List<PlotConditional> Conditionals;

        [JsonProperty("floats")]
        public List<PlotElement> Floats;

        [JsonProperty("ints")]
        public List<PlotElement> Ints;

        [JsonProperty("organizational")] 
        public List<PlotElement> Organizational;

        [JsonProperty("transitions")] 
        public List<PlotTransition> Transitions;

        /// <summary>
        /// Builds the Parent and Child List relationships between all plot elements.
        /// Needs to be run when database gets initialized.
        /// </summary>
        public void BuildTree()
        {
            Dictionary<int, PlotElement> table =
                Bools.Concat<PlotElement>(Ints)
                    .Concat(Floats).Concat(Conditionals)
                    .Concat(Transitions).Concat(Organizational)
                    .ToDictionary((e) => e.ElementId);

            foreach (var element in table)
            {
                var plot = element.Value;
                var parentId = plot.ParentElementId;
                if (parentId != 0)
                {
                    var parent = table[parentId];
                    plot.Parent = parent;
                    parent.Children.Add(plot);
                }
            }
        }
    }
}
