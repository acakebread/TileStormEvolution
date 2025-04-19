using UnityEngine;

namespace GamePreviewNamespace
{
	public readonly struct GridCoord
	{
		public int X { get; }
		public int Z { get; }

		public GridCoord(int x, int z)
		{
			X = x;
			Z = z;
		}

		// Convert to map index (z * width + x)
		public int ToIndex(int width) => Z * width + X;

		// Convert to world position (x, 0, z)
		public Vector3 ToPosition() => new Vector3(X, 0f, Z);

		// Add direction offset
		public GridCoord Add(int dx, int dz) => new GridCoord(X + dx, Z + dz);

		// Equality for comparisons
		public override bool Equals(object obj) => obj is GridCoord other && X == other.X && Z == other.Z;
		public override int GetHashCode() => (X, Z).GetHashCode();
		public static bool operator ==(GridCoord a, GridCoord b) => a.X == b.X && a.Z == b.Z;
		public static bool operator !=(GridCoord a, GridCoord b) => !(a == b);

		// String representation for debugging
		public override string ToString() => $"({X}, {Z})";
	}
}