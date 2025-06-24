using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<GameObject> deck;
    public GameObject RandCard()
    {
        int i = Random.Range(0, deck.Count);
        GameObject nextCard = deck[i];
        deck.RemoveAt(i);
        
        return nextCard;
    }
}
