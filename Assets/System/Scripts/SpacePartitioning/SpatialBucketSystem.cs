using UnityEngine;
using System.Collections.Generic;

public static class SpatialBucketSystem
{
	private static readonly Dictionary<Vector2Int, Vector3> buckets = new();
	private static float bucketSize; // Set based on min distance

	public static void Initialize(float minDistance)
	{
		bucketSize = minDistance; // Bucket size ensures points in same bucket are too close
		buckets.Clear();
	}

	public static bool CanAddPoint(Vector3 position)
	{
		Vector2Int cell = GetBucketCell(position);

		// If the bucket is occupied, the point is too close to an existing point
		if (buckets.ContainsKey(cell))
			return false;

		// Check neighboring buckets
		for (int x = cell.x - 1; x <= cell.x + 1; x++)
		{
			for (int y = cell.y - 1; y <= cell.y + 1; y++)
			{
				Vector2Int neighbor = new Vector2Int(x, y);
				if (buckets.ContainsKey(neighbor) && Vector3.Distance(position, buckets[neighbor]) < bucketSize)
					return false;
			}
		}
		return true;
	}

	public static void AddPoint(Vector3 position)
	{
		Vector2Int cell = GetBucketCell(position);
		if (!buckets.ContainsKey(cell)) // Only add if bucket is empty
			buckets[cell] = position;
	}

	public static bool TryAddPoint(Vector3 position)
	{
		if (!CanAddPoint(position)) return false;
		AddPoint(position);
		return true;
	}

	public static void RemovePoint(Vector3 position)
	{
		Vector2Int cell = GetBucketCell(position);
		buckets.Remove(cell); // Remove regardless of exact position (since bucket holds one point)
	}

	public static void Clear()
	{
		buckets.Clear();
	}

	private static Vector2Int GetBucketCell(Vector3 position)
	{
		// Discretize to 2D grid (ignoring y-axis for simplicity)
		return new Vector2Int(
			Mathf.FloorToInt(position.x / bucketSize),
			Mathf.FloorToInt(position.z / bucketSize)
		);
	}
}