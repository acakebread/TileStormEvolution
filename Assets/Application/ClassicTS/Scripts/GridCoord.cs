using UnityEngine;

namespace ClassicTilestorm
{
	public readonly struct GridCoord
	{
		public int X { get; }
		public int Z { get; }

		public GridCoord(int x, int z) { X = x; Z = z; }
		public GridCoord(float x, float z) { X = Mathf.RoundToInt(x); Z = Mathf.RoundToInt(z); }
		public GridCoord(Vector3 v) { X = Mathf.RoundToInt(v.x); Z = Mathf.RoundToInt(v.z); }

		// Convert to world position (x, 0, z)
		public Vector3 ToPosition() => new(X, 0f, Z);

		// Add direction offset
		public GridCoord Add(int dx, int dz) => new(X + dx, Z + dz);

		// Equality for comparisons
		public override bool Equals(object obj) => obj is GridCoord other && X == other.X && Z == other.Z;
		public override int GetHashCode() => (X, Z).GetHashCode();
		public static bool operator ==(GridCoord a, GridCoord b) => a.X == b.X && a.Z == b.Z;
		public static bool operator !=(GridCoord a, GridCoord b) => !(a == b);
		public static GridCoord operator +(GridCoord a, GridCoord b) => new(a.X + b.X, a.Z + b.Z);

		// String representation for debugging
		public override string ToString() => $"({X}, {Z})";
	}
}