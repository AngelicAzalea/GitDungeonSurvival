using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// NavService: keeps both the grid-based pathfinder (legacy) and a lightweight node-graph pathfinder.
/// Use GetPathWorld(...) for grid A* (existing). Use GetPathUsingNodes(...) for node-graph (fast).
/// </summary>
public class NavService : MonoBehaviour
{
	public static NavService Instance { get; private set; }

	// exposed for convenience
	public Tilemap CollisionTilemap { get; private set; }
	public Tilemap NonCollisionTilemap { get; private set; }

	private bool[,] grid;
	private Vector3Int origin;
	private int w, h;
	// node graph (new, built from TileGeneratorScript)
	private List<NavNode> nodes;

	// path cache: key = "sx:sy:tx:ty:simplify" value = path as list of cells
	private readonly Dictionary<string, List<Vector3Int>> pathCache = new Dictionary<string, List<Vector3Int>>();

	private readonly object cacheLock = new object();

	void Awake()
	{
		if (Instance == null) Instance = this;
		else Destroy(this);
	}

	// Build from tilemaps (preferred). Also clears cache.
	public void BuildGridFromTilemaps(Tilemap collision, Tilemap nonCollision)
	{
		CollisionTilemap = collision;
		NonCollisionTilemap = nonCollision;
		if (collision == null)
		{
			grid = null;
			origin = Vector3Int.zero;
			w = h = 0;
			return;
		}

		var bounds = collision.cellBounds;
		if (nonCollision != null)
		{
			var other = nonCollision.cellBounds;
			int minX = Math.Min(bounds.xMin, other.xMin);
			int minY = Math.Min(bounds.yMin, other.yMin);
			int maxX = Math.Max(bounds.xMax, other.xMax);
			int maxY = Math.Max(bounds.yMax, other.yMax);
			bounds = new BoundsInt(minX, minY, 0, maxX - minX, maxY - minY, 1);
		}

		origin = new Vector3Int(bounds.xMin, bounds.yMin, 0);
		w = bounds.size.x;
		h = bounds.size.y;
		if (w <= 0 || h <= 0) { grid = null; return; }

		grid = new bool[w, h];
		for (int x = 0; x < w; x++)
			for (int y = 0; y < h; y++)
			{
				var cell = new Vector3Int(origin.x + x, origin.y + y, 0);
				bool blocked = collision.GetTile(cell) != null;
				bool hasGround = nonCollision == null || nonCollision.GetTile(cell) != null;
				grid[x, y] = !blocked && hasGround;
			}

		ClearCache();
	}

	// Try get grid for other systems
	public bool TryGetGrid(out bool[,] outGrid, out Vector3Int outOrigin)
	{
		outGrid = grid;
		outOrigin = origin;
		return grid != null;
	}

	public void ClearCache()
	{
		lock (cacheLock) pathCache.Clear();
	}

	public void SetNodeGraph(List<NavNode> nodeGraph)
	{
		nodes = nodeGraph;
	}

	public void ClearNodeGraph()
	{
		nodes = null;
	}
	/// <summary>
	/// Fast node-graph path. Returns null if no path found.
	/// Steps:
	/// - If direct LOS (start->target) and target cell is reachable, return direct waypoint.
	/// - Otherwise find nearest reachable node to start and target and run A* on nodes.
	/// </summary>
	public List<Vector3> GetPathUsingNodes(Vector3 startWorld, Vector3 targetWorld)
	{
		if (nodes == null || nodes.Count == 0 || CollisionTilemap == null) return null;

		// quick direct check (world to world)
		if (HasLineOfSightWorld(startWorld, targetWorld))
			return new List<Vector3> { targetWorld };

		int startNode = GetNearestVisibleNodeIndex(startWorld);
		int targetNode = GetNearestVisibleNodeIndex(targetWorld);

		if (startNode == -1 || targetNode == -1) return null;
		if (startNode == targetNode) return new List<Vector3> { nodes[startNode].worldPos, targetWorld };

		var nodePath = AStarNodes(startNode, targetNode);
		if (nodePath == null || nodePath.Count == 0) return null;

		// convert to world positions and append final player target for accuracy
		var worldPath = new List<Vector3>(nodePath.Count + 1);
		foreach (var idx in nodePath) worldPath.Add(nodes[idx].worldPos);
		worldPath.Add(targetWorld);
		return worldPath;
	}

