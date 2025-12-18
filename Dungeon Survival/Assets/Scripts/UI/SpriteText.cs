using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SpriteText : MonoBehaviour
{
 public SpriteFont font;
 public string text = "HELLO";
 public float spacing =0.0f; // extra spacing between characters (units)
 public float charSize =32f; // pixel size for each char sprite
 public bool center = true;

 private List<GameObject> charObjects = new List<GameObject>();

 public void Refresh()
 {
 // clear old
 for (int i =0; i < charObjects.Count; i++)
 {
 if (Application.isPlaying) Destroy(charObjects[i]); else DestroyImmediate(charObjects[i]);
 }
 charObjects.Clear();

 if (font == null || string.IsNullOrEmpty(text)) return;

 float x =0f;
 for (int i =0; i < text.Length; i++)
 {
 char c = text[i];
 Sprite s = font.GetSpriteFor(c);
 if (s == null) {
 x += charSize + spacing; // skip width
 continue;
 }
 var go = new GameObject("ch_" + c);
 go.transform.SetParent(this.transform, false);
 var img = go.AddComponent<UnityEngine.UI.Image>();
 img.sprite = s;
 img.preserveAspect = true;
 var rt = go.GetComponent<RectTransform>();
 rt.sizeDelta = new Vector2(charSize, charSize);
 rt.anchoredPosition = new Vector2(x + charSize *0.5f,0);
 charObjects.Add(go);
 x += charSize + spacing;
 }

 // center
 if (center && charObjects.Count >0)
 {
 float totalW = charObjects.Count * (charSize + spacing) - spacing;
 foreach (var go in charObjects)
 {
 var rt = go.GetComponent<RectTransform>();
 rt.anchoredPosition -= new Vector2(totalW *0.5f,0);
 }
 }
 }

 private void OnValidate()
 {
 Refresh();
 }

 private void Awake()
 {
 Refresh();
 }
}
