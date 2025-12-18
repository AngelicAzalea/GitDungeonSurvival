using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class PlayerHealthFillUI : MonoBehaviour
{
	[SerializeField] GameObject heartFillPrefab;

	public void DrawHeartFill(int CurrentHealth, int MaximumHealth)
	{
		//This Script will be called after PlayerHealthContainerUI to fill in the hearts, so we need to calculate how many hearts to fill.
		//We are Filling Containers from Right to left, Meaning that if we have 60 HP, and lose Hp, the heart on the most left should be the first to be emptied.

		//This is the Maximum number of Heart Containers that should already be drawn by PlayerHealthContainerUI
		int numContainers = MaximumHealth / 20;

		//Calculate number of Full Hearts to draw, this needs to round down
		int numFullHearts = CurrentHealth / 20;

		//Calculate remainder health for the partial heart
		int remainderHealth = CurrentHealth % 20;


		foreach (Transform child in transform)
		{
			Destroy(child.gameObject);
		}

		for (int i = 0; i < numContainers; i++)
		{
			if (i < numFullHearts)
			{
				{
					//Full Heart
					GameObject HeartFill = Instantiate(heartFillPrefab, transform);
					HeartFill.transform.SetParent(transform, false);
				}
			}
			else if (i == numFullHearts && remainderHealth > 0)
			{
				{
					//Partial Heart
					GameObject HeartFill = Instantiate(heartFillPrefab, transform);
					HeartFill.transform.SetParent(transform, false);
					Image heartImage = HeartFill.GetComponent<Image>();
					if (heartImage != null)
					{
						heartImage.fillAmount = remainderHealth / 20f;
					}
				}
			}
			else
			{
				{
				   // //Empty Heart (do not fill)
				   // GameObject HeartFill = Instantiate(heartFillPrefab, transform);
				   // HeartFill.transform.SetParent(transform, false);
				   // Image heartImage = HeartFill.GetComponent<Image>();
				   // if (heartImage != null)
				   // {
				   //     heartImage.fillAmount = 0f;
				   // }
				}
			}
		}
	}       
}
