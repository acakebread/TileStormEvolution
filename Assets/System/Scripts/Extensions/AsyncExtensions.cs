using System.Threading;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class AsyncExtensions
	{
		// Wait for N frames (equivalent to your original WaitFrames coroutine)
		public static async Awaitable WaitFramesAsync(int count)
		{
			if (count <= 0) return;

			for (int i = 0; i < count; i++)
			{
				await Awaitable.NextFrameAsync();
			}
		}

		// Optional: version that respects Time.timeScale = 0
		// (NextFrameAsync always waits one rendered frame regardless of timescale)
		public static async Awaitable WaitRealFramesAsync(int count)
		{
			if (count <= 0) return;

			for (int i = 0; i < count; i++)
			{
				await Awaitable.NextFrameAsync();
			}
		}

		public static async Awaitable WaitFramesAsync(int count, CancellationToken ct = default)
		{
			if (count <= 0) return;

			for (int i = 0; i < count; i++)
			{
				ct.ThrowIfCancellationRequested();
				await Awaitable.NextFrameAsync(ct);   // ← pass ct here
			}
		}
	}
}