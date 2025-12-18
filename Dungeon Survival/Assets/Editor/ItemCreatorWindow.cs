using UnityEditor;
using UnityEngine;

public class ItemCreatorWindow : EditorWindow
{
 private string id = "";
 private string nameField = "New Item";
 private string description = "";
 private Sprite icon;
 private StatModifier[] modifiers = new StatModifier[0];

 [MenuItem("Tools/Item Creator")]
 public static void Open()
 {
 GetWindow<ItemCreatorWindow>("Item Creator");
 }

 private Vector2 scroll;

 void OnGUI()
 {
 scroll = EditorGUILayout.BeginScrollView(scroll);
 id = EditorGUILayout.TextField("ID", id);
 nameField = EditorGUILayout.TextField("Name", nameField);
 icon = (Sprite)EditorGUILayout.ObjectField("Icon", icon, typeof(Sprite), false);
 description = EditorGUILayout.TextArea(description, GUILayout.Height(60));

 EditorGUILayout.LabelField("Modifiers");
 int newLen = EditorGUILayout.IntField("Count", modifiers.Length);
 if (newLen != modifiers.Length)
 {
 System.Array.Resize(ref modifiers, Mathf.Max(0, newLen));
 }

 for (int i =0; i < modifiers.Length; i++)
 {
 modifiers[i].stat = (StatType)EditorGUILayout.EnumPopup("Stat", modifiers[i].stat);
 modifiers[i].mode = (ModifierMode)EditorGUILayout.EnumPopup("Mode", modifiers[i].mode);
 modifiers[i].value = EditorGUILayout.FloatField("Value", modifiers[i].value);
 EditorGUILayout.Space();
 }

 if (GUILayout.Button("Create Item Asset"))
 {
 CreateItemAsset();
 }

 EditorGUILayout.EndScrollView();
 }

 void CreateItemAsset()
 {
 var item = ScriptableObject.CreateInstance<ItemData>();
 item.id = id;
 item.itemName = nameField;
 item.icon = icon;
 item.description = description;
 item.modifiers = modifiers;

 string path = EditorUtility.SaveFilePanelInProject("Save Item", nameField, "asset", "Save Item Data");
 if (string.IsNullOrEmpty(path)) return;
 AssetDatabase.CreateAsset(item, path);
 AssetDatabase.SaveAssets();
 EditorUtility.FocusProjectWindow();
 Selection.activeObject = item;
 }
}
