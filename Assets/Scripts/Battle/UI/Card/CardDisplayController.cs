using UnityEngine;

public class CardDisplayController : MonoBehaviour
{
    public GameObject cardSheetPrefab; // CardSheetPrefab をアサイン
    public Transform displayRoot;      // 表示先（=このパネル内）

    private GameObject currentSheet;

    public void ShowCard(CardData card)
    {
        HideCard();

        currentSheet = Instantiate(cardSheetPrefab, displayRoot);
        var sheetDisplay = currentSheet.GetComponent<CardSheetDisplay>();
        sheetDisplay.Setup(card);
    }

    public void HideCard()
    {
        if (currentSheet != null)
        {
            Destroy(currentSheet);
            currentSheet = null;
        }
    }
}