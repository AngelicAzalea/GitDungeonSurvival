using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Simple UI controller to display end-run summary and post to highscores
public class RunEndSummaryController : MonoBehaviour
{
 [Header("UI Refs")]
 public Text scoreText;
 public Text timeText;
 public Transform itemsParent;
 public GameObject itemEntryPrefab; // simple prefab with Image + Text
 public Button submitButton;
 public Button backToMenuButton;

 private void OnEnable()
 {
 // subscribe to GameManager end run
 if (GameManager.Instance != null)
 {
 GameManager.Instance.OnRunEnded += OnRunEnded;
 }
 }

 private void OnDisable()
 {
 if (GameManager.Instance != null)
 {
 GameManager.Instance.OnRunEnded -= OnRunEnded;
 }
 }

 private void OnRunEnded()
 {
 // populate UI from GameManager
 if (GameManager.Instance == null) return;
 int score = GameManager.Instance.Score;
 float time = GameManager.Instance.RunElapsed;
 scoreText.text = score.ToString();
 timeText.text = System.TimeSpan.FromSeconds(time).ToString(@"hh\:mm\:ss");

 // items: attempt to read run inventory (Resources fallback)
 var runInv = Resources.Load<ItemInventory>("RunInventory");
 foreach (Transform t in itemsParent) Destroy(t.gameObject);
 if (runInv != null)
 {
 foreach (var item in runInv.items)
 {
 if (item == null) continue;
 var go = Instantiate(itemEntryPrefab, itemsParent);
 var txt = go.GetComponentInChildren<Text>();
 if (txt != null) txt.text = item.itemName;
 }
 }
 }

 public void SubmitScore()
 {
 if (HighscoreManager.Instance == null) return;
 var runInv = Resources.Load<ItemInventory>("RunInventory");
 List<string> names = new List<string>();
 if (runInv != null)
 {
 foreach (var it in runInv.items) if (it != null) names.Add(it.itemName);
 }
 HighscoreManager.Instance.AddEntry(GameManager.Instance.Score, GameManager.Instance.RunElapsed, names);
 }

 public void BackToMenu()
 {
 if (GameManager.Instance != null) GameManager.Instance.GoToMenu();
 }
}
