using UnityEngine;

public class Simple75Test : MonoBehaviour
{
	private void Start()
	{
		var rng = new System.Random(123456);
		float worstError = 0f;

		for (int i = 0; i < 100000; i++)
		{
			float x = (float)(rng.NextDouble() * 8192 - 4096); // ±4096 = full 12.6 range
			float y = (float)(rng.NextDouble() * 8192 - 4096);
			float z = (float)(rng.NextDouble() * 8192 - 4096);
			float w = (float)(rng.NextDouble() * 8192 - 4096);

			FixedPointFloatPacker75_WORKING.Pack(x, y, z, w, out float a, out float b, out float c);
			FixedPointFloatPacker75_WORKING.Unpack(a, b, c, out float rx, out float ry, out float rz, out float rw);

			float err = Mathf.Max(
				Mathf.Abs(rx - x),
				Mathf.Abs(ry - y),
				Mathf.Abs(rz - z),
				Mathf.Abs(rw - w)
			);

			worstError = Mathf.Max(worstError, err);
		}

		Debug.Log($"<color=lime>SIMPLE 75-BIT PACKER (FP12.6) – WORST ERROR: {worstError:G9}</color>");
		Debug.Log("<color=lime>EXPECTED: ≤ 0.0078125 (0.5 / 64)</color>");
	}
}