using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class SkyCubeDebugUtil : MonoBehaviour
	{
		[Header("SkyCube Debug")]
		[SerializeField] private bool showDebugPanel = true;

		[SerializeField, Range(256, 2048)]
		private int panoramaWidth = 512;

		[SerializeField, Range(128, 1024)]
		private int panoramaHeight = 256;

		public Texture2D panoramaTexture;
		private List<float> lumHistogram = new List<float>();
		private Color brightColor = Color.gray;
		private float maxLum = 0f;
		private int brightPixelCount = 0;
		private Cubemap lastCubemap;

		private DirectionalLightUtility directionalLightUtil;

		private void Awake()
		{
			SkyboxUtility.OnSkyboxChanged += UpdateFromSkybox;
			directionalLightUtil = FindAnyObjectByType<DirectionalLightUtility>(FindObjectsInactive.Include);
		}

		private void OnDestroy()
		{
			SkyboxUtility.OnSkyboxChanged -= UpdateFromSkybox;
			Clear();
		}

		private void Update()
		{
			//if (directionalLightUtil == null)
			//	directionalLightUtil = FindAnyObjectByType<DirectionalLightUtility>(FindObjectsInactive.Include);

			//if (directionalLightUtil != null)
			//{
			//	Vector3 testDir = GetTestSunDirection();
			//	directionalLightUtil.transform.rotation = Quaternion.LookRotation(-testDir);
			//}
		}

		private Vector2 sunUV;
		private Vector3 lightDir;

		private void UpdateFromSkybox(Material skybox = null)
		{
			Cubemap cubemap = SkyboxUtility.GetTintedSkyboxCubemap(skybox);
			if (cubemap == null)
			{
				Clear();
				return;
			}

			lastCubemap = cubemap;

			if (panoramaTexture != null)
				Destroy(panoramaTexture);

			//panoramaTexture = AtlasCubemapUtility.FlattenCubemap(cubemap);
			//panoramaTexture = EquirectangularCubemapUtility.Create(cubemap, panoramaWidth, panoramaHeight);
			panoramaTexture = LinearCubemapUtility.Create(cubemap, panoramaWidth, panoramaHeight);

			brightColor = ImageProcessing.ComputeBrightColorWithHistogram(
				panoramaTexture,
				out lumHistogram,
				out maxLum,
				out brightPixelCount,
				threshold: 0.85f);

			sunUV = ImageProcessing.FindSunUV(panoramaTexture, 0.95f);
			//lightDir = AtlasCubemapUtility.FindLightDirection(cubemap);
		}

		private void OnGUI()
		{
			if (!showDebugPanel) return;

			GUILayout.BeginArea(new Rect(10, 10, 600, 920));
			GUILayout.BeginVertical(GUI.skin.box);

			GUILayout.Label("<size=18><b>Sky Cubemap Debug - Oval Panorama</b></size>");
			GUILayout.Space(10);

			if (lastCubemap == null)
			{
				GUILayout.Label("Waiting for skybox...");
				GUILayout.EndVertical();
				GUILayout.EndArea();
				return;
			}

			GUILayout.Label($"Cubemap: {lastCubemap.name}");
			GUILayout.Label($"Resolution: {lastCubemap.width} × {lastCubemap.width}");

			GUILayout.Space(10);
			GUILayout.Label("Oval Panorama Preview:");
			Rect previewRect = GUILayoutUtility.GetRect(560, 280);

			if (panoramaTexture != null)
			{
				//GUI.DrawTexture(previewRect, panoramaTexture, ScaleMode.StretchToFill);
				GUI.DrawTexture(previewRect, panoramaTexture, ScaleMode.StretchToFill);

				//Vector3 testDir = GetTestSunDirection();
				//Vector2 markerPos = GetOvalMarkerPosition(previewRect, testDir);

				var markerPos = new Vector2(previewRect.xMin + sunUV.x * previewRect.width, previewRect.yMin + sunUV.y * previewRect.height);

				GUI.color = Color.yellow;
				GUI.DrawTexture(new Rect(markerPos.x - 12, markerPos.y - 1, 24, 2), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(markerPos.x - 1, markerPos.y - 12, 2, 24), Texture2D.whiteTexture);
				GUI.color = Color.white;
			}

			GUILayout.Space(8);
			//GUILayout.Label($"<color=yellow>TEST SUN DIRECTION: {GetTestSunDirection():F3}</color>");

			GUILayout.Space(10);
			GUILayout.Label($"Bright Color: {brightColor}");
			GUILayout.Label($"Max Luminance: {maxLum:F3}");
			GUILayout.Label($"Bright Pixels: {brightPixelCount}");

			GUILayout.Space(10);
			GUILayout.Label("Luminance Histogram:");
			Rect histRect = GUILayoutUtility.GetRect(560, 180);
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

			GUILayout.EndVertical();
			GUILayout.EndArea();
		}

		//private Vector2 GetOvalMarkerPosition(Rect previewRect, Vector3 dir)
		//{
		//	dir = dir.normalized;

		//	float longitude = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
		//	if (longitude < 0) longitude += 360f;

		//	float latitude = Mathf.Asin(dir.y) * Mathf.Rad2Deg;

		//	float u = (longitude + 180f) / 360f;
		//	float v = (latitude + 90f) / 180f;

		//	return new Vector2(
		//		previewRect.x + u * previewRect.width,
		//		previewRect.y + (1f - v) * previewRect.height
		//	);
		//}

		private void Clear()
		{
			lastCubemap = null;
			lumHistogram.Clear();
			brightPixelCount = 0;
			maxLum = 0f;
			brightColor = Color.gray;

			if (panoramaTexture != null)
			{
				Destroy(panoramaTexture);
				panoramaTexture = null;
			}
		}

		public static Vector3 GetTestSunDirection()
		{
			float time = Time.time * 0.4f;

			float azimuth = time * 360f;
			float elevation = 15f + Mathf.Sin(time * 0.7f) * 35f;

			float rad = azimuth * Mathf.Deg2Rad;

			return new Vector3(
				Mathf.Sin(rad),
				Mathf.Sin(elevation * Mathf.Deg2Rad),
				Mathf.Cos(rad)
			).normalized;
		}
	}
}