	// Find nearest node index that is visible (line-of-sight) from worldPos.
	private int GetNearestVisibleNodeIndex(Vector3 worldPos)
	{
		if (nodes == null || nodes.Count == 0) return -1;
		int best = -1;
		float bestDistSqr = float.MaxValue;
		for (int i = 0; i < nodes.Count; i++)
		{
			// prefer nodes that have direct LOS from worldPos (i.e., start can see node)
			if (!HasLineOfSightWorld(worldPos, nodes[i].worldPos)) continue;
			float d = (nodes[i].worldPos - worldPos).sqrMagnitude;
			if (d < bestDistSqr) { bestDistSqr = d; best = i; }
		}

		// If no visible node found, fallback to nearest node regardless of LOS
		if (best == -1)
		{
			for (int i = 0; i < nodes.Count; i++)
			{
				float d = (nodes[i].worldPos - worldPos).sqrMagnitude;
				if (d < bestDistSqr) { bestDistSqr = d; best = i; }
			}
		}

		return best;
	}

	// Check line-of-sight between two world positions by stepping through cells and checking collision tilemap.
	private bool HasLineOfSightWorld(Vector3 aWorld, Vector3 bWorld)
	{
		if (CollisionTilemap == null) return false;
		Vector3Int aCell = CollisionTilemap.WorldToCell(aWorld);
		Vector3Int bCell = CollisionTilemap.WorldToCell(bWorld);
		return LineOfSightCells(aCell, bCell);
	}

	// Bresenham line between two cells; returns true if all cells along line are walkable (no collision tile)
	private bool LineOfSightCells(Vector3Int a, Vector3Int b)
	{
		int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;

		int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
		int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
		int err = dx + dy;
		while (true)
		{
			var cell = new Vector3Int(x0, y0, 0);
			if (CollisionTilemap.GetTile(cell) != null) return false;
			if (x0 == x1 && y0 == y1) break;
			int e2 = 2 * err;
			if (e2 >= dy) { err += dy; x0 += sx; }
			if (e2 <= dx) { err += dx; y0 += sy; }
		}
		return true;
	}

	// A* on node graph. Returns list of node indices (including start & target).
	private List<int> AStarNodes(int startIdx, int targetIdx)
	{
		int n = nodes.Count;
		var g = new float[n];
		var f = new float[n];
		var from = new int[n];
		var closed = new byte[n];
		for (int i = 0; i < n; i++) { g[i] = float.MaxValue; f[i] = float.MaxValue; from[i] = -1; closed[i] = 0; }

		var open = new BinaryHeap();
		g[startIdx] = 0f;
		f[startIdx] = HeuristicNodeCost(startIdx, targetIdx);
		open.Push(startIdx, (int)(f[startIdx] * 1000f));

		while (open.Count > 0)
		{
			int current = open.Pop();
			if (current == targetIdx) return ReconstructNodePath(from, current);

			closed[current] = 1;
			foreach (var nb in nodes[current].neighbors)
			{
				if (closed[nb] != 0) continue;
				float tentativeG = g[current] + Vector3.Distance(nodes[current].worldPos, nodes[nb].worldPos);
				if (tentativeG < g[nb])
				{
					from[nb] = current;
					g[nb] = tentativeG;
					f[nb] = tentativeG + HeuristicNodeCost(nb, targetIdx);
					open.Push(nb, (int)(f[nb] * 1000f));
				}
			}
		}

		return new List<int>(); // no path
	}

