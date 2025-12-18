
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Grid A* Pathfinder component (C# port).
/// Important: this version stores the tilemap union origin so world cell coords (Tilemap.WorldToCell)
/// can be translated into internal indices correctly. Use InitializeFromTilemaps(...) after tilemaps
/// are available (TileGenerator or NavService should call this).
/// </summary>
public class PathfinderComponent : MonoBehaviour
{
	[SerializeField] public Tilemap CollisionTilemap;
	[SerializeField] public Tilemap NonCollisionTilemap;

	// Grid dimensions & storage
	public int m_NumNodes { get; private set; }
	private int m_MapWidth;
	private int m_MapHeight;
	private Vector3Int m_GridOrigin = Vector3Int.zero; // IMPORTANT: world cell coordinate of index 0,0
	private PathNode[] m_Nodes;
	private List<int> m_OpenNodeIndexes = new List<int>();

	private struct PathNode
	{
		public enum PathNodeStatus
		{
			Unchecked,
			Open,
			Closed,
		}

		public int parentNodeIndex;
		public PathNodeStatus status;

		public float LowestCost;
		public float Estimate;
		public float FinalCost;
	}

	// Debug / visualization settings
	[Header("Debug (Gizmos)")]
	public bool debugDrawGrid = false;
	public Color debugGridColor = new Color(0f, 0.8f, 0f, 0.25f);
	public bool debugDrawAllCells = false; // if false, only draw walkable cells
	public bool debugDrawPath = true;
	public Color debugPathColor = Color.yellow;
	[Range(0.05f, 1f)]
	public float debugCellFill = 0.9f;

	// Stores last path computed for visual debugging (indices)
	[NonSerialized] public List<int> debugLastPath = new List<int>();

	void Awake()
	{
		m_NumNodes = 0;
		m_Nodes = Array.Empty<PathNode>();
	}

	/// <summary>
	/// Initialize with explicit width/height and origin cell.
	/// </summary>
	public void Initialize(Tilemap collision, Tilemap nonCollision, Vector3Int gridOrigin, int width, int height)
	{
		CollisionTilemap = collision;
		NonCollisionTilemap = nonCollision;

		m_GridOrigin = gridOrigin;
		m_MapWidth = Math.Max(0, width);
		m_MapHeight = Math.Max(0, height);
		m_NumNodes = m_MapWidth * m_MapHeight;

		if (m_NumNodes > 0)
			m_Nodes = new PathNode[m_NumNodes];
		else
			m_Nodes = Array.Empty<PathNode>();

		Reset();
	}

	/// <summary>
	/// Convenience initializer: compute union bounds of provided tilemaps and initialize using that size/origin.
	/// </summary>
	public void InitializeFromTilemaps(Tilemap collision, Tilemap nonCollision)
	{
		CollisionTilemap = collision;
		NonCollisionTilemap = nonCollision;

		if (CollisionTilemap == null && NonCollisionTilemap == null)
		{
			Initialize(null, null, Vector3Int.zero, 0, 0);
			return;
		}

		var bounds = CollisionTilemap != null ? CollisionTilemap.cellBounds : new BoundsInt();
		if (NonCollisionTilemap != null)
		{
			var other = NonCollisionTilemap.cellBounds;
			if (CollisionTilemap == null) bounds = other;
			else
			{
				int minX = Math.Min(bounds.xMin, other.xMin);
				int minY = Math.Min(bounds.yMin, other.yMin);
				int maxX = Math.Max(bounds.xMax, other.xMax);
				int maxY = Math.Max(bounds.yMax, other.yMax);
				bounds = new BoundsInt(minX, minY, 0, maxX - minX, maxY - minY, 1);
			}
		}

		Vector3Int origin = new Vector3Int(bounds.xMin, bounds.yMin, 0);
		Initialize(collision, nonCollision, origin, bounds.size.x, bounds.size.y);
	}

	public void Reset()
	{
		m_OpenNodeIndexes.Clear();
		if (m_Nodes == null || m_Nodes.Length == 0) return;

		for (int i = 0; i < m_NumNodes; i++)
		{
			m_Nodes[i].parentNodeIndex = -1;
			m_Nodes[i].status = PathNode.PathNodeStatus.Unchecked;
			m_Nodes[i].LowestCost = float.MaxValue;
			m_Nodes[i].Estimate = float.MaxValue;
			m_Nodes[i].FinalCost = float.MaxValue;
		}
	}

	// Convert an absolute tile cell (world cell coords returned by Tilemap.WorldToCell) to internal index.
	// Returns -1 if outside grid.
	private int CellToIndex(Vector3Int cell)
	{
		int rx = cell.x - m_GridOrigin.x;
		int ry = cell.y - m_GridOrigin.y;
		if (rx < 0 || rx >= m_MapWidth || ry < 0 || ry >= m_MapHeight) return -1;
		return ry * m_MapWidth + rx;
	}

	// Convert internal index to absolute tile cell (world cell coords)
	private Vector3Int IndexToCell(int index)
	{
		if (index < 0 || index >= m_NumNodes) return Vector3Int.zero;
		int rx = index % m_MapWidth;
		int ry = index / m_MapWidth;
		return new Vector3Int(m_GridOrigin.x + rx, m_GridOrigin.y + ry, 0);
	}

	// Bounds-check absolute tile cell coordinates
	private bool InBoundsCell(int x, int y)
	{
		return x >= m_GridOrigin.x && x < m_GridOrigin.x + m_MapWidth && y >= m_GridOrigin.y && y < m_GridOrigin.y + m_MapHeight;
	}

	// Determine walkability for an absolute tile cell coordinate
	private bool IsTileWalkableCell(int x, int y)
	{
		if (!InBoundsCell(x, y)) return false;

		if (CollisionTilemap != null)
		{
			if (CollisionTilemap.GetTile(new Vector3Int(x, y, 0)) != null) return false;
		}

		if (NonCollisionTilemap != null)
		{
			if (NonCollisionTilemap.GetTile(new Vector3Int(x, y, 0)) == null) return false;
		}

		return true;
	}

	/// <summary>
	/// Find path using A* on the grid. sx,sy and ex,ey are absolute tile cell coordinates
	/// (what Tilemap.WorldToCell returns).
	/// </summary>
	public bool FindPath(int sx, int sy, int ex, int ey)
	{
		// Convert callers' absolute cell coordinates to indices (and validate)
		int startIndex = CellToIndex(new Vector3Int(sx, sy, 0));
		int endIndex = CellToIndex(new Vector3Int(ex, ey, 0));
		if (m_NumNodes == 0 || startIndex == -1 || endIndex == -1) return false;

		Reset();

		m_Nodes[startIndex].LowestCost = 0f;
		// Manhattan heuristic (using absolute cells)
		int sxr = sx, syr = sy;
		m_Nodes[startIndex].Estimate = Mathf.Abs(sxr - ex) + Mathf.Abs(syr - ey);
		m_Nodes[startIndex].FinalCost = m_Nodes[startIndex].Estimate;
		m_Nodes[startIndex].status = PathNode.PathNodeStatus.Open;
		m_OpenNodeIndexes.Add(startIndex);

		while (m_OpenNodeIndexes.Count != 0)
		{
			// find open node with lowest FinalCost
			float lowestFinal = float.MaxValue;
			int bestIndexInOpenList = 0;
			int currentNodeIndex = m_OpenNodeIndexes[0];

			for (int u = 0; u < m_OpenNodeIndexes.Count; u++)
			{
				int idx = m_OpenNodeIndexes[u];
				if (m_Nodes[idx].FinalCost < lowestFinal)
				{
					lowestFinal = m_Nodes[idx].FinalCost;
					currentNodeIndex = idx;
					bestIndexInOpenList = u;
				}
			}

			m_OpenNodeIndexes.RemoveAt(bestIndexInOpenList);

			if (currentNodeIndex == endIndex)
			{
				// cache debug path for visualization
				debugLastPath = GetPath(ex, ey);
				return true;
			}

			m_Nodes[currentNodeIndex].status = PathNode.PathNodeStatus.Closed;

			// get absolute cell of current node
			Vector3Int currentCell = IndexToCell(currentNodeIndex);
			int currentX = currentCell.x;
			int currentY = currentCell.y;

			// neighbors in absolute cell coords
			var neighborCells = new List<Vector3Int>(4)
			{
				new Vector3Int(currentX - 1, currentY, 0),
				new Vector3Int(currentX + 1, currentY, 0),
				new Vector3Int(currentX, currentY + 1, 0),
				new Vector3Int(currentX, currentY - 1, 0)
			};

			for (int ni = 0; ni < neighborCells.Count; ni++)
			{
				var nc = neighborCells[ni];
				if (!IsTileWalkableCell(nc.x, nc.y)) continue;

				int neighborIndex = CellToIndex(nc);
				if (neighborIndex == -1) continue; // out of grid for some reason

				if (m_Nodes[neighborIndex].status == PathNode.PathNodeStatus.Closed) continue;

				if (m_Nodes[neighborIndex].status != PathNode.PathNodeStatus.Open)
				{
					m_Nodes[neighborIndex].status = PathNode.PathNodeStatus.Open;
					m_OpenNodeIndexes.Add(neighborIndex);
				}

				float newCost = m_Nodes[currentNodeIndex].LowestCost + 1f; // uniform cost
				if (newCost < m_Nodes[neighborIndex].LowestCost)
				{
					m_Nodes[neighborIndex].parentNodeIndex = currentNodeIndex;
					m_Nodes[neighborIndex].LowestCost = newCost;

					int nx = nc.x;
					int ny = nc.y;
					m_Nodes[neighborIndex].Estimate = Mathf.Abs(nx - ex) + Mathf.Abs(ny - ey);
					m_Nodes[neighborIndex].FinalCost = m_Nodes[neighborIndex].LowestCost + m_Nodes[neighborIndex].Estimate;
				}
			}
		}

		debugLastPath.Clear();
		return false;
	}

	/// <summary>
	/// Reconstruct path from absolute end cell (ex,ey). Returns list of internal node indices in order end->start.
	/// </summary>
	public List<int> GetPath(int ex, int ey)
	{
		var path = new List<int>();

		int endIndex = CellToIndex(new Vector3Int(ex, ey, 0));
		if (endIndex == -1 || m_NumNodes == 0) return path;

		int parentIndex = endIndex;
		while (parentIndex >= 0 && parentIndex < m_NumNodes && m_Nodes[parentIndex].parentNodeIndex != -1)
		{
			path.Add(parentIndex);
			parentIndex = m_Nodes[parentIndex].parentNodeIndex;
		}

		return path;
	}

	/// <summary>
	/// Convert an internal index to a world-space center position using a supplied Tilemap (or fallback).
	/// </summary>
	public Vector3 IndexToWorldPos(int index, Tilemap useTilemap = null)
	{
		if (index < 0 || index >= m_NumNodes) return Vector3.zero;
		var cell = IndexToCell(index);
		var map = useTilemap ?? NonCollisionTilemap ?? CollisionTilemap;
		if (map == null) return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
		var world = map.CellToWorld(cell);
		world += (Vector3)map.cellSize * 0.5f;
		return world;
	}

	// ----- Editor / Runtime Gizmo drawing for debugging -----
	private void OnDrawGizmos()
	{
		if (!debugDrawGrid && !debugDrawPath) return;

		var map = NonCollisionTilemap != null ? NonCollisionTilemap : CollisionTilemap;
		if (map == null) return;

		if (debugDrawGrid && m_NumNodes > 0)
		{
			Gizmos.color = debugGridColor;
			Vector3 cellSize = (Vector3)map.cellSize;
			Vector3 half = cellSize * 0.5f * debugCellFill;
			for (int i = 0; i < m_NumNodes; i++)
			{
				Vector3 world = IndexToWorldPos(i, map);
				int rx = i % m_MapWidth;
				int ry = i / m_MapWidth;
				int absX = m_GridOrigin.x + rx;
				int absY = m_GridOrigin.y + ry;

				if (!debugDrawAllCells && !IsTileWalkableCell(absX, absY)) continue;

				Gizmos.DrawCube(world, new Vector3(half.x * 2f, half.y * 2f, Math.Max(0.05f, half.x * 0.25f)));
			}
		}

		if (debugDrawPath && debugLastPath != null && debugLastPath.Count > 0)
		{
			Gizmos.color = debugPathColor;
			for (int i = 0; i < debugLastPath.Count - 1; i++)
			{
				Vector3 a = IndexToWorldPos(debugLastPath[i], map);
				Vector3 b = IndexToWorldPos(debugLastPath[i + 1], map);
				Gizmos.DrawLine(a, b);
				Gizmos.DrawSphere(a, Mathf.Min(map.cellSize.x, map.cellSize.y) * 0.12f);
			}
			Vector3 last = IndexToWorldPos(debugLastPath[debugLastPath.Count - 1], map);
			Gizmos.DrawSphere(last, Mathf.Min(map.cellSize.x, map.cellSize.y) * 0.14f);
		}
	}
}
