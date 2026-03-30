using UnityEngine;
using UnityEngine.UI;
using MassiveHadronLtd;
using UnityEngine.EventSystems;
using System;
using TMPro;

namespace ClassicTilestorm
{
	public class ColourSelectorPanel : UIPanel
	{
		[SerializeField] private RawImage colourPickerImage;
		[SerializeField] private RawImage brightnessPickerImage;
		[SerializeField] private RawImage swatchImage;
		[SerializeField] private TMP_InputField hexColourInput;

		// Color picker state - now includes alpha
		private Texture2D colorTexture;
		private Texture2D valueTexture;
		private float currentHue = 0f;
		private float currentSaturation = 0.8f;
		private float currentValue = 1f;
		private float currentAlpha = 1f;

		private UIDragHandler colorDrag;
		private UIDragHandler valueDrag;

		public Action<Color> onValueChanged;

		protected override void OnEnable()
		{
			base.OnEnable();

			colorDrag = colourPickerImage?.GetComponent<UIDragHandler>();
			valueDrag = brightnessPickerImage?.GetComponent<UIDragHandler>();

			if (colorDrag != null)
			{
				colorDrag.OnPointerDownEvent += OnColorPointer;
				colorDrag.OnDragEvent += OnColorPointer;
			}

			if (valueDrag != null)
			{
				valueDrag.OnPointerDownEvent += OnValuePointer;
				valueDrag.OnDragEvent += OnValuePointer;
			}

			if (colourPickerImage != null && brightnessPickerImage != null)
			{
				colorTexture = ColorPickerSquareUtility.CreateColorPickerTexture(
					size: 256,
					style: ColorPickerSquareUtility.PickerStyle.HueSaturation_FullValue
				);
				colourPickerImage.texture = colorTexture;

				UpdateValueSlider();
			}

			UpdateHexInput();

			if (hexColourInput != null)
				hexColourInput.onEndEdit.AddListener(OnHexInputEndEdit);
		}

		protected override void OnDisable()
		{
			if (hexColourInput != null)
				hexColourInput.onEndEdit.RemoveListener(OnHexInputEndEdit);

			base.OnDisable();
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Color Picker (now preserves alpha)
		// ────────────────────────────────────────────────────────────────────────────────

		private void OnColorPointer(UIDragHandler sender, PointerEventData eventData)
		{
			UpdateColorFromPointer(eventData, colourPickerImage.rectTransform, colorTexture, true);
		}

		private void OnValuePointer(UIDragHandler sender, PointerEventData eventData)
		{
			UpdateColorFromPointer(eventData, brightnessPickerImage.rectTransform, valueTexture, false);
		}

		private void UpdateColorFromPointer(PointerEventData eventData, RectTransform rt, Texture2D tex, bool isColorSquare)
		{
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out Vector2 localPos))
				return;

			Color picked = ColorPickerSquareUtility.GetColorFromLocalPoint(tex, localPos, rt);

			if (isColorSquare)
			{
				Color.RGBToHSV(picked, out currentHue, out currentSaturation, out _);
				UpdateValueSlider();
			}
			else
			{
				Color.RGBToHSV(picked, out _, out _, out currentValue);
			}

			Color final = Color.HSVToRGB(currentHue, currentSaturation, currentValue);
			final.a = currentAlpha;                    // ← Preserve alpha

			UpdateSwatchAndNotify(final);
			UpdateHexInput();
		}

		private void UpdateValueSlider()
		{
			if (valueTexture != null) Destroy(valueTexture);
			valueTexture = ColorPickerSquareUtility.CreateValueSliderTexture(
				height: 256,
				hue: currentHue,
				saturation: currentSaturation
			);
			brightnessPickerImage.texture = valueTexture;
		}

		// ────────────────────────────────────────────────────────────────────────────────
		//   Hex Input (with full alpha + shorthand support)
		// ────────────────────────────────────────────────────────────────────────────────

		private void UpdateHexInput()
		{
			if (hexColourInput == null) return;

			Color currentColor = Color.HSVToRGB(currentHue, currentSaturation, currentValue);
			currentColor.a = currentAlpha;

			string hex = (currentAlpha >= 0.999f)
				? ColorUtility.ToHtmlStringRGB(currentColor)
				: ColorUtility.ToHtmlStringRGBA(currentColor);

			hexColourInput.text = hex;   // No '#'
		}

		private void OnHexInputEndEdit(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
			{
				UpdateHexInput();
				return;
			}

			string cleanInput = input.Trim().TrimStart('#').ToUpperInvariant();

			if (string.IsNullOrEmpty(cleanInput))
			{
				UpdateHexInput();
				return;
			}

			// Try full parse first
			if (ColorUtility.TryParseHtmlString("#" + cleanInput, out Color parsedColor))
			{
				ApplyParsedColor(parsedColor);
				return;
			}

			// Try shorthand expansion
			string expanded = ExpandShorthandHex(cleanInput);
			if (!string.IsNullOrEmpty(expanded) &&
				ColorUtility.TryParseHtmlString("#" + expanded, out parsedColor))
			{
				ApplyParsedColor(parsedColor);
			}
			else
			{
				// Invalid → revert
				UpdateHexInput();
			}
		}

		private string ExpandShorthandHex(string hex)
		{
			switch (hex.Length)
			{
				case 3: return $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
				case 4: return $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}";
				case 6:
				case 8: return hex;
				default: return null;
			}
		}

		private void ApplyParsedColor(Color parsedColor)
		{
			// Extract alpha before converting to HSV
			currentAlpha = parsedColor.a;

			// Convert RGB to HSV (alpha is stored separately)
			Color.RGBToHSV(parsedColor, out currentHue, out currentSaturation, out currentValue);

			UpdateValueSlider();

			Color finalColor = Color.HSVToRGB(currentHue, currentSaturation, currentValue);
			finalColor.a = currentAlpha;               // ← Restore alpha

			UpdateSwatchAndNotify(finalColor);
			UpdateHexInput();
		}

		private void UpdateSwatchAndNotify(Color color)
		{
			if (swatchImage != null)
				swatchImage.color = color;

			onValueChanged?.Invoke(color);
		}
	}
}