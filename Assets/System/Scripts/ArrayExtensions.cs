public static class ArrayExtensions
{
	public static void RollArray<T>(T[] array, int first, int size, int roll, int stride)
	{
		if (array == null || size <= 0 || stride == 0)
			return;

		// Determine the bounds of the strip
		int lastIndex = first + (size - 1) * stride;
		int minIndex = stride > 0 ? first : lastIndex;
		int maxIndex = stride > 0 ? lastIndex : first;

		// Bounds check: ensure the strip stays within array bounds
		if (minIndex < 0 || maxIndex >= array.Length)
			return;

		// Normalize roll to be within [0, size)
		int nRoll = roll % size;
		if (nRoll < 0)
			nRoll += size;

		if (nRoll == 0)
			return;

		int nSrc = 0;
		int nDst = nRoll % size;
		T nVal = array[first];

		for (int i = size; i > 0; i--)
		{
			int dstIndex = first + nDst * stride;
			T nTmp = array[dstIndex];
			array[dstIndex] = nVal;
			nVal = nTmp;

			if (nDst == nSrc)
			{
				nSrc++;
				nDst++;
				if (nDst < size)
					nVal = array[first + nDst * stride];
			}
			nDst = (nDst + nRoll) % size;
		}
	}
}