using System;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Stable hash identifier for definitions, tiles, and other resources.
	/// Used system-wide as the primary key to reference and resolve resource definitions.
	/// Prevents confusion with positional ints (tile indices, counts, offsets, etc.).
	/// </summary>
	public readonly struct HashId : IComparable<HashId>, IComparable
	{
		public readonly int Value;

		public HashId(int value) => Value = value;

		// Implicit conversions (keep these for easy migration)
		public static implicit operator int(HashId id) => id.Value;
		public static implicit operator HashId(int value) => new HashId(value);

		// For debugging/logging
		public override string ToString() => Value.ToString();

		// Equality (required for GroupBy, Contains, etc.)
		public override bool Equals(object obj)
			=> obj is HashId other && Value == other.Value;

		public bool Equals(HashId other) => Value == other.Value;

		public override int GetHashCode() => Value.GetHashCode();

		public static bool operator ==(HashId left, HashId right) => left.Value == right.Value;
		public static bool operator !=(HashId left, HashId right) => left.Value != right.Value;

		// Comparison — sort by the underlying int value
		public int CompareTo(HashId other) => Value.CompareTo(other.Value);

		int IComparable.CompareTo(object obj)
		{
			if (obj == null) return 1;
			if (obj is not HashId other)
				throw new ArgumentException($"Object is not a {nameof(HashId)}");
			return CompareTo(other);
		}
	}
}