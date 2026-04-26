//using UnityEngine;
//using MassiveHadronLtd;

//namespace ClassicTilestorm
//{
//    internal class TileSelectableHandler : IEditorAttachmentHandler
//    {
//		public static readonly TileSelectableHandler Instance = new();

//		public void OnSelect(IMapEdit iMap, Camera camera, ISelectable selection)
//		{
//			var tile = (Tile)selection;
//			if (null != tile.gameObject)
//			{
//				tile.gameObject.SetActive(false);
//				//EditorDirectionUtil.ShowAt(tile.gameObject.transform.position + Vector3.up * 0.25f, tile.gameObject.transform.rotation, camera);
//			}
//		}

//		public void OnDeselect(IMapEdit iMap, Camera camera, ISelectable selection)
//		{
//			var tile = (Tile)selection;
//			if (null != tile.gameObject) tile.gameObject.SetActive(true);
//			//EditorDirectionUtil.Hide();
//		}

//		//public bool OnGizmoInput(IMapEdit iMap, Camera camera, ISelectable selection)
//		//{
//		//	if (!EditorDirectionUtil.HandleInput(camera, out Quaternion newWorldRot))
//		//		return false;

//		//	var variant = EditorSelectionUtil.CurrentVariant;
//		//	variant.angle = newWorldRot.eulerAngles.y;
//		//	EditorSelectionUtil.UpdateGhostMesh(variant);
//		//	return true;
//		//}

//		//public void OnUpdate(IMapEdit map, Camera camera, ISelectable selection)
//		//{
//		//	var rotation = Quaternion.AngleAxis(EditorSelectionUtil.CurrentRotation, Vector3.up);
//		//	EditorDirectionUtil.ShowAt(EditorSelectionUtil.CurrentPosition + Vector3.up * 0.25f, rotation, camera);
//		//}
//	}
//}