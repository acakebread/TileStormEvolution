namespace MassiveHadronLtd
{
	public static class Probability
	{
		public static int Choose(int num, int count) => MathUtil.Factorial(count) / MathUtil.Factorial(num) / MathUtil.Factorial(count - num);

		//return number of unique combinations of supplied array
		public static int DistinctPermutations(int[] v)
		{
			var n = MathUtil.Factorial(v.Length);
			for (var i = 0; i < v.Length;)
			{
				var c = 1;
				for (var j = i + 1; j < v.Length; ++j)
				{
					if (v[j] != v[i]) continue;
					v[j] = v[i + c];
					n /= ++c;
				}
				i += c;
			}
			return n;
		}
	}
}