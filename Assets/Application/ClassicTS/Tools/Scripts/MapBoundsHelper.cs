using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MapBoundsHelper
{
	public static void ProcessMaps(List<ArrayDatabaseParser.Element> rootElements)
	{
		// Find the maps section
		ArrayDatabaseParser.Element mapsSection = rootElements.Find(e => e.Name == "maps");
		if (mapsSection == null || mapsSection.Children == null)
		{
			Debug.LogWarning("Maps section not found or empty.");
			return;
		}

		foreach (var map in mapsSection.Children)
		{
			// Log all children of map to diagnose waypoints issue
			Debug.Log($"Map {map.Name} children: {string.Join(", ", map.Children?.Select(c => c.Name) ?? new[] { "none" })}");

			// Find tiles, mixed, and waypoints
			ArrayDatabaseParser.Element tiles = map.Children?.Find(e => e.Name == "tiles");
			ArrayDatabaseParser.Element mixed = map.Children?.Find(e => e.Name == "mixed");
			// Use case-insensitive search for waypoints
			ArrayDatabaseParser.Element waypoints = map.Children?.Find(e => e.Name.ToLower() == "waypoints");

			// Debug: Log waypoints status
			Debug.Log($"Map {map.Name} waypoints: {(waypoints != null ? $"found (Name={waypoints.Name})" : "null")}");

			if (tiles == null || tiles.Children == null)
			{
				Debug.LogWarning($"Tiles not found in map {map.Name}. Skipping.");
				continue;
			}

			// Debug: Log tiles children
			Debug.Log($"Map {map.Name} tiles children: {string.Join(", ", tiles.Children.Select(c => $"{c.Name} ({c.Type})"))}");

			// Get nWidth, nHeight, and TileData (nTileIndex)
			ArrayDatabaseParser.Element nWidthElement = tiles.Children.Find(e => e.Name == "nWidth");
			ArrayDatabaseParser.Element nHeightElement = tiles.Children.Find(e => e.Name == "nHeight");
			ArrayDatabaseParser.Element nTileIndex = tiles.Children.Find(e => e.Name == "nTileIndex");

			// Debug: Log TileData status
			if (nTileIndex != null)
			{
				Debug.Log($"Map {map.Name} TileData children: {string.Join(", ", nTileIndex.Children?.Select(c => $"{c.Name} ({c.Type})") ?? new[] { "none" })}");
			}

			// Validate structure
			if (nWidthElement == null || nHeightElement == null || nTileIndex == null)
			{
				Debug.LogWarning($"Invalid tiles structure in map {map.Name}: nWidth={nWidthElement != null}, nHeight={nHeightElement != null}, TileData={nTileIndex != null}. Skipping.");
				continue;
			}

			// Get bytes element
			ArrayDatabaseParser.Element bytesElement = nTileIndex.Children?.Find(e => e.Name == "bytes");
			if (bytesElement == null)
			{
				Debug.LogWarning($"Bytes element not found in map {map.Name}. TileData or nTileIndex structure invalid. Skipping.");
				continue;
			}

			// Get decompressed bytes and dimensions
			int[] bytes = ArrayDatabaseParser.DecompressBytes(nTileIndex, tiles);
			int nWidth = (int)nWidthElement.NumberValue;
			int nHeight = (int)nHeightElement.NumberValue;

			if (bytes.Length != nWidth * nHeight)
			{
				Debug.LogWarning($"Bytes length {bytes.Length} does not match nWidth * nHeight {nWidth * nHeight} in map {map.Name}. Skipping.");
				continue;
			}

			// Prepare for mixed processing
			int[] mixedBytes = null;
			ArrayDatabaseParser.Element mixedNTileIndex = null;
			if (mixed != null && mixed.Children != null)
			{
				mixedNTileIndex = mixed.Children.Find(e => e.Name == "nTileIndex");
				if (mixedNTileIndex == null)
				{
					Debug.LogWarning($"nTileIndex not found in mixed for map {map.Name}. Skipping mixed.");
				}
				else
				{
					ArrayDatabaseParser.Element mixedBytesElement = mixedNTileIndex.Children?.Find(e => e.Name == "bytes");
					if (mixedBytesElement == null)
					{
						Debug.LogWarning($"Bytes element not found in map {map.Name} mixed. Skipping mixed.");
					}
					else
					{
						mixedBytes = ArrayDatabaseParser.DecompressBytes(mixedNTileIndex, mixed);
						if (mixedBytes.Length != nWidth * nHeight)
						{
							Debug.LogWarning($"Mixed bytes length {mixedBytes.Length} does not match nWidth * nHeight {nWidth * nHeight} in map {map.Name}. Skipping mixed.");
							mixedBytes = null;
						}
					}
				}
			}

			// Find bounding box
			int minX = nWidth, maxX = -1, minY = nHeight, maxY = -1;
			for (int y = 0; y < nHeight; y++)
			{
				for (int x = 0; x < nWidth; x++)
				{
					int index = y * nWidth + x;
					if (bytes[index] != 0)
					{
						minX = Mathf.Min(minX, x);
						maxX = Mathf.Max(maxX, x);
						minY = Mathf.Min(minY, y);
						maxY = Mathf.Max(maxY, y);
					}
				}
			}

			// Check if map has valid tiles
			if (minX > maxX || minY > maxY)
			{
				Debug.LogWarning($"No valid tiles found in map {map.Name}. Skipping.");
				continue;
			}

			// Calculate new dimensions
			int newWidth = maxX - minX + 1;
			int newHeight = maxY - minY + 1;
			int[] newBytes = new int[newWidth * newHeight];
			int[] newMixedBytes = mixedBytes != null ? new int[newWidth * newHeight] : null;

			// Remap tiles and mixed bytes
			for (int y = minY; y <= maxY; y++)
			{
				for (int x = minX; x <= maxX; x++)
				{
					int oldIndex = y * nWidth + x;
					int newX = x - minX;
					int newY = y - minY;
					int newIndex = newY * newWidth + newX;

					// Remap tiles bytes
					newBytes[newIndex] = bytes[oldIndex];

					// Remap mixed bytes as indices if available
					if (newMixedBytes != null)
					{
						int value = mixedBytes[oldIndex];
						int oldDeltaIndex = y * nWidth + x + value;
						int oldX = oldDeltaIndex % nWidth;
						int oldZ = oldDeltaIndex / nWidth;
						int deltaX = oldX - x;
						int deltaZ = oldZ - y;
						int newValue = deltaZ * newWidth + deltaX;

						if (newIndex + newValue >= 0 && newIndex + newValue < newWidth * newHeight)
						{
							newMixedBytes[newIndex] = newValue;
						}
						else
						{
							newMixedBytes[newIndex] = value; // Keep original if out-of-bounds
							Debug.LogWarning($"Mixed remap map {map.Name}: Out-of-bounds xCoord={deltaX}  zCoord={deltaZ} (newWidth={newWidth}) at oldIndex={oldIndex}, value={value}, keeping original");
						}
						Debug.Log($"Mixed remap map {map.Name}: oldIndex={oldIndex}, value={value}, x={deltaX}, z={deltaZ}, newValue={newValue}, newIndex={newIndex}");
					}
				}
			}

			// Update tiles nWidth and nHeight
			nWidthElement.NumberValue = newWidth;
			nHeightElement.NumberValue = newHeight;

			// Replace tiles bytes element
			nTileIndex.Children.RemoveAll(e => e.Name == "bytes");
			var boundedBytesElement = new ArrayDatabaseParser.Element
			{
				Type = "array",
				Name = "bytes",
				Children = newBytes.Select(v => new ArrayDatabaseParser.Element
				{
					Type = "int",
					Name = "",
					NumberValue = v
				}).ToList()
			};
			nTileIndex.Children.Add(boundedBytesElement);

			// Update mixed if available
			if (newMixedBytes != null && mixedNTileIndex != null)
			{
				// Update mixed nWidth and nHeight (same as tiles)
				var mixedNWidthElement = mixed.Children.Find(e => e.Name == "nWidth");
				var mixedNHeightElement = mixed.Children.Find(e => e.Name == "nHeight");
				if (mixedNWidthElement != null && mixedNHeightElement != null)
				{
					mixedNWidthElement.NumberValue = newWidth;
					mixedNHeightElement.NumberValue = newHeight;
				}

				// Replace mixed bytes element
				mixedNTileIndex.Children.RemoveAll(e => e.Name == "bytes");
				var mixedBoundedBytesElement = new ArrayDatabaseParser.Element
				{
					Type = "array",
					Name = "bytes",
					Children = newMixedBytes.Select(v => new ArrayDatabaseParser.Element
					{
						Type = "int",
						Name = "",
						NumberValue = v
					}).ToList()
				};
				mixedNTileIndex.Children.Add(mixedBoundedBytesElement);
			}

			// Update waypoints
			if (waypoints != null)
			{
				if (waypoints.Children == null || waypoints.Children.Count == 0)
				{
					Debug.LogWarning($"Waypoints found in map {map.Name} but has no children.");
				}
				else
				{
					foreach (var wp in waypoints.Children)
					{
						// Debug: Log waypoint details
						Debug.Log($"Processing waypoint {wp.Name} in map {map.Name}");

						// Update vSrc
						ArrayDatabaseParser.Element vSrc = wp.Children?.Find(e => e.Name == "vSrc");
						if (vSrc?.Children != null)
						{
							ArrayDatabaseParser.Element fX = vSrc.Children.Find(e => e.Name == "fX");
							ArrayDatabaseParser.Element fZ = vSrc.Children.Find(e => e.Name == "fZ");
							if (fX != null && fZ != null)
							{
								float origFX = fX.NumberValue;
								float origFZ = fZ.NumberValue;
								fX.NumberValue = origFX - minX;
								fZ.NumberValue = origFZ - minY;
								Debug.Log($"Waypoint {wp.Name} in map {map.Name}: vSrc fX={origFX} -> {fX.NumberValue}, fZ={origFZ} -> {fZ.NumberValue}");
							}
							else
							{
								Debug.LogWarning($"fX or fZ missing in vSrc for waypoint {wp.Name} in map {map.Name}.");
							}
						}
						else
						{
							Debug.LogWarning($"vSrc not found or invalid in waypoint {wp.Name} for map {map.Name}.");
						}

						// Update vDst
						ArrayDatabaseParser.Element vDst = wp.Children?.Find(e => e.Name == "vDst");
						if (vDst?.Children != null)
						{
							ArrayDatabaseParser.Element fX = vDst.Children.Find(e => e.Name == "fX");
							ArrayDatabaseParser.Element fZ = vDst.Children.Find(e => e.Name == "fZ");
							if (fX != null && fZ != null)
							{
								float origFX = fX.NumberValue;
								float origFZ = fZ.NumberValue;
								fX.NumberValue = origFX - minX;
								fZ.NumberValue = origFZ - minY;
								Debug.Log($"Waypoint {wp.Name} in map {map.Name}: vDst fX={origFX} -> {fX.NumberValue}, fZ={origFZ} -> {fZ.NumberValue}");
							}
							else
							{
								Debug.LogWarning($"fX or fZ missing in vDst for waypoint {wp.Name} in map {map.Name}.");
							}
						}
						else
						{
							Debug.LogWarning($"vDst not found or invalid in waypoint {wp.Name} for map {map.Name}.");
						}

						// Update nTile
						ArrayDatabaseParser.Element nTile = wp.Children?.Find(e => e.Name == "nTile");
						if (nTile != null)
						{
							int value = (int)nTile.NumberValue;
							int x = (value % nWidth) - minX;
							int z = (value / nWidth) - minY;
							int newValue = z * newWidth + x;
							if ((newValue / newWidth) < newHeight)
							{
								nTile.NumberValue = newValue;
								Debug.Log($"Waypoint {wp.Name} in map {map.Name}: nTile orig={value} (z={z}, x={x}) -> new={newValue}, bounds={newWidth}x{newHeight}");
							}
							else
							{
								Debug.LogWarning($"Waypoint {wp.Name} in map {map.Name}: nTile x={x} out-of-bounds (newWidth={newWidth}), keeping original nTile={value}");
							}
						}
						else
						{
							Debug.LogWarning($"nTile not found in waypoint {wp.Name} for map {map.Name}.");
						}
					}
				}
			}
			else
			{
				Debug.LogWarning($"Waypoints not found in map {map.Name}.");
			}

			Debug.Log($"Bounded map {map.Name}: Original ({nWidth}x{nHeight}) -> New ({newWidth}x{newHeight}), Offset ({minX},{minY})");
		}
	}
}