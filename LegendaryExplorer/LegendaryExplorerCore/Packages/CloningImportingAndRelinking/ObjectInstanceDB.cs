﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{

    // THIS IS A BIG WIP
    // Mgamerz Sept 18 2021

    /// <summary>
    /// Database class for mapping objects from an export in one game to the same one in another game
    /// </summary>
    public class ObjectInstanceDB
    {

        // DB PUBLIC PROPERTIES
        /// <summary>
        /// The list of names
        /// </summary>
        public List<string> NameTable { get; set; } = new List<string>();

        public Dictionary<int, List<int>> ObjectRecords { get; set; } = new();

        // DB PRIVATE MEMBERS
        /// <summary>
        /// The name table map to help speed up compilation
        /// </summary>
        private CaseInsensitiveDictionary<int> nametableMap = new CaseInsensitiveDictionary<int>();

        public PackageCache HACK_CACHE = new PackageCache();

        public int GetNameTableIndex(string name)
        {
            if (nametableMap.TryGetValue(name, out var index))
            {
                return index;
            }

            if (nametableMap.Count != NameTable.Count)
            {
                for (int i = 0; i < NameTable.Count; i++)
                {
                    if (!nametableMap.TryGetValue(NameTable[i], out _))
                    {
                        Debug.WriteLine($"Not found: {NameTable[i]}");
                    }
                }
            }
            nametableMap[name] = nametableMap.Count;
            NameTable.Add(name);
            return nametableMap[name];
        }



        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static ObjectInstanceDB DeserializeDB(string dbText)
        {
            return JsonConvert.DeserializeObject<ObjectInstanceDB>(dbText);
        }

        public List<string> GetFilesContainingObject(string ifp)
        {
            int nameIdx = -1;
            nametableMap?.TryGetValue(ifp, out nameIdx);
            if (nametableMap == null || nametableMap.Count == 0)
                nameIdx = NameTable.FindIndex(x => x.CaseInsensitiveEquals(ifp));

            if (nameIdx >= 0)
            {
                if (ObjectRecords.TryGetValue(nameIdx, out var result))
                    return result.Select(x => NameTable[x]).ToList();
                return null; // NOT FOUND IN NOBJECT LIST
            }

            return null; // NOT IN NAME TABLE
        }

        /// <summary>
        /// Adds the full instanced path to the specified list of packageNameIndex
        /// </summary>
        /// <param name="ifp"></param>
        /// <param name="packageNameIndex"></param>
        public void AddRecord(string ifp, int packageNameIndex, bool insertAtFront = false)
        {
            var keyIdx = GetNameTableIndex(ifp);
            if (!ObjectRecords.TryGetValue(keyIdx, out var records))
            {
                records = new List<int>();
                ObjectRecords[keyIdx] = records;
            }

            if (insertAtFront)
                records.Insert(0, packageNameIndex);
            else
                records.Add(packageNameIndex);
        }

        /// <summary>
        /// Builds the lookup table to speed up lookups
        /// </summary>
        public void BuildLookupTable()
        {
            nametableMap.Clear();
            for (int i = 0; i < NameTable.Count; i++)
            {
                nametableMap[NameTable[i]] = i;
            }
        }
    }
}
