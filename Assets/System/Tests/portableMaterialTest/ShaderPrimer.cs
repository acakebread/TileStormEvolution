//using System.Collections;
//using UnityEngine;

//[ExecuteAlways]
//public class ShaderPrimer : MonoBehaviour
//{
//	void Awake()
//	{
//		PrimeShaders();
//	}

//	private void PrimeShaders()
//	{
//		// Prime the exact URP Lit variant used by the portable material test,
//		// including base-map texture sampling.
//		var template = Resources.Load<Material>("ForceInclude/URP lit opaque");
//		var testTexture = Resources.Load<Texture2D>("test");

//		if (template != null)
//		{
//			var dummy = new Material(template);
//			if (testTexture != null)
//			{
//				dummy.SetTexture("_BaseMap", testTexture);
//				dummy.SetTexture("_MainTex", testTexture);
//				dummy.mainTexture = testTexture;
//			}

//			dummy.color = Color.white;
//			dummy.SetColor("_EmissionColor", Color.green);
//			dummy.EnableKeyword("_EMISSION");

//			// Use a hidden primitive so Unity has to bind the material as a renderer target.
//			var primingObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
//			primingObject.name = "ShaderPrimer_PrimeCube";
//			primingObject.hideFlags = HideFlags.HideAndDontSave;
//			primingObject.transform.position = new Vector3(0f, 1f, 0f);
//			primingObject.transform.localScale = Vector3.one * 0.01f;

//			var renderer = primingObject.GetComponent<MeshRenderer>();
//			renderer.sharedMaterial = dummy;

//			if (Application.isPlaying)
//				StartCoroutine(CleanupAfterFrame(primingObject, dummy));
//			else
//			{
//				DestroyImmediate(primingObject);
//				DestroyImmediate(dummy);
//			}
//			Debug.Log("ShaderPrimer: Pre-warmed URP Lit with texture and emission");
//		}
//	}

//	private IEnumerator CleanupAfterFrame(GameObject primingObject, Material dummy)
//	{
//		yield return null;
//		DestroyImmediate(primingObject);
//		DestroyImmediate(dummy);
//	}
//}
