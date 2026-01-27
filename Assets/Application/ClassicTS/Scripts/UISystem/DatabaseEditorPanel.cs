using UnityEngine;
using UnityEngine.UI;
//using TMPro;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using MassiveHadronLtd;
//using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public class DatabaseEditorPanel : UIPanel
	{
		#region Serialized Fields - UI References

		[Header("UI References")]
		[SerializeField] private Button closeButton;
		[SerializeField] private ScrollRect definitionScrollView;
		[SerializeField] private Transform contentParent;
		[SerializeField] private GameObject definitionListItemPrefab;

		[SerializeField] private Button ButtonInsert;
		[SerializeField] private Button ButtonDelete;
		[SerializeField] private Button ButtonMoveUp;
		[SerializeField] private Button ButtonMoveDown;

		//[Header("Preview")]
		//[SerializeField] private RawImage previewImage;

		//[Header("Dynamic Properties Panel")]
		//[SerializeField] private RectTransform flagPropertiesRect;
		//[SerializeField] private GameObject flagTogglePrefab;

		//[Header("ID Input")]
		//[SerializeField] private TMP_InputField IDInput;

		//[Header("Dropdowns")]
		//[SerializeField] private TMP_Dropdown modelDropdown;
		//[SerializeField] private string noneModelOptionText = "— None —";

		//[SerializeField] private TMP_Dropdown textureSequenceDropdown;
		//[SerializeField] private string noneTextureOptionText = "— None —";

		//[SerializeField] private TMP_Dropdown materialDropdown;
		//[SerializeField] private string noneMaterialOptionText = "— None —";

		#endregion

		#region Serialized Fields - Preview Settings

		//[Header("Preview Settings")]
		//[SerializeField] private Color backgroundColor = new Color(0.129f, 0.698f, 0.882f);
		//[SerializeField] private float fieldOfView = 60f;
		//[SerializeField] private float sizeToDistanceFactor = 1f;
		//[SerializeField] private float defaultTiltAngle = 30f;
		//[SerializeField] private float minTiltAngle = 0f;
		//[SerializeField] private float maxTiltAngle = 90f;
		//[SerializeField] private float minDistance = 0.8f;
		//[SerializeField] private float maxDistance = 10f;
		//[SerializeField] private float dragOrbitSensitivity = 0.2f;
		//[SerializeField] private float dragTiltSensitivity = 0.2f;
		//[SerializeField] private float scrollZoomSensitivity = 0.5f;
		//[SerializeField] private float autoRotateSpeed = -15f;

		//[Header("Ground Plane Settings")]
		//[SerializeField] private Color groundColor = Color.white;
		//[SerializeField] private float groundSize = 2.5f;
		//[SerializeField] private float groundY = -0.01f;
		//[SerializeField] private float groundUVScale = 1f;
		//[SerializeField] private Texture2D groundOverrideTexture;

		#endregion

		protected override void Awake()
		{
			base.Awake();

			InitializeUIReferences();
		}

		protected override void OnEnable()
		{
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			base.OnDisable();
		}

		private void InitializeUIReferences()
		{
			if (closeButton) closeButton.onClick.AddListener(() => gameObject.SetActive(false));

			if (!contentParent && definitionScrollView)
				contentParent = definitionScrollView.content;
		}
	}
}