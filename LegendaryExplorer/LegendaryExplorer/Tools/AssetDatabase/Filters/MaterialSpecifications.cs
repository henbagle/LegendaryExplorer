﻿using System;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    /// <summary>
    /// Specification to filter materials based on a BoolProperty
    /// </summary>
    public class MaterialBoolSpec : MaterialSpecification
    {
        /// <summary>
        /// If true, spec instead filters materials that do not have this property, or have false values
        /// </summary>
        public bool Inverted { get; set; } = false;
        public string PropertyName { get; set; }

        public MaterialBoolSpec() { }
        public MaterialBoolSpec(BoolProperty boolFilter) : base()
        {
            FilterName = boolFilter.Name;
            PropertyName = boolFilter.Name;
        }

        public override bool MatchesSpecification(MaterialRecord mr)
        {
            var anyTrue = mr.MatSettings.Any(s => s.Name == PropertyName && s.Parm2 == "True");
            if (Inverted) return !anyTrue;
            else return anyTrue;
        }
    }

    /// <summary>
    /// Specification to filter materials based on any predicate
    /// </summary>
    public class MaterialPredicateSpec : MaterialSpecification
    {
        private readonly Predicate<MaterialRecord> _predicate;

        public MaterialPredicateSpec(string filterName, Predicate<MaterialRecord> predicate)
        {
            FilterName = filterName;
            _predicate = predicate;
        }

        public override bool MatchesSpecification(MaterialRecord item) => _predicate(item);
    }

    /// <summary>
    /// Specification to filter materials based on MatSettings parameters
    /// </summary>
    public class MaterialSettingSpec : MaterialSpecification
    {
        public string SettingName { get; set; }
        public bool Inverted { get; set; }
        private readonly string _parm1;
        private readonly string _parm2;
        private readonly Predicate<MatSetting> _customPredicate = null;

        public MaterialSettingSpec(string filterName, string settingName, string parm1 = "", string parm2 = "") : base()
        {
            FilterName = filterName;
            SettingName = settingName;
            _parm1 = parm1;
            _parm2 = parm2;
        }

        public MaterialSettingSpec(string filterName, string settingName, Predicate<MatSetting> predicate)
        {
            // Custom predicate option has an additional check against settingName.
            // We could remove this and make code a bit simpler, but we would need to add settingName checks to all predicates
            FilterName = filterName;
            SettingName = settingName;
            _customPredicate = predicate;
        }

        public override bool MatchesSpecification(MaterialRecord mr)
        {
            Func<MatSetting, bool> predicate = _customPredicate is not null ? PredicateMatches : ParametersMatch;
            var specMatches = mr.MatSettings.Any(predicate);

            if (Inverted) return !specMatches;
            else return specMatches;
        }

        private bool PredicateMatches(MatSetting s)
        {
            if (s.Name == SettingName)
            {
                return _customPredicate(s);
            }
            return false;
        }

        private bool ParametersMatch(MatSetting s)
        {
            if (s.Name == SettingName)
            {
                if (_parm1 != "" && s.Parm1 != _parm1)
                {
                    return false;
                }
                if (_parm2 != "" && s.Parm2 != _parm2)
                {
                    return false;
                }
                return true;
            }
            return false;
        }
    }
}