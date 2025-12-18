#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;

public static class FormatWithTabs
{
	[MenuItem("Tools/Format C# Files/Convert Leading Spaces to Tabs")] 
	public static void ConvertLeadingSpacesToTabs()
	{
		string assetsPath = Application.dataPath;
		var files = Directory.GetFiles(assetsPath, "*.cs", System.IO.SearchOption.AllDirectories);
		int modified =0;
		foreach (var file in files)
		{
			// skip this formatter to avoid modifying itself while running
			if (file.EndsWith("FormatWithTabs.cs")) continue;

			string[] lines = File.ReadAllLines(file);
			bool changed = false;
			for (int i =0; i < lines.Length; i++)
			{
				string line = lines[i];
				int idx =0;
				int tabCount =0;
				int spaceCount =0;
				// count leading tabs and spaces
				while (idx < line.Length && (line[idx] == '\t' || line[idx] == ' '))
				{
					if (line[idx] == '\t') tabCount++;
					else spaceCount++;
					idx++;
				}

				// total indent in spaces (assume tab ==4 spaces)
				int totalSpaces = tabCount *4 + spaceCount;
				int newTabs = totalSpaces /4;
				int remainder = totalSpaces %4;

				string trimmed = line.Substring(idx);
				string newLeading = new string('\t', newTabs) + new string(' ', remainder);
				string newLine = newLeading + trimmed;
				if (newLine != line)
				{
					lines[i] = newLine;
					changed = true;
				}
			}

			if (changed)
			{
				File.WriteAllLines(file, lines);
				modified++;
			}
		}

		AssetDatabase.Refresh();
		EditorUtility.DisplayDialog("FormatWithTabs", $"Formatted {modified} files to use tabs for indentation.", "OK");
	}
}
#endif
