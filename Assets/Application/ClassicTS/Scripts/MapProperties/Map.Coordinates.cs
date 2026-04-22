using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public partial class Map
	{
		public static Vector3 FullFloorVec(Vector3 vec) => new(Mathf.FloorToInt(vec.x), vec.y, Mathf.FloorToInt(vec.z));
		public static Vector3 HalfFloorVec(Vector3 vec) => new(Mathf.FloorToInt(vec.x * 2f) * 0.5f, vec.y, Mathf.FloorToInt(vec.z * 2f) * 0.5f);

		public static Vector3 WorldToRender(Vector3 value) => TileOriginShift.WorldToRender(value);
		public static Vector3 RenderToWorld(Vector3 value) => TileOriginShift.RenderToWorld(value);

		public static bool RayToWorld(Ray ray, out Vector3 point)
		{
			point = Vector3.zero;
			if (GeomUtils.RayToPlane(ray, new Plane(Vector3.up, Vector3.zero), out Vector3 result))
			{
				point = TileOriginShift.AdjustRaycastResult(result);
				return true;
			}
			return false;
		}

		public static Vector3 ScreenToWorld(Camera camera, Vector3 screenPos, float offset = 0f)
		{
			if (null == camera) return Vector3.negativeInfinity;

			if (GeomUtils.RayToPlane(camera.ScreenPointToRay(screenPos),
									 new Plane(Vector3.up, Vector3.up * offset),
									 out Vector3 result))
			{
				return TileOriginShift.AdjustRaycastResult(result);
			}

			return Vector3.negativeInfinity;
		}

		public static Vector3 CameraToWorld(Camera camera, Vector3 direction = default)
		{
			if (null == camera) return Vector3.negativeInfinity;
			if (default == direction) direction = camera.transform.forward;
			var ray = new Ray(camera.transform.position, direction);
			RayToWorld(ray, out Vector3 result);
			return result;
		}

		public static bool ValidExtents(RectInt extents) => extents.width <= MAP_MAX_SIZE && extents.height <= MAP_MAX_SIZE;

		public int VectorToIndex(Vector3 vec) => vec.x < 0 || vec.x >= width || vec.z < 0 || vec.z >= height ? width > 0 ? -1 : 0 : Mathf.FloorToInt(vec.z) * width + Mathf.FloorToInt(vec.x);
		public Vector3 IndexToVector(int index) => width > 0 ? new(index % width, 0f, index / width) : Vector3.zero;
		public Vector3 TileRenderPosition(int index) => WorldToRender(IndexToVector(index));

		public int CameraHitTile(Camera camera, Vector3 position) => VectorToIndex(ScreenToWorld(camera, position));
		public Variant CameraHitVariant(Camera camera, Vector3 position) => GetVariantAt(CameraHitTile(camera, position));
		public Definition CameraHitDefinition(Camera camera, Vector3 position) => GetDefinitionAt(CameraHitTile(camera, position));

		public Quaternion LocalRotation(int tileIndex, Quaternion worldRotation) => worldRotation;
		public Quaternion WorldRotation(int tileIndex, Quaternion localRotation) => localRotation;

		public Vector3 LocalPosition(int tileIndex, Vector3 worldPosition) => tileIndex < 0 ? worldPosition : worldPosition - TileRenderPosition(tileIndex);
		public Vector3 WorldPosition(int tileIndex, Vector3 localPosition) => tileIndex < 0 ? localPosition : localPosition + TileRenderPosition(tileIndex);

		public HashId GetTileID(int index)
		{
			if (tiles == null || index < 0 || index >= tiles.Length)
				return 0;

			var tableIdx = tiles[index];
			if (tableIdx >= 0 && tableIdx < variants.Length)
				return variants[tableIdx].hash;

			return 0;
		}
	}
}