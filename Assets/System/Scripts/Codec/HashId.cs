namespace MassiveHadronLtd
{
	/// <summary>
	/// Strongly-typed identifier for resource definitions and tiles.
	/// Used system-wide to reference definitions via their stable hash value.
	/// Prevents accidental confusion with other integer types (e.g. tile indices, counts, offsets).
	/// </summary>
	public readonly struct HashId
	{
		public readonly int Value;

		public HashId(int value) => Value = value;

		// Implicit conversions — makes migration easy and code readable
		public static implicit operator int(HashId id) => id.Value;
		public static implicit operator HashId(int value) => new HashId(value);

		// For debugging / logging
		public override string ToString() => Value.ToString();

		// Equality and hashing (so it works in dictionaries, HashSet, etc.)
		public override bool Equals(object obj)
			=> obj is HashId other && Value == other.Value;

		public bool Equals(HashId other) => Value == other.Value;

		public override int GetHashCode() => Value.GetHashCode();

		// Optional: explicit equality operators if you like
		public static bool operator ==(HashId left, HashId right) => left.Value == right.Value;
		public static bool operator !=(HashId left, HashId right) => left.Value != right.Value;
	}
}