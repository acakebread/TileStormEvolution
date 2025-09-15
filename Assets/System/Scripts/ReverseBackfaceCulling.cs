//using UnityEngine;
//using UnityEngine.Rendering.Universal;

//[RequireComponent(typeof(Camera))]
//public class ReverseBackfaceCulling : MonoBehaviour
//{
//	private Camera cam;

//	void Start()
//	{
//		cam = GetComponent<Camera>();
//		Debug.Log($"ReverseBackfaceCulling: Initialized for camera {cam.name}");

//		// Verify the renderer has ReverseCullingRenderFeature
//		var urpAsset = UniversalRenderPipeline.asset;
//		if (urpAsset != null)
//		{
//			var rendererDataList = urpAsset.rendererDataList; // Use rendererDataList
//			if (rendererDataList != null && rendererDataList.Length > 0)
//			{
//				var rendererData = rendererDataList[0]; // Use first renderer data
//				if (rendererData != null)
//				{
//					foreach (var feature in rendererData.rendererFeatures)
//					{
//						if (feature is ReverseCullingRenderFeature)
//						{
//							Debug.Log("ReverseBackfaceCulling: Found ReverseCullingRenderFeature");
//							return;
//						}
//					}
//					Debug.LogWarning("ReverseBackfaceCulling: ReverseCullingRenderFeature not found in renderer");
//				}
//				else
//				{
//					Debug.LogWarning("ReverseBackfaceCulling: Renderer data is null");
//				}
//			}
//			else
//			{
//				Debug.LogWarning("ReverseBackfaceCulling: No renderer data list found in URP Asset");
//			}
//		}
//		else
//		{
//			Debug.LogWarning("ReverseBackfaceCulling: No URP Asset found");
//		}
//	}

//	void OnDisable()
//	{
//		GL.invertCulling = false; // Ensure culling is reset
//		Debug.Log("ReverseBackfaceCulling: Disabled and reset GL.invertCulling");
//	}
//}