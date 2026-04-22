using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public partial class Map
	{
		[NonSerialized] private readonly Dictionary<MapAttachment, GameObject> attachmentGameObjects = new();

		public MapAttachment[] GetAttachments(int? tileIndex = null, Type[] filterTypes = null)
		{
			var real = attachments ?? Array.Empty<MapAttachment>();

			MapAttachment[] waypointWrappers = Array.Empty<MapAttachment>();
			if (waypoints != null && waypoints.Length > 0)
			{
				waypointWrappers = new MapAttachment[waypoints.Length];
				for (var i = 0; i < waypoints.Length; i++)
					waypointWrappers[i] = new Waypoint(i, waypoints[i]);
			}

			var source = real.Concat(waypointWrappers).AsEnumerable();

			source = source.Where(a => a != null && a.tile >= 0);

			if (tileIndex.HasValue)
				source = source.Where(a => a.tile == tileIndex.Value);

			if (filterTypes != null && filterTypes.Length > 0)
			{
				var typeSet = new HashSet<Type>(filterTypes);
				source = source.Where(a => typeSet.Contains(a.GetType()));
			}

			return source.ToArray();
		}

		public void RefreshAttachment(MapAttachment attachment)
		{
			if (attachment == null) return;

			if (attachment is Waypoint wp)
			{
				if (waypoints != null && wp.waypointIndex >= 0 && wp.waypointIndex < waypoints.Length)
					waypoints[wp.waypointIndex] = wp.tile;
			}

			string prefabName = attachment switch
			{
				Waypoint => null,
				Emitter e => e.variant switch
				{
					"flame" => "flame",
					"spark" => "spark",
					_ => null
				},
				Pickup => null,
				View => null,
				_ => null
			};

			if (string.IsNullOrEmpty(prefabName))
			{
				DestroyAttachmentInstance(attachment);
				return;
			}

			Vector3 localPos = attachment switch
			{
				Waypoint w => Vector3.zero,
				Emitter e => e.Position,
				Pickup => Vector3.up * 0.5f,
				View v => v.Position,
				_ => Vector3.zero
			};

			Quaternion rotation = attachment switch
			{
				Waypoint w => Quaternion.identity,
				Emitter e => e.Rotation,
				Pickup => Quaternion.identity,
				View v => v.Rotation,
				_ => Quaternion.identity
			};

			Vector3 worldPos = TileRenderPosition(attachment.tile) + localPos;

			if (attachmentGameObjects.TryGetValue(attachment, out GameObject go) && go != null)
			{
				go.transform.position = worldPos;
				go.transform.rotation = rotation;
				return;
			}

			go = Assets.PrefabAssets.Instantiate(prefabName, worldPos, rotation, parent);
			go.name = $"{attachment.TypeName}_{prefabName}_tile{attachment.tile}";
			attachmentGameObjects[attachment] = go;
		}

		public void AddAttachment(MapAttachment attachment)
		{
			if (attachment == null) return;

			if (attachment is Waypoint wp)
			{
				var list = waypoints?.ToList() ?? new List<int>();
				while (list.Count <= wp.waypointIndex)
					list.Add(-1);
				list[wp.waypointIndex] = wp.tile;
				waypoints = list.ToArray();
			}
			else
			{
				var list = attachments?.ToList() ?? new List<MapAttachment>();
				list.Add(attachment);
				attachments = list.ToArray();
			}

			RefreshAttachment(attachment);
			OnMapEdited?.Invoke(this, false, Vector3.zero);
		}

		public bool RemoveAttachment(MapAttachment attachment)
		{
			if (attachment == null) return false;

			bool removed = false;

			if (attachment is Waypoint wp)
			{
				if (waypoints == null || wp.waypointIndex < 0 || wp.waypointIndex >= waypoints.Length)
					return false;

				var newWaypoints = new List<int>(waypoints.Length - 1);

				for (var i = 0; i < waypoints.Length; i++)
				{
					if (i != wp.waypointIndex)
						newWaypoints.Add(waypoints[i]);
				}

				waypoints = newWaypoints.Count > 0 ? newWaypoints.ToArray() : null;

				removed = true;
			}
			else if (attachments != null)
			{
				var idx = Array.IndexOf(attachments, attachment);
				if (idx >= 0)
				{
					var list = attachments.ToList();
					list.RemoveAt(idx);
					attachments = list.Count > 0 ? list.ToArray() : null;
					removed = true;
				}
			}

			if (removed)
			{
				DestroyAttachmentInstance(attachment);
				OnMapEdited?.Invoke(this, false, Vector3.zero);
			}

			return removed;
		}

		public bool RemoveAttachments(MapAttachment[] attachmentArray)
		{
			if (attachmentArray == null || attachmentArray.Length == 0)
				return false;

			var anyRemoved = false;

			var waypointsToRemove = attachmentArray.OfType<Waypoint>().ToList();
			var others = attachmentArray.Where(a => a is not Waypoint).ToArray();

			foreach (var att in others)
			{
				if (RemoveAttachment(att))
					anyRemoved = true;
			}

			var sortedWaypoints = waypointsToRemove.OrderByDescending(wp => wp.waypointIndex).ToList();

			foreach (var wp in sortedWaypoints)
			{
				if (RemoveAttachment(wp))
					anyRemoved = true;
			}

			return anyRemoved;
		}

		public void RefreshAttachments(MapAttachment[] attachmentsToRefresh)
		{
			if (attachmentsToRefresh == null || attachmentsToRefresh.Length == 0)
				return;

			foreach (var att in attachmentsToRefresh)
				RefreshAttachment(att);
		}

		private void DestroyAttachmentInstance(MapAttachment attachment)
		{
			if (attachment == null) return;

			if (attachmentGameObjects.TryGetValue(attachment, out GameObject go) && go != null)
			{
				if (Application.isPlaying)
					UnityEngine.Object.Destroy(go);
				else
					UnityEngine.Object.DestroyImmediate(go);
			}

			attachmentGameObjects.Remove(attachment);
		}

		private void CleanupAttachmentInstances()
		{
			foreach (var att in attachmentGameObjects.Keys.ToList())
				DestroyAttachmentInstance(att);

			attachmentGameObjects.Clear();
		}
	}
}