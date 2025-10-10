using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class SpatialBucketSystem
	{
		private readonly List<Vector3> points = new();
		private readonly Dictionary<Vector2Int, Vector3> buckets = new();
		private readonly float bucketSize;
		private readonly int MaxPoints;

		public IReadOnlyList<Vector3> Points => points;

		public SpatialBucketSystem(float minDistance, int maxPoints)
		{
			bucketSize = minDistance;
			MaxPoints = maxPoints;
		}

		public bool TryAddPoint(Vector3 position)
		{
			if (!CanAddPoint(position)) return false;
			points.Add(position);
			AddPoint(position);
			EnforceMaxSize();
			return true;
		}

		public void SetPoints(List<Vector3> newPoints)
		{
			Clear();
			if (newPoints == null || newPoints.Count == 0) return;
			foreach (var point in newPoints)
			{
				if (CanAddPoint(point))
				{
					points.Add(point);
					AddPoint(point);
				}
			}
			EnforceMaxSize();
		}

		public void Clear()
		{
			points.Clear();
			buckets.Clear();
		}

		private bool CanAddPoint(Vector3 position)
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

		private void AddPoint(Vector3 position)
		{
			Vector2Int cell = GetBucketCell(position);
			if (!buckets.ContainsKey(cell)) // Only add if bucket is empty
				buckets[cell] = position;
		}

		private void RemovePoint(Vector3 position)
		{
			Vector2Int cell = GetBucketCell(position);
			buckets.Remove(cell); // Remove regardless of exact position (since bucket holds one point)
		}

		private void EnforceMaxSize()
		{
			while (points.Count > MaxPoints)
			{
				var oldest = points[0];
				RemovePoint(oldest);
				points.RemoveAt(0);
			}
		}

		private Vector2Int GetBucketCell(Vector3 position)
		{
			// Discretize to 2D grid (ignoring y-axis for simplicity)
			return new Vector2Int(
				Mathf.FloorToInt(position.x / bucketSize),
				Mathf.FloorToInt(position.z / bucketSize)
			);
		}
	}
}