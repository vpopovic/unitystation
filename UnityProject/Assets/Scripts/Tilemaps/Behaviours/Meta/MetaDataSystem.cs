﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Tilemaps.Behaviours.Meta;
using UnityEngine;

public class MetaDataSystem : SubsystemBehaviour
{
	private SubsystemManager subsystemManager;
	private HashSet<MetaDataNode> externalNodes;

	// Set higher priority to ensure that it is executed before other systems
	public override int Priority => 100;

	public override void Awake()
	{
		base.Awake();

		subsystemManager = GetComponentInParent<SubsystemManager>();

		externalNodes = new HashSet<MetaDataNode>();
	}

	public override void Initialize()
	{
		Stopwatch sw = new Stopwatch();
		sw.Start();

		if (MatrixManager.IsInitialized)
		{
			LocateRooms();
		}

		sw.Stop();

		Logger.Log("MetaData init: " + sw.ElapsedMilliseconds + " ms", Category.Matrix);
	}

	public override void UpdateAt(Vector3Int position)
	{
		MetaDataNode node = metaDataLayer.Get(position);

		if (metaTileMap.IsAtmosPassableAt(position))
		{
			MetaUtils.RemoveFromNeighbors(node);
			node.ClearNeighbors();

			SetupNeighbors(node);
			MetaUtils.AddToNeighbors(node);

			node.Type = metaTileMap.IsSpaceAt(position) ? NodeType.Space : NodeType.Room;
		}
		else
		{
			node.Type = NodeType.Occupied;
			MetaUtils.RemoveFromNeighbors(node);
		}
	}

	private void LocateRooms()
	{
		BoundsInt bounds = metaTileMap.GetBounds();

		foreach (Vector3Int position in bounds.allPositionsWithin)
		{
			FindRoomAt(position);
		}
	}

	private void FindRoomAt(Vector3Int position)
	{
		if (Check(position) && !metaDataLayer.IsRoomAt(position))
		{
			CreateRoom(position);
		}
		else
		{
			if (!metaTileMap.IsAtmosPassableAt(position))
			{
				MetaDataNode node = metaDataLayer.Get(position);
				node.Type = NodeType.Occupied;

				SetupNeighbors(node);
			}
		}
	}

	private void CreateRoom(Vector3Int origin)
	{
		var roomPositions = new HashSet<Vector3Int>();
		var freePositions = new UniqueQueue<Vector3Int>();

		freePositions.Enqueue(origin);

		var isSpace = false;

		while (!freePositions.IsEmpty)
		{
			Vector3Int position;
			if (freePositions.TryDequeue(out position))
			{
				roomPositions.Add(position);

				foreach (Vector3Int neighbor in MetaUtils.GetNeighbors(position))
				{
					if (Check(neighbor))
					{
						if (!roomPositions.Contains(neighbor) && !freePositions.Contains(neighbor) && !metaDataLayer.IsRoomAt(neighbor))
						{
							freePositions.Enqueue(neighbor);
						}
					}
					else if (metaTileMap.IsSpaceAt(neighbor))
					{
						Vector3 worldPosition = transform.TransformPoint(neighbor);
						if (MatrixManager.IsSpaceAt(worldPosition.RoundToInt()))
						{
							isSpace = true;
						}
					}
				}
			}
		}

		if (!isSpace)
		{
			AssignRoom(roomPositions);
		}

		SetupNeighbors(roomPositions);
	}

	private void AssignRoom(IEnumerable<Vector3Int> positions)
	{
		foreach (Vector3Int position in positions)
		{
			MetaDataNode node = metaDataLayer.Get(position);

			node.Type = NodeType.Room;
		}
	}

	private bool Check(Vector3Int position)
	{
		return metaTileMap.IsAtmosPassableAt(position) && !metaTileMap.IsSpaceAt(position);
	}

	private void SetupNeighbors(IEnumerable<Vector3Int> positions)
	{
		foreach (Vector3Int position in positions)
		{
			SetupNeighbors(metaDataLayer.Get(position));
		}
	}

	private void SetupNeighbors(MetaDataNode node)
	{
		foreach (Vector3Int neighbor in MetaUtils.GetNeighbors(node.Position))
		{
			if (metaTileMap.IsSpaceAt(neighbor))
			{
				if (node.IsRoom)
				{
					externalNodes.Add(node);
				}

				Vector3 worldPosition = transform.TransformPoint(neighbor) + Vector3.one * 0.5f;
				worldPosition.z = 0;
				if (!MatrixManager.IsSpaceAt(worldPosition.RoundToInt()))
				{
					MatrixInfo matrixInfo = MatrixManager.AtPoint(worldPosition.RoundToInt());

					Vector3Int localPosition = MatrixManager.WorldToLocalInt(worldPosition, matrixInfo);

					if (matrixInfo.MetaTileMap.IsAtmosPassableAt(localPosition))
					{
						node.AddNeighbor(matrixInfo.MetaDataLayer.Get(localPosition));
					}

					continue;
				}
			}

			if (metaTileMap.IsAtmosPassableAt(neighbor))
			{
				node.AddNeighbor(metaDataLayer.Get(neighbor));
			}
		}
	}

	private void Update()
	{
		foreach (MetaDataNode node in externalNodes)
		{
			subsystemManager.UpdateAt(node.Position);
		}
	}
}