using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<GameObject> deck;

    private void Awake()
    {
        // Старые сцены могли хранить null вместо пустого списка.
        if (deck == null)
            deck = new List<GameObject>();
    }

    public void Shuffle()
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            GameObject temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
    }

    /// <summary>
    /// Добор сверху перемешанной колоды. Именно такую модель использует
    /// симуляционный слой AI, поэтому игра и поиск не расходятся.
    /// </summary>
    public GameObject RandCard()
    {
        if (deck == null || deck.Count == 0)
            return null;

        GameObject nextCard = deck[0];
        deck.RemoveAt(0);
        return nextCard;
    }
}
