using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RunInventory", menuName = "Game/Run Inventory", order =250)]
public class ItemInventory : ScriptableObject
{
 public List<ItemData> items = new List<ItemData>();

 public void AddItem(ItemData item)
 {
 if (item == null) return;
 items.Add(item);
 }

 public void Clear()
 {
 items.Clear();
 }
}
