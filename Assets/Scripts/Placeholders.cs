using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Placeholders : MonoBehaviour
{
    public List<GameObject> placeholders = new List<GameObject>();
    public List<Vector2> placePositions = new List<Vector2>();

    public GameObject placeholderPref;
    void Start()
    {
        foreach (Transform plc in GetComponentInChildren<Transform>())
        {
            placeholders.Add(plc.gameObject);
            placePositions.Add(plc.position);
        }
    }


    public void AddCard(GameObject placeholder, GameObject card)
    {
        int id = 0;
        foreach (GameObject plc in placeholders)
        {
            if (plc == placeholder)
            {
                break;
            }
            id++;
        }
        Destroy(placeholders[id]);
        GameObject crd = Instantiate(card);
        placeholders[id] = crd;
        placeholders[id].transform.SetParent(transform);
        placeholders[id].transform.position = placePositions[id];
    }

    public void DelCard(GameObject card)
    {
        int id = 0;
        foreach (GameObject plc in placeholders)
        {
            if (plc == card)
            {
                break;
            }
            id++;
        }
        Destroy(placeholders[id]);
        GameObject crd = Instantiate(placeholderPref);
        placeholders[id] = crd;
        placeholders[id].transform.SetParent(transform);
        placeholders[id].transform.position = placePositions[id];
    }
}
