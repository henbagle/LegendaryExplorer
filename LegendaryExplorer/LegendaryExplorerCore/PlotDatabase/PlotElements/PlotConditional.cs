using System.Collections.Generic;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.PlotElements
{
    /// <summary>
    /// Represents a plot conditional element in a Plot Database
    /// </summary>
    public class PlotConditional : PlotElement
    {
        /// <summary>The code of this conditional</summary>
        [JsonProperty("code")]
        public string Code { get; set; }

        /// <summary>
        /// Initializes a new <see cref="PlotConditional"/>
        /// </summary>
        PlotConditional()
        { }

        /// <summary>
        /// Initializes a new PlotConditional
        /// </summary>
        /// <param name="plotid">PlotID of new element</param>
        /// <param name="elementId">ElementID of new element</param>
        /// <param name="label">Label of new element</param>
        /// <param name="type">Type of new element</param>
        /// <param name="parent">Parent PlotElement of new element</param>
        /// <param name="children">Children of new element</param>
        public PlotConditional(int plotid, int elementId, string label, PlotElementType type, PlotElement parent,
            List<PlotElement> children = null)
            : base(plotid, elementId, label, type, parent, children)
        {
            Type = PlotElementType.Conditional;
        }
    }
}