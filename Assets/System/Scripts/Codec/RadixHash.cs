//stable ish version

using System;
using System.Numerics;
using System.Text;
using System.Security.Cryptography;
using System.Linq;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Utility class for hashing inputs to a numeric range defined by a modulus or generating random values in that range.
	/// This is a companion to HTB50 (or other encoders) for generating fixed-range values suitable for encoding.
	/// Defaults to base-64 assumptions but can be overridden for any radix/power (e.g., 50 for HTB50).
	/// Note: Encoding is handled separately (e.g., via HTB50 for radix 50). For other radices like 32 or 64,
	/// use native encoders if available; otherwise, throw for unsupported radices in your implementation.
	/// </summary>
	public static class RadixHash
	{
		/// <summary>
		/// Hashes the input string to a BigInteger in the range [0, modulus-1].
		/// </summary>
		public static BigInteger HashToRange(string input, BigInteger modulus)
		{
			if (modulus <= 0)
				throw new ArgumentOutOfRangeException(nameof(modulus));

			if (string.IsNullOrEmpty(input))
				return BigInteger.Zero;

			using var sha = SHA256.Create();
			byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));

			// Treat hash bytes as unsigned big-endian integer
			// (reverse to make little-endian → big-endian conversion correct)
			Array.Reverse(hash);
			BigInteger value = new BigInteger(hash, isUnsigned: true, isBigEndian: true);

			// To avoid modulo bias, use rejection sampling (constant-time safe)
			BigInteger range = modulus;
			BigInteger maxValid = BigInteger.Pow(2, hash.Length * 8) / range * range;  // largest multiple ≤ 2^(256)

			while (value >= maxValid)
			{
				// Re-hash with extra input to get new value (simple but effective)
				hash = sha.ComputeHash(hash.Concat(Encoding.UTF8.GetBytes("retry")).ToArray());
				Array.Reverse(hash);
				value = new BigInteger(hash, isUnsigned: true, isBigEndian: true);
			}

			return value % range;
		}

		/// <summary>
		/// Overload: Hashes the input string to a BigInteger in the range [0, radix^power - 1].
		/// </summary>
		public static BigInteger HashToRange(string input, int radix = 64, int power = 6)
		{
			BigInteger modulus = BigInteger.Pow(radix, power);
			return HashToRange(input, modulus);
		}

		/// <summary>
		/// Hashes the input string to a long in the range [0, modulus-1].
		/// </summary>
		public static long HashToRange64(string input, long modulus)
		{
			if (modulus <= 0)
				throw new ArgumentOutOfRangeException(nameof(modulus), "Modulus must be positive");

			BigInteger biModulus = (BigInteger)modulus;
			BigInteger biResult = HashToRange(input, biModulus);
			return (long)biResult;
		}

		/// <summary>
		/// Overload: Hashes the input string to a long in the range [0, radix^power - 1].
		/// Throws if the range exceeds long.MaxValue.
		/// </summary>
		public static long HashToRange64(string input, int radix = 64, int power = 6)
		{
			BigInteger modulus = BigInteger.Pow(radix, power);
			if (modulus > long.MaxValue)
				throw new OverflowException("Modulus exceeds long range");

			return (long)HashToRange(input, modulus);
		}

		/// <summary>
		/// Hashes the input string to an int in the range [0, modulus-1].
		/// </summary>
		public static int HashToRange32(string input, int modulus)
		{
			if (modulus <= 0)
				throw new ArgumentOutOfRangeException(nameof(modulus), "Modulus must be positive");

			BigInteger biModulus = (BigInteger)modulus;
			BigInteger biResult = HashToRange(input, biModulus);
			return (int)biResult;
		}

		/// <summary>
		/// Overload: Hashes the input string to an int in the range [0, radix^power - 1].
		/// Throws if the range exceeds int.MaxValue.
		/// </summary>
		public static int HashToRange32(string input, int radix = 64, int power = 6)
		{
			BigInteger modulus = BigInteger.Pow(radix, power);
			if (modulus > int.MaxValue)
				throw new OverflowException("Modulus exceeds int range");

			return (int)HashToRange(input, modulus);
		}

		/// <summary>
		/// Generates a cryptographically secure random BigInteger in the range [0, modulus-1].
		/// </summary>
		public static BigInteger GenerateRandomInRange(BigInteger modulus)
		{
			if (modulus <= 0)
				throw new ArgumentOutOfRangeException(nameof(modulus), "Modulus must be positive");

			if (modulus == 1)
				return BigInteger.Zero;

			// Calculate required bytes (with extra to minimize bias)
			int byteCount = (int)Math.Ceiling(BigInteger.Log(modulus, 2) / 8.0) + 4;
			byte[] randomBytes = new byte[byteCount];

			using (var rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(randomBytes);
				BigInteger randValue = new BigInteger(randomBytes.Concat(new byte[] { 0 }).ToArray());
				randValue %= modulus;
				if (randValue < 0) randValue += modulus;
				return randValue;
			}
		}

		/// <summary>
		/// Overload: Generates a cryptographically secure random BigInteger in the range [0, radix^power - 1].
		/// </summary>
		public static BigInteger GenerateRandomInRange(int radix = 64, int power = 6)
		{
			BigInteger modulus = BigInteger.Pow(radix, power);
			return GenerateRandomInRange(modulus);
		}

		/// <summary>
		/// Generates a cryptographically secure random long in the range [0, modulus-1].
		/// </summary>
		public static long GenerateRandomInRange64(long modulus)
		{
			if (modulus <= 0)
				throw new ArgumentOutOfRangeException(nameof(modulus), "Modulus must be positive");

			BigInteger biModulus = (BigInteger)modulus;
			BigInteger biResult = GenerateRandomInRange(biModulus);
			return (long)biResult;
		}

		/// <summary>
		/// Overload: Generates a cryptographically secure random long in the range [0, radix^power - 1].
		/// Throws if the range exceeds long.MaxValue.
		/// </summary>
		public static long GenerateRandomInRange64(int radix = 64, int power = 6)
		{
			BigInteger modulus = BigInteger.Pow(radix, power);
			if (modulus > long.MaxValue)
				throw new OverflowException("Modulus exceeds long range");

			return (long)GenerateRandomInRange(modulus);
		}

		/// <summary>
		/// Generates a cryptographically secure random int in the range [0, modulus-1].
		/// </summary>
		public static int GenerateRandomInRange32(int modulus)
		{
			if (modulus <= 0)
				throw new ArgumentOutOfRangeException(nameof(modulus), "Modulus must be positive");

			BigInteger biModulus = (BigInteger)modulus;
			BigInteger biResult = GenerateRandomInRange(biModulus);
			return (int)biResult;
		}

		/// <summary>
		/// Overload: Generates a cryptographically secure random int in the range [0, radix^power - 1].
		/// Throws if the range exceeds int.MaxValue.
		/// </summary>
		public static int GenerateRandomInRange32(int radix = 64, int power = 6)
		{
			BigInteger modulus = BigInteger.Pow(radix, power);
			if (modulus > int.MaxValue)
				throw new OverflowException("Modulus exceeds int range");

			return (int)GenerateRandomInRange(modulus);
		}
	}
}

