using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CardHolder : MonoBehaviour
{
    public GameObject Deck;
    public List<GameObject> cards = new List<GameObject>();
    public int maxCardKol= 7;

    public TMP_Text kolCardsTxt;


    private void Start()
    {
        kolCardsTxt.text = "cards " + (cards.Count) + "/" + maxCardKol;
    }

    public void newCard(GameObject cardPref)
    {
        if (cards.Count < maxCardKol)
        {
            GameObject card = Instantiate(cardPref);
            cards.Add(card);
            
            cards[cards.Count - 1].transform.position = Deck.transform.position;
            CardsKolChange();
        }
    }

    public void CardsKolChange()
    {
        kolCardsTxt.text = "cards " + (cards.Count) + "/" + maxCardKol;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards.Count % 2 == 0) {
                cards[i].GetComponent<CardMoving>().startPos = new Vector2(transform.position.x + (i - (cards.Count / 2)) * 1.7f + 0.85f, transform.position.y);
            }
            else
            {
                cards[i].GetComponent<CardMoving>().startPos = new Vector2(transform.position.x + (i - (cards.Count / 2)) * 1.7f, transform.position.y);
            }
        }
    }

    private void Update()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            GameObject card = cards[i];
            if (card.activeSelf == false)
            {
                Destroy(card);
                cards.Remove(card);
                CardsKolChange();
            }
        }
    }
}