	private float HeuristicNodeCost(int aIdx, int bIdx)
	{
		return Vector3.Distance(nodes[aIdx].worldPos, nodes[bIdx].worldPos);
	}

	private List<int> ReconstructNodePath(int[] from, int current)
	{
		var path = new List<int>();
		while (current != -1)
		{
			path.Add(current);
			current = from[current];
		}
		path.Reverse();
		return path;
	}


	// Public api: get world-space path. Returns null if no path.
	// simplifyStep =1 (no simplification). >1 will downsample path for distant agents.
	public List<Vector3> GetPathWorld(Vector3 startWorld, Vector3 targetWorld, int simplifyStep = 1, float cellCenterOffset = 0.5f)
	{
		if (grid == null || CollisionTilemap == null) return null;

		Vector3Int startCell = CollisionTilemap.WorldToCell(startWorld);
		Vector3Int targetCell = CollisionTilemap.WorldToCell(targetWorld);

		var sIdx = CellToIndex(startCell);
		var tIdx = CellToIndex(targetCell);
		if (!IndexInBounds(sIdx) || !IndexInBounds(tIdx)) return null;

		if (Vector3.Distance(CollisionTilemap.CellToWorld(targetCell), startWorld) <= 0.6f)
		{
			return new List<Vector3> { CollisionTilemap.CellToWorld(targetCell) + (Vector3)CollisionTilemap.cellSize * cellCenterOffset };
		}

		string key = PathKey(sIdx, tIdx, simplifyStep);

		List<Vector3Int> cellPath = null;
		lock (cacheLock)
		{
			if (pathCache.TryGetValue(key, out var cached))
				cellPath = new List<Vector3Int>(cached);
		}

		if (cellPath == null)
		{
			var indicesPath = AStarSearch(sIdx, tIdx);
			if (indicesPath == null || indicesPath.Count == 0) return null;
			var fullCellPath = ConvertIndicesToCells(indicesPath);

			if (simplifyStep > 1)
			{
				var simplified = new List<Vector3Int>();
				for (int i = 0; i < fullCellPath.Count; i += simplifyStep)
					simplified.Add(fullCellPath[i]);
				if (simplified.Count == 0 || simplified[simplified.Count - 1] != fullCellPath[fullCellPath.Count - 1])
					simplified.Add(fullCellPath[fullCellPath.Count - 1]);
				cellPath = simplified;
			}
			else
			{
				cellPath = fullCellPath;
			}

			lock (cacheLock)
			{
				if (cellPath.Count <= 512)
					pathCache[key] = new List<Vector3Int>(cellPath);
			}
		}

		var worldPath = new List<Vector3>(cellPath.Count);
		foreach (var c in cellPath)
			worldPath.Add(CollisionTilemap.CellToWorld(c) + (Vector3)CollisionTilemap.cellSize * cellCenterOffset);

		return worldPath;
	}

	private string PathKey((int x, int y) s, (int x, int y) t, int simplify)
	{
		return s.x + ":" + s.y + ":" + t.x + ":" + t.y + ":" + simplify;
	}

	// --- A* implementation using binary heap priority queue for speed ---
	private List<int> AStarSearch((int x, int y) start, (int x, int y) target)
	{
		int sx = start.x, sy = start.y;
		int tx = target.x, ty = target.y;

		int size = w * h;
		var gScore = new int[size];
		var fScore = new int[size];
		var cameFrom = new int[size];
		var closed = new byte[size];

		for (int i = 0; i < size; i++) { gScore[i] = int.MaxValue; fScore[i] = int.MaxValue; cameFrom[i] = -1; closed[i] = 0; }

		int startIdx = sy * w + sx;
		int targetIdx = ty * w + tx;

		var open = new BinaryHeap();
		gScore[startIdx] = 0;
		fScore[startIdx] = Heuristic(sx, sy, tx, ty);
		open.Push(startIdx, fScore[startIdx]);

		int[] dirX = { 1, -1, 0, 0 };
		int[] dirY = { 0, 0, 1, -1 };

		while (open.Count > 0)
		{
			int current = open.Pop();
			if (current == targetIdx) return ReconstructPath(cameFrom, current);

			closed[current] = 1;
			int cx = current % w;
			int cy = current / w;

			for (int d = 0; d < 4; d++)
			{
				int nx = cx + dirX[d], ny = cy + dirY[d];
				if (!IndexInBounds((nx, ny))) continue;
				int neighbor = ny * w + nx;
				if (closed[neighbor] != 0) continue;
				if (!grid[nx, ny]) continue;

				int tentativeG = gScore[current] + 1;
				if (tentativeG < gScore[neighbor])
				{
					cameFrom[neighbor] = current;
					gScore[neighbor] = tentativeG;
					int newF = tentativeG + Heuristic(nx, ny, tx, ty);
					fScore[neighbor] = newF;
					open.Push(neighbor, newF);
				}
			}
		}

		return new List<int>(); // no path
	}

