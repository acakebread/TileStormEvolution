namespace MassiveHadronLtd
{
	public static class MathUtil
	{
		public static int Factorial(int n) { var v = n; while (--n > 1) v *= n; return v > 0 ? v : 1; }

		//public static int CalculateFactorialLinq(int number)
		//{
		//	if (number < 0)
		//		throw new ArgumentException("Factorial is not defined for negative numbers.", nameof(number));

		//	return number <= 1 ? 1 : Enumerable.Range(1, number).Aggregate((a, b) => a * b);
		//}
	}
}