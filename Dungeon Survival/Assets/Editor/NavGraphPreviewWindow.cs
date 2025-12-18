using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window to preview the Nav node graph in the Scene view.
/// - Shows nodes as discs (customizable color/radius) and edges as lines.
/// - Offers Refresh and Build (calls selected TileGeneratorScript.BuildAndPushNodeGraph()).
/// </summary>

public class NavGraphPreviewWindow : EditorWindow
{
	private Color nodeColor = new Color(0.0f, 0.7f, 1.0f, 0.9f);
	private Color edgeColor = new Color(1f, 0.6f, 0.0f, 0.9f);
	private Color labelColor = Color.white;
	private float nodeRadius = 0.25f;
	private bool drawLabels = true;
	private bool drawEdges = true;
	private bool autoRefreshScene = true;

	[MenuItem("Window/Dungeon/Nav Graph Preview")]
	public static void ShowWindow()
	{
		var w = GetWindow<NavGraphPreviewWindow>("Nav Graph Preview");
		w.minSize = new Vector2(260, 120);
	}

	private void OnEnable()
	{
		SceneView.duringSceneGui += OnSceneGUI;
	}

	private void OnDisable()
	{
		SceneView.duringSceneGui -= OnSceneGUI;
	}

	private void OnGUI()
	{
		EditorGUILayout.LabelField("Nav Graph Preview", EditorStyles.boldLabel);
		nodeColor = EditorGUILayout.ColorField("Node Color", nodeColor);
		nodeRadius = EditorGUILayout.FloatField("Node Radius", nodeRadius);
		drawEdges = EditorGUILayout.Toggle("Draw Edges", drawEdges);
		edgeColor = EditorGUILayout.ColorField("Edge Color", edgeColor);
		drawLabels = EditorGUILayout.Toggle("Draw Labels", drawLabels);
		labelColor = EditorGUILayout.ColorField("Label Color", labelColor);
		autoRefreshScene = EditorGUILayout.Toggle("Auto Refresh Scene Overlay", autoRefreshScene);

		GUILayout.Space(8);

		using (new EditorGUILayout.HorizontalScope())
		{
			if (GUILayout.Button("Refresh"))
			{
				SceneView.RepaintAll();
			}
			if (GUILayout.Button("Build Node Graph (selected TileGenerator)"))
			{
				BuildNodeGraphFromSelection();
			}
			if (GUILayout.Button("Ping NavService"))
			{
				if (NavService.Instance != null)
					EditorGUIUtility.PingObject(NavService.Instance.gameObject);
				else
					Debug.LogWarning("NavService.Instance is null in editor.");
			}
		}

		GUILayout.FlexibleSpace();
		EditorGUILayout.LabelField("Tip: open Scene view to see overlay. Toggle Auto Refresh for real-time updates.", EditorStyles.miniLabel);
	}

	private void BuildNodeGraphFromSelection()
	{

	}

	private void OnSceneGUI(SceneView sv)
	{
		if (!autoRefreshScene && Event.current.type != EventType.Repaint) return;

		if (NavService.Instance == null) return;
		var nodes = NavService.Instance.GetNodeGraph();
		if (nodes == null || nodes.Count == 0) return;

		Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

		// Draw edges
		if (drawEdges)
		{
			Handles.color = edgeColor;
			foreach (var n in nodes)
			{
				foreach (var nb in n.neighbors)
				{
					if (nb < 0 || nb >= nodes.Count) continue;
					Handles.DrawLine(n.worldPos, nodes[nb].worldPos);
				}
			}
		}

		// Draw nodes
		Handles.color = nodeColor;
		foreach (var n in nodes)
		{
			Handles.DrawSolidDisc(n.worldPos, Vector3.forward, nodeRadius);
		}

		if (drawLabels)
		{
			GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = labelColor } };
			foreach (var n in nodes)
			{
				Handles.Label(n.worldPos + Vector3.up * (nodeRadius + 0.05f), $"#{n.id}", style);
			}
		}

		// Ensure scene updates when auto-refresh is enabled
		if (autoRefreshScene) sv.Repaint();
	}
}
