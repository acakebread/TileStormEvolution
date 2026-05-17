using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MassiveHadronLtd.FileBrowserUtil
{
	public static class WebGLDownloadUtility
	{
#if UNITY_WEBGL && !UNITY_EDITOR
		[DllImport("__Internal")]
		private static extern void MassiveHadron_DownloadText(string filename, string text, string mimeType);
		[DllImport("__Internal")]
		private static extern void MassiveHadron_DownloadBytes(string filename, byte[] data, int length, string mimeType);
#endif

		public static void DownloadText(string filename, string text, string mimeType = "text/plain;charset=utf-8")
		{
			if (string.IsNullOrWhiteSpace(filename))
				throw new ArgumentNullException(nameof(filename));

#if UNITY_WEBGL && !UNITY_EDITOR
			MassiveHadron_DownloadText(filename, text ?? string.Empty, mimeType ?? "text/plain;charset=utf-8");
#else
			Debug.LogWarning("WebGLDownloadUtility.DownloadText was called outside WebGL.");
#endif
		}

		public static void DownloadBytes(string filename, byte[] data, string mimeType = "application/octet-stream")
		{
			if (string.IsNullOrWhiteSpace(filename))
				throw new ArgumentNullException(nameof(filename));

#if UNITY_WEBGL && !UNITY_EDITOR
			MassiveHadron_DownloadBytes(filename, data ?? Array.Empty<byte>(), data?.Length ?? 0, mimeType ?? "application/octet-stream");
#else
			Debug.LogWarning("WebGLDownloadUtility.DownloadBytes was called outside WebGL.");
#endif
		}
	}
}
