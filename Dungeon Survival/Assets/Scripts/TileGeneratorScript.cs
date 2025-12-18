using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileGeneratorScript : MonoBehaviour
{
	[SerializeField] public Tilemap CollisionTilemap;
	[SerializeField] public Tilemap NonCollisionTilemap;
	[SerializeField] private TileBase[] wallTiles;
	[SerializeField] private TileBase[] groundTiles;

	//Tile count for pathfinding purposes
	public int tileCount;

	public RoomTemplate[] roomTemplates;

	// Generated list of walkable cells
	private List<Vector3Int> walkableCells = new List<Vector3Int>();

	//Generate a Combined class of the Tilemaps for pathfinding purposes
	

	[Header("Seed")]
	[Tooltip("If non-empty, the dungeon generation will be seeded by this string for deterministic results. Leave empty for normal random behaviour.")]
	[SerializeField] private string seed = "";

   

	// ----------------- existing methods & generation -------------------------
	void Start()
	{
		if (!ValidateSetup()) return;
		RebuildNavGrid();
	}

	private bool ValidateSetup()
	{
		bool ok = true;
		if (CollisionTilemap == null) { Debug.LogWarning("CollisionTilemap is not assigned."); }
		if (NonCollisionTilemap == null) { Debug.LogWarning("NonCollisionTilemap is not assigned."); }
		if (wallTiles == null || wallTiles.Length ==0) { Debug.LogWarning("wallTiles is not configured."); }
		if (groundTiles == null || groundTiles.Length ==0) { Debug.LogWarning("groundTiles is not configured."); }
		return ok;
	}

	// Rebuild the shared walkable grid using the current tilemaps.
	// A cell is walkable when there is NO tile on the collision tilemap and
	// there IS a tile on the non-collision (ground) tilemap (if available).
	// This produces SharedWalkableGrid and origin/size metadata.
	public void RebuildNavGrid()
	{
		walkableCells.Clear();

		if (CollisionTilemap == null && NonCollisionTilemap == null)
		{
			Debug.LogWarning("No tilemaps assigned to build nav grid.");
			tileCount =0;
			return;
		}

		// Determine union bounds of both tilemaps
		BoundsInt bounds = CollisionTilemap != null ? CollisionTilemap.cellBounds : NonCollisionTilemap.cellBounds;
		if (NonCollisionTilemap != null) bounds = EncapsulateBounds(bounds, NonCollisionTilemap.cellBounds);
		if (CollisionTilemap != null) bounds = EncapsulateBounds(bounds, CollisionTilemap.cellBounds);

		for (int x = bounds.xMin; x < bounds.xMax; x++)
		{
			for (int y = bounds.yMin; y < bounds.yMax; y++)
			{
				Vector3Int cell = new Vector3Int(x, y,0);
				bool hasCollision = CollisionTilemap != null && CollisionTilemap.GetTile(cell) != null;
				bool hasGround = NonCollisionTilemap == null || NonCollisionTilemap.GetTile(cell) != null; // if no non-collision map treat as ground

				if (!hasCollision && hasGround)
				{
					walkableCells.Add(cell);
				}
			}
		}

		tileCount = walkableCells.Count;
	}

	private BoundsInt EncapsulateBounds(BoundsInt a, BoundsInt b)
	{
		int xMin = Math.Min(a.xMin, b.xMin);
		int yMin = Math.Min(a.yMin, b.yMin);
		int zMin = Math.Min(a.zMin, b.zMin);
		int xMax = Math.Max(a.xMax, b.xMax);
		int yMax = Math.Max(a.yMax, b.yMax);
		int zMax = Math.Max(a.zMax, b.zMax);
		return new BoundsInt(xMin, yMin, zMin, xMax - xMin, yMax - yMin, zMax - zMin);
	}

	/// <summary>
	/// Try to find a spawn world position on a walkable tile that is at least minDistance cells away from playerWorldPos.
	/// minDistance is measured in tile units (euclidean distance on cell coordinates).
	/// </summary>
	public bool TryGetSpawnPositionAwayFrom(Vector3 playerWorldPos, int minDistanceInCells, out Vector3 worldPos)
	{
		worldPos = Vector3.zero;
		if (walkableCells == null || walkableCells.Count ==0)
			return false;

		// determine reference tilemap for converting cell->world. Prefer NonCollision if available
		Tilemap refMap = NonCollisionTilemap != null ? NonCollisionTilemap : CollisionTilemap;
		if (refMap == null) return false;

		Vector3Int playerCell = refMap.WorldToCell(playerWorldPos);

		// collect candidates
		var candidates = new List<Vector3Int>();
		float minDistSq = minDistanceInCells * minDistanceInCells;
		foreach (var cell in walkableCells)
		{
			float dx = cell.x - playerCell.x;
			float dy = cell.y - playerCell.y;
			float distSq = dx * dx + dy * dy;
			if (distSq >= minDistSq)
				candidates.Add(cell);
		}

		if (candidates.Count ==0)
			return false;

		var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
		worldPos = refMap.GetCellCenterWorld(chosen);
		return true;
	}

	// Update is called once per frame
	void Update()
	{

	}
}
