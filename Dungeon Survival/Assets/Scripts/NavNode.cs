using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small navigation node used by node-graph pathfinding.
/// </summary>
public class NavNode
{
	public int id;
	public Vector3Int cell;    // grid cell coordinate (tilemap cell)
	public Vector3 worldPos;   // world position (cell origin + center offset)
	public List<int> neighbors = new List<int>();

	public NavNode(int id, Vector3Int cell, Vector3 worldPos)
	{
		this.id = id;
		this.cell = cell;
		this.worldPos = worldPos;
	}
}
