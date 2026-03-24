using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Light))]
	public class DirectionalLightUtility : MonoBehaviour
	{
		[Header("Histogram Settings")]
		[Range(0.005f, 0.5f)]
		[SerializeField] private float topPercent = 0.15f;

		[Range(1f, 20f)]
		[SerializeField] private float intensityMultiplier = 1f;

		[Range(0f, 3f)]
		[SerializeField] private float minIntensity = 0.2f;

		// Debug & preview
		private Texture2D cubemapPreviewTexture; // flattened cubemap for OnGUI
		private List<float> lumHistogram = new List<float>();
		private Color debugBrightColor = Color.gray;
		private float debugMaxLum = 0f;
		private int debugBrightPixelCount = 0;
		private bool hasRun = false;

		private Light directionalLight;
		private Cubemap lastCubemap;

		private void Awake()
		{
			directionalLight = GetComponent<Light>();
			SkyboxUtility.OnSkyboxChanged += value => UpdateFromSkybox(value);
		}

		//private void Update()
		//{
		//	Cubemap current = SkyboxUtility.GetTintedSkyboxCubemap();
		//	if (current != lastCubemap)
		//	{
		//		lastCubemap = current;
		//		UpdateFromSkybox();
		//	}
		//}

		public void UpdateFromSkybox(Material skybox = null)
		{
			Cubemap cubemap = SkyboxUtility.GetTintedSkyboxCubemap(skybox);
			if (cubemap == null)
			{
				directionalLight.color = Color.white;
				directionalLight.intensity = 1f;
				return;
			}

			// Color with your dynamic threshold
			Color brightColor = CubemapUtility.ComputeBrightRegionColor(cubemap, 1f - topPercent);

			// Direction using the cubemap directly
			Vector3 lightDir = CubemapUtility.FindLightDirection(cubemap);

			directionalLight.color = brightColor;

			float lum = brightColor.r * 0.2126f + brightColor.g * 0.7152f + brightColor.b * 0.0722f;
			directionalLight.intensity = Mathf.Max(minIntensity, lum * intensityMultiplier);

			if (lightDir.sqrMagnitude > 0.001f)
			{
				transform.rotation = Quaternion.LookRotation(lightDir.normalized);
			}
			else
			{
				transform.rotation = Quaternion.LookRotation(new Vector3(0.5f, -1f, 0.5f).normalized);
			}

			Debug.Log($"Sky updated: BrightColor={brightColor}, Lum={lum:F3}, LightDir={lightDir}, Intensity={directionalLight.intensity:F2}");
		}

		private void OnGUI()
		{
			if (!hasRun) return;

			GUILayout.BeginArea(new Rect(10, 10, 500, 800));
			GUILayout.BeginVertical(GUI.skin.box);

			GUILayout.Label("<size=18><b>Cubemap Histogram Debug</b></size>");
			GUILayout.Space(10);

			GUILayout.Label($"Cubemap being analyzed: {lastCubemap?.name ?? "null"}");
			GUILayout.Label($"Resolution: {lastCubemap?.width ?? 0}×{lastCubemap?.width ?? 0}");

			GUILayout.Space(10);
			GUILayout.Label("Cubemap Preview (flattened cross layout):");
			Rect previewRect = GUILayoutUtility.GetRect(400, 300);
			if (cubemapPreviewTexture != null)
				GUI.DrawTexture(previewRect, cubemapPreviewTexture, ScaleMode.ScaleToFit);

			GUILayout.Space(10);
			GUILayout.Label($"Bright Avg Color: {debugBrightColor}");
			GUILayout.Label($"Max Lum: {debugMaxLum:F3}");
			GUILayout.Label($"Bright Pixels: {debugBrightPixelCount}");

			GUILayout.Space(10);
			GUILayout.Label("Luminance Histogram (upper hemisphere):");
			Rect histRect = GUILayoutUtility.GetRect(400, 150);
			GUI.Box(histRect, "");

			if (lumHistogram.Count > 0)
			{
				float bw = histRect.width / lumHistogram.Count;
				for (int i = 0; i < lumHistogram.Count; i++)
				{
					float h = lumHistogram[i] * histRect.height;
					Rect bar = new Rect(histRect.x + i * bw, histRect.y + histRect.height - h, bw - 1, h);
					GUI.color = Color.Lerp(Color.black, Color.white, (float)i / (lumHistogram.Count - 1));
					GUI.DrawTexture(bar, Texture2D.whiteTexture);
				}
				GUI.color = Color.white;
			}

			GUILayout.Space(10);
			GUILayout.Label("Bright Region Preview Color:");
			Rect colorRect = GUILayoutUtility.GetRect(140, 140);
			GUI.color = debugBrightColor;
			GUI.DrawTexture(colorRect, Texture2D.whiteTexture);
			GUI.color = Color.white;

			GUILayout.EndVertical();
			GUILayout.EndArea();
		}

		private void OnDestroy()
		{
			if (cubemapPreviewTexture != null)
				Destroy(cubemapPreviewTexture);
		}
	}
}