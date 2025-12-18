using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RunHUDController : MonoBehaviour
{
 [SerializeField] private StatSystem statSystem;
 [SerializeField] private GameObject entryPrefab; // prefab with Image+Text
 [SerializeField] private Transform listParent;

 private List<GameObject> entries = new List<GameObject>();

 private void Awake()
 {
 if (statSystem == null) statSystem = FindObjectOfType<StatSystem>();
 }

 private void OnEnable()
 {
 if (statSystem != null) statSystem.OnInventoryChanged += Rebuild;
 Rebuild();
 }

 private void OnDisable()
 {
 if (statSystem != null) statSystem.OnInventoryChanged -= Rebuild;
 }

 public void Rebuild()
 {
 // clear
 foreach(var e in entries) Destroy(e);
 entries.Clear();

 if (statSystem == null || listParent == null) return;
 var inv = statSystem == null ? null : statSystem;
 // get ItemInventory via reflection? better to expose runInventory but keep simple: use Resources
 var runInventory = Resources.Load<ItemInventory>("RunInventory");
 if (runInventory == null) return;

 foreach(var item in runInventory.items)
 {
 if (item == null) continue;
 var go = Instantiate(entryPrefab, listParent);
 var img = go.GetComponentInChildren<Image>();
 var txt = go.GetComponentInChildren<Text>();
 if (img != null) img.sprite = item.icon;
 if (txt != null) txt.text = item.itemName;
 entries.Add(go);
 }
 }
}
