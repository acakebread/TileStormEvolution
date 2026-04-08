using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class SkyboxEditorPanel : UIPanel
	{
		[SerializeField] private RawImage skyboxImage;
		[SerializeField] private RawImage cursorImage;     // Small crosshair/dot, child of skyboxImage

		public Action<Vector2> onValueChanged;

		private UIDragHandler dragHandler;
		private Vector2 currentNormalizedUV = new Vector2(0.5f, 0.75f);

		protected override void OnEnable()
		{
			base.OnEnable();

			if (skyboxImage == null) return;

			dragHandler = skyboxImage.GetComponent<UIDragHandler>();
			if (dragHandler == null)
				dragHandler = skyboxImage.gameObject.AddComponent<UIDragHandler>();

			dragHandler.OnPointerDownEvent += OnSkyboxPointer;
			dragHandler.OnDragEvent += OnSkyboxPointer;

			UpdateCursorPosition(currentNormalizedUV);
		}

		protected override void OnDisable()
		{
			if (dragHandler != null)
			{
				dragHandler.OnPointerDownEvent -= OnSkyboxPointer;
				dragHandler.OnDragEvent -= OnSkyboxPointer;
			}

			base.OnDisable();
		}

		protected override void LateUpdate()
		{
			base.LateUpdate();
			UpdateCursorPosition(currentNormalizedUV);   // keeps cursor correct after resize
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Set initial skybox + skyvec
		// ────────────────────────────────────────────────────────────────────────────────

		public void SetInitialSkybox(Material skyboxMaterial, float[] currentSkyvec = null)
		{
			if (skyboxImage == null) return;

			Texture2D linearTexture = null;
			Cubemap sourceCubemap = CubemapUtility.GetTintedCubemap(skyboxMaterial);

			if (sourceCubemap != null)
			{
				linearTexture = LinearCubemapUtility.Create(sourceCubemap, width: 512, height: 512);
			}

			Vector2 initialUV;

			if (currentSkyvec != null && currentSkyvec.Length >= 2)
			{
				initialUV = new Vector2(currentSkyvec[0], currentSkyvec[1]);
			}
			else if (linearTexture != null)
			{
				initialUV = ImageProcessing.FindSunUV(linearTexture, scanAboveHorizonOnly: true);
			}
			else
			{
				initialUV = new Vector2(0.5f, 0.75f);
			}

			skyboxImage.texture = linearTexture;

			currentNormalizedUV = initialUV;
			UpdateCursorPosition(initialUV);

			onValueChanged?.Invoke(initialUV);
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Dragging
		// ────────────────────────────────────────────────────────────────────────────────

		private void OnSkyboxPointer(UIDragHandler sender, PointerEventData eventData)
		{
			if (skyboxImage == null) return;

			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
				skyboxImage.rectTransform,
				eventData.position,
				eventData.pressEventCamera,
				out Vector2 localPos))
			{
				return;
			}

			Rect rect = skyboxImage.rectTransform.rect;

			float normX = Mathf.Clamp01((localPos.x - rect.xMin) / rect.width);
			float normY = Mathf.Clamp01((localPos.y - rect.yMin) / rect.height);

			// IMPORTANT: Invert Y to match LinearCubemapUtility (sky = top of texture)
			currentNormalizedUV = new Vector2(normX, normY);

			UpdateCursorPosition(currentNormalizedUV);
			onValueChanged?.Invoke(currentNormalizedUV);
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Cursor (now correctly synced with Linear projection)
		// ────────────────────────────────────────────────────────────────────────────────

		private void UpdateCursorPosition(Vector2 normalizedUV)
		{
			if (cursorImage == null || skyboxImage == null) return;

			RectTransform imageRT = skyboxImage.rectTransform;

			// Convert normalized UV to local position inside RawImage
			Vector2 localPos = new Vector2(
				Mathf.Lerp(imageRT.rect.xMin, imageRT.rect.xMax, normalizedUV.x),
				Mathf.Lerp(imageRT.rect.yMin, imageRT.rect.yMax, normalizedUV.y)   // y already inverted above
			);

			cursorImage.rectTransform.anchoredPosition = localPos;
		}
	}
}