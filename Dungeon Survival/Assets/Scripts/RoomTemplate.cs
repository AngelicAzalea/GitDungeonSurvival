using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Dungeon/RoomTemplate")]
public class RoomTemplate : ScriptableObject
{
	public int width;
	public int height;
	// length should be width*height, row-major
	public TileBase[] groundTiles;
	public TileBase[] collisionTiles; // optional, can be null entries

	[Header("Template Options")]
	[Tooltip("If true the template includes outer wall tiles (i.e. full room including walls). If false it's interior only.")]
	public bool hasOuterWalls = false;

	// Rotation/flip options removed - provide separate templates for rotated/flipped variants.
}
