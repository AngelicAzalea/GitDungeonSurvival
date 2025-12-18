using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System; // add Math

public class RoomTemplateCreator : EditorWindow
{
	private Tilemap groundTilemap;
	private Tilemap collisionTilemap;
	private string assetName = "RoomTemplate";
	private string assetFolder = "Assets/RoomTemplates";

	[MenuItem("Tools/Dungeon/Room Template Creator...")]
	public static void ShowWindow()
	{
		var wnd = GetWindow<RoomTemplateCreator>("Room Template Creator");
		wnd.minSize = new Vector2(400,160);
	}

	void OnGUI()
	{
		EditorGUILayout.LabelField("Create a RoomTemplate asset from painted Tilemaps", EditorStyles.boldLabel);
		EditorGUILayout.Space();

		groundTilemap = (Tilemap)EditorGUILayout.ObjectField("Ground Tilemap (NonCollision)", groundTilemap, typeof(Tilemap), true);
		collisionTilemap = (Tilemap)EditorGUILayout.ObjectField("Collision Tilemap (optional)", collisionTilemap, typeof(Tilemap), true);

		EditorGUILayout.Space();
		assetName = EditorGUILayout.TextField("Asset Name", assetName);
		assetFolder = EditorGUILayout.TextField("Asset Folder", assetFolder);

		EditorGUILayout.Space();
		EditorGUILayout.HelpBox("The tool will compute the union bounds of the provided tilemaps and export their tiles into a RoomTemplate asset. The arrays are row-major: index = y*width + x.", MessageType.Info);

		EditorGUI.BeginDisabledGroup(groundTilemap == null && collisionTilemap == null);
	if (GUILayout.Button("Create RoomTemplate"))
	{
		CreateTemplate();
	}
		EditorGUI.EndDisabledGroup();
	}

	private void CreateTemplate()
	{
		if (groundTilemap == null && collisionTilemap == null)
		{
		EditorUtility.DisplayDialog("Room Template Creator", "Please assign at least one Tilemap (ground or collision).", "OK");
		 return;
		}

	// Determine union bounds
	BoundsInt bounds = new BoundsInt();
	bool first = true;
		if (groundTilemap != null)
		{
			var b = groundTilemap.cellBounds;
			if (first) { bounds = b; first = false; } else bounds = Union(bounds, b);
		}
		if (collisionTilemap != null)
		{
			var b = collisionTilemap.cellBounds;
			if (first) { bounds = b; first = false; } else bounds = Union(bounds, b);
		}

	if (bounds.size.x <=0 || bounds.size.y <=0)
	{
		EditorUtility.DisplayDialog("Room Template Creator", "Computed bounds are empty. Make sure tilemaps contain painted tiles.", "OK");
		return;
	}

	int width = bounds.size.x;
	int height = bounds.size.y;

	var template = ScriptableObject.CreateInstance<RoomTemplate>();
	template.width = width;
	template.height = height;
	template.groundTiles = new TileBase[width * height];
	template.collisionTiles = new TileBase[width * height];

	for (int y =0; y < height; y++)
	{
		for (int x =0; x < width; x++)
		{
			int idx = y * width + x;
			var cell = new Vector3Int(bounds.xMin + x, bounds.yMin + y,0);
			if (groundTilemap != null)
			{
				template.groundTiles[idx] = groundTilemap.GetTile(cell);
			}
			else
			{
				template.groundTiles[idx] = null;
			}

			if (collisionTilemap != null)
			{
				template.collisionTiles[idx] = collisionTilemap.GetTile(cell);
			}
			else
			{
				template.collisionTiles[idx] = null;
			}
		}
	}

 // Ensure folder exists
	if (!AssetDatabase.IsValidFolder(assetFolder))
	{
		Directory.CreateDirectory(Path.GetFullPath(assetFolder));
		AssetDatabase.Refresh();
	}

		string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(assetFolder, assetName + ".asset"));
		AssetDatabase.CreateAsset(template, path);
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();

		EditorUtility.DisplayDialog("Room Template Creator", $"RoomTemplate created at '{path}' (size {width}x{height}).", "OK");
		Selection.activeObject = template;
	}

	private static BoundsInt Union(BoundsInt a, BoundsInt b)
	{
		int minX = Math.Min(a.xMin, b.xMin);
		int minY = Math.Min(a.yMin, b.yMin);
		int maxX = Math.Max(a.xMax, b.xMax);
		int maxY = Math.Max(a.yMax, b.yMax);
		return new BoundsInt(minX, minY,0, maxX - minX, maxY - minY,1);
	}
}