	private List<int> ReconstructPath(int[] cameFrom, int current)
	{
		var path = new List<int>();
		while (current != -1)
		{
			path.Add(current);
			current = cameFrom[current];
		}
		path.Reverse();
		return path;
	}

	private List<Vector3Int> ConvertIndicesToCells(List<int> indices)
	{
		var outCells = new List<Vector3Int>(indices.Count);
		foreach (var idx in indices)
		{
			int x = idx % w;
			int y = idx / w;
			outCells.Add(new Vector3Int(origin.x + x, origin.y + y, 0));
		}
		return outCells;
	}

	private (int x, int y) CellToIndex(Vector3Int cell) => (cell.x - origin.x, cell.y - origin.y);
	private bool IndexInBounds((int x, int y) idx) => idx.x >= 0 && idx.x < w && idx.y >= 0 && idx.y < h;
	private int Heuristic(int x, int y, int tx, int ty) => Mathf.Abs(x - tx) + Mathf.Abs(y - ty);

	// ----- small binary heap (min-heap) for int priorities -----
	private class BinaryHeap
	{
		private List<int> items = new List<int>();
		private List<int> priorities = new List<int>();
		private Dictionary<int, int> indexOf = new Dictionary<int, int>();

		public int Count => items.Count;

		public void Push(int item, int priority)
		{
			if (indexOf.TryGetValue(item, out var idx))
			{
				if (priority < priorities[idx])
				{
					priorities[idx] = priority;
					SiftUp(idx);
				}
				return;
			}

			indexOf[item] = items.Count;
			items.Add(item);
			priorities.Add(priority);
			SiftUp(items.Count - 1);
		}

		public int Pop()
		{
			int result = items[0];
			Swap(0, items.Count - 1);
			items.RemoveAt(items.Count - 1);
			priorities.RemoveAt(priorities.Count - 1);
			indexOf.Remove(result);
			if (items.Count > 0) SiftDown(0);
			return result;
		}

		private void SiftUp(int i)
		{
			while (i > 0)
			{
				int p = (i - 1) / 2;
				if (priorities[i] >= priorities[p]) break;
				Swap(i, p);
				i = p;
			}
		}

		private void SiftDown(int i)
		{
			while (true)
			{
				int l = i * 2 + 1;
				int r = i * 2 + 2;
				int smallest = i;
				if (l < items.Count && priorities[l] < priorities[smallest]) smallest = l;
				if (r < items.Count && priorities[r] < priorities[smallest]) smallest = r;
				if (smallest == i) break;
				Swap(i, smallest);
				i = smallest;
			}
		}

		private void Swap(int a, int b)
		{
			int ia = items[a], ib = items[b];
			int pa = priorities[a], pb = priorities[b];

			items[a] = ib; items[b] = ia;
			priorities[a] = pb; priorities[b] = pa;

			indexOf[items[a]] = a;
			indexOf[items[b]] = b;
		}

	}

	public List<NavNode> GetNodeGraph()
	{
		return nodes;
	}
}
