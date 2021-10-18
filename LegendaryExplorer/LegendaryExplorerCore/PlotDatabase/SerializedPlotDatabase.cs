﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase
{
    /// <summary>
    /// A class representing the JSON serialized plot database file
    /// </summary>
    public class SerializedPlotDatabase
    {
        // TODO: Change the JSON serialization to dictionary
        [JsonProperty("bools")] public List<PlotBool> Bools = new();

        [JsonProperty("ints")] public List<PlotElement> Ints = new();

        [JsonProperty("floats")] public List<PlotElement> Floats = new();

        [JsonProperty("conditionals")] public List<PlotConditional> Conditionals = new();

        [JsonProperty("transitions")] public List<PlotTransition> Transitions = new();

        [JsonProperty("organizational")] public List<PlotElement> Organizational = new();

        public SerializedPlotDatabase()
        {
        }

        public SerializedPlotDatabase(PlotDatabase plotDatabase)
        {
            Bools = plotDatabase.Bools.Values.ToList();
            Ints = plotDatabase.Ints.Values.ToList();
            Floats = plotDatabase.Floats.Values.ToList();
            Conditionals = plotDatabase.Conditionals.Values.ToList();
            Transitions = plotDatabase.Transitions.Values.ToList();
            Organizational = plotDatabase.Organizational.Values.ToList();
        }

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
                if (parentId > 0)
                {
                    plot.AssignParent(table[parentId]);
                }
            }
        }
    }
}