//version that appears to yield no discerable difference in results

//using System;
//using System.Numerics;
//using System.Text;
//using System.Security.Cryptography;

//namespace MassiveHadronLtd
//{
//	/// <summary>
//	/// Robust hashing and random generation utilities for arbitrary radix/power ranges.
//	/// Combines SHA256 for large ranges and XXHash64 for 32/64-bit ranges to minimize collisions.
//	/// </summary>
//	public static class RadixHash
//	{
//		#region SHA256 -> BigInteger

//		/// <summary>
//		/// Hash input string to a BigInteger in [0, modulus-1] using SHA256.
//		/// Deterministic, uniform, low collision.
//		/// </summary>
//		public static BigInteger HashToRange(string input, BigInteger modulus)
//		{
//			if (modulus <= 0) throw new ArgumentOutOfRangeException(nameof(modulus));
//			if (string.IsNullOrEmpty(input)) return BigInteger.Zero;

//			using var sha = SHA256.Create();
//			byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
//			Array.Reverse(hash); // big-endian
//			BigInteger value = new BigInteger(hash, isUnsigned: true, isBigEndian: true);

//			return value % modulus;
//		}

//		/// <summary>
//		/// Hash input string to a BigInteger in [0, radix^power - 1].
//		/// </summary>
//		public static BigInteger HashToRange(string input, int radix = 64, int power = 6)
//		{
//			BigInteger modulus = BigInteger.Pow(radix, power);
//			return HashToRange(input, modulus);
//		}

//		#endregion

//		#region 64-bit hashing

//		/// <summary>
//		/// Hash input to a long in [0, modulus-1].
//		/// Uses first 8 bytes of SHA256 for speed and uniformity.
//		/// </summary>
//		public static long HashToRange64(string input, long modulus)
//		{
//			if (modulus <= 0) throw new ArgumentOutOfRangeException(nameof(modulus));

//			using var sha = SHA256.Create();
//			byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
//			long value = BitConverter.ToInt64(hash, 0) & long.MaxValue;
//			return value % modulus;
//		}

//		/// <summary>
//		/// Hash input to a long in [0, radix^power -1]. Throws if overflow.
//		/// </summary>
//		public static long HashToRange64(string input, int radix = 64, int power = 6)
//		{
//			BigInteger modulus = BigInteger.Pow(radix, power);
//			if (modulus > long.MaxValue)
//				throw new OverflowException("Modulus exceeds long range");
//			return (long)HashToRange(input, modulus);
//		}

//		#endregion

//		#region 32-bit hashing

//		/// <summary>
//		/// Hash input to int in [0, modulus-1] using first 4 bytes of SHA256.
//		/// </summary>
//		public static int HashToRange32(string input, int modulus)
//		{
//			if (modulus <= 0) throw new ArgumentOutOfRangeException(nameof(modulus));

//			using var sha = SHA256.Create();
//			byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
//			int value = BitConverter.ToInt32(hash, 0) & int.MaxValue;
//			return value % modulus;
//		}

//		/// <summary>
//		/// Hash input to int in [0, radix^power -1]. Throws if overflow.
//		/// </summary>
//		public static int HashToRange32(string input, int radix = 64, int power = 6)
//		{
//			BigInteger modulus = BigInteger.Pow(radix, power);
//			if (modulus > int.MaxValue)
//				throw new OverflowException("Modulus exceeds int range");
//			return (int)HashToRange(input, modulus);
//		}

//		#endregion

//		#region Random generation

//		/// <summary>
//		/// Secure random BigInteger in [0, modulus-1].
//		/// </summary>
//		public static BigInteger GenerateRandomInRange(BigInteger modulus)
//		{
//			if (modulus <= 0) throw new ArgumentOutOfRangeException(nameof(modulus));
//			if (modulus == 1) return BigInteger.Zero;

//			int byteCount = (int)Math.Ceiling(BigInteger.Log(modulus, 2) / 8.0) + 1;
//			byte[] bytes = new byte[byteCount];
//			using var rng = RandomNumberGenerator.Create();

//			BigInteger result;
//			do
//			{
//				rng.GetBytes(bytes);
//				result = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
//			} while (result >= modulus);

//			return result;
//		}

//		public static BigInteger GenerateRandomInRange(int radix = 64, int power = 6)
//		{
//			BigInteger modulus = BigInteger.Pow(radix, power);
//			return GenerateRandomInRange(modulus);
//		}

//		public static long GenerateRandomInRange64(long modulus)
//		{
//			return (long)GenerateRandomInRange(modulus);
//		}

//		public static long GenerateRandomInRange64(int radix = 64, int power = 6)
//		{
//			BigInteger modulus = BigInteger.Pow(radix, power);
//			if (modulus > long.MaxValue)
//				throw new OverflowException("Modulus exceeds long range");
//			return (long)GenerateRandomInRange(modulus);
//		}

//		public static int GenerateRandomInRange32(int modulus)
//		{
//			return (int)GenerateRandomInRange(modulus);
//		}

//		public static int GenerateRandomInRange32(int radix = 64, int power = 6)
//		{
//			BigInteger modulus = BigInteger.Pow(radix, power);
//			if (modulus > int.MaxValue)
//				throw new OverflowException("Modulus exceeds int range");
//			return (int)GenerateRandomInRange(modulus);
//		}

//		#endregion
//	}
//}
