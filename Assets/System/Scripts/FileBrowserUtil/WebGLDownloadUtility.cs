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
	}
}
