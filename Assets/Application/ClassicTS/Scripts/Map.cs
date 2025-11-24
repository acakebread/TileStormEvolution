using System;
using UnityEngine;
using Newtonsoft.Json;
using MassiveHadronLtd;
using System.Linq;

namespace ClassicTilestorm
{
	[Serializable]
	public class Map
	{
		public string name;
		public string character;
		public string music;
		public string button;
		public int width;
		public int height;

		public Waypoint[] waypoints;
		public string[] table;
		public int[] tiles;
		public int[] mixed;
		public Pickups Pickups;

		// Atomic fields - ignored during normal serialization
		[JsonIgnore] public Definition[] definitions;
		[JsonIgnore] public TextureSequence[] textures;
		[JsonIgnore] public string version = "1.0";
		[JsonIgnore] public string author = "Player";
		[JsonIgnore] public string exportedFrom = "ClassicTilestorm";

		public bool ShouldSerializePickups() => Pickups != null && Pickups.nPickupCount > 0;

		[JsonIgnore] public bool IsAtomic => definitions?.Length > 0 || textures?.Length > 0;

		/// <summary>
		/// Rebuilds the optimal frequency-sorted table and remaps tiles.
		/// Returns true if any changes were made (table or tiles changed).
		/// </summary>
		public bool Consolidate(string[] definitions)
		{
			if (definitions == null) throw new ArgumentNullException(nameof(definitions));

			bool changed = false;

			// Step 1: Build the new optimal frequency-sorted table
			var newFrequencyTable = definitions.ToFrequencySortedTable();

			// Fast comparison: first check reference equality (common case when nothing changed)
			// Then structural equality using SequenceEqual (very fast for arrays)
			if (table == null || !table.SequenceEqual(newFrequencyTable))
			{
				table = newFrequencyTable;
				changed = true;
			}

			// Step 2: Only remap tiles if the table actually changed
			// (This avoids unnecessary work and prevents false-positive tile changes)
			if (changed || tiles == null || tiles.Length != definitions.Length)
			{
				var oldTiles = tiles; // Keep for potential future diff logging if needed
				tiles = new int[definitions.Length];

				for (int i = 0; i < definitions.Length; i++)
				{
					string id = definitions[i];
					if (string.IsNullOrEmpty(id))
					{
						Debug.LogError($"Invalid ID at index {i}");
						tiles[i] = -1;
						continue;
					}

					int newIndex = Array.IndexOf(table, id);
					if (newIndex == -1)
					{
						Debug.LogError($"Definition '{id}' not found in frequency table! This should not happen.");
						tiles[i] = -1;
					}
					else
					{
						tiles[i] = newIndex;
					}
				}

				// If you want to detect if tiles actually changed (beyond table change), uncomment:
				// if (oldTiles != null && !oldTiles.SequenceEqual(tiles)) changed = true;
			}
			if (changed) Debug.Log($"{name} consolidated");
			return changed;
		}
	}

	[Serializable]
	public class Pickups { public int nPickupCount; }
}