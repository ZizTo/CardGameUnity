using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;

public class CardMoving : MonoBehaviour
{
    bool isOnMouse = false, isDragged = false, therePlaceholder = false, placed = false;
    public Vector2 startPos = new Vector2(0f, 0f);
    Vector2 placeholderPos = new Vector2(0f, 0f);
    GameObject placeholder;
    
    public GameObject tableCardPref;
    public float velocity = 10f;

    public TMP_Text manaT, dmgT, hpT, nameT, DescT;
    private void Start()
    {
        if (manaT != null)
        {
            manaT.text = "" + tableCardPref.GetComponent<CardOnTable>().Mana;
            dmgT.text = "" + tableCardPref.GetComponent<CardOnTable>().Damage;
            hpT.text = "" + tableCardPref.GetComponent<CardOnTable>().HP;
            nameT.text = tableCardPref.GetComponent<CardOnTable>().Name;
            DescT.text = tableCardPref.GetComponent<CardOnTable>().Desc;
        }
    }

    private void OnMouseDown()
    {
        if (isOnMouse && Camera.main.GetComponent<Order>().yourOrd && transform.CompareTag("Card"))
        {
            isDragged = true;
        }
    }

    private void OnMouseEnter()
    {
        if (transform.CompareTag("Card"))
        {
            isOnMouse = true;
        }
    }

    private void OnMouseUp()
    {
        isDragged = false;
        if (therePlaceholder && tableCardPref.GetComponent<CardOnTable>().Mana <= Camera.main.GetComponent<Order>().manaYou) { 
            placed = true; 
            Camera.main.GetComponent<Order>().ChangeManaYou(-tableCardPref.GetComponent<CardOnTable>().Mana);
        }
    }

    private void OnMouseExit()
    {
        isOnMouse = false;
    }

    private void Update()
    {
        Vector2 nextPos;
        if (placed) { 
            nextPos = placeholderPos; 

            if (Mathf.Sqrt((transform.position.x - placeholderPos.x) * (transform.position.x - placeholderPos.x)
                + (transform.position.y - placeholderPos.y) * (transform.position.y - placeholderPos.y)) < 0.001f) {
                placeholder.transform.parent.GetComponent<Placeholders>().AddCard(placeholder, tableCardPref);
                gameObject.SetActive(false);
            }
        }
        else if (isOnMouse && !isDragged)
        {
            nextPos = new Vector2(startPos.x, startPos.y + 0.5f);
        }
        else if (isDragged)
        {
            bool tryingFindPlaceh = false;
            placeholderPos = new Vector2(0,0);
            Collider2D[] colliders = Physics2D.OverlapPointAll(Camera.main.ScreenPointToRay(Input.mousePosition).origin);
            foreach (Collider2D collider in colliders)
            {
                if (collider.gameObject.CompareTag("Placeholder") && collider.transform.parent.CompareTag("PlsYou")) { 
                    tryingFindPlaceh = true; 
                    placeholderPos = collider.transform.position;
                    placeholder = collider.gameObject;
                }
            }
            if (tryingFindPlaceh) { therePlaceholder = true; }
            else { therePlaceholder = false; }
            if (therePlaceholder) { nextPos = placeholderPos; }
            else { nextPos = Camera.main.ScreenToWorldPoint(Input.mousePosition); }
        }
        else
        {
            nextPos = startPos;
        }
        float rast = Mathf.Sqrt((transform.position.x - nextPos.x) * (transform.position.x - nextPos.x) + (transform.position.y - nextPos.y) * (transform.position.y - nextPos.y));
        transform.position = Vector2.MoveTowards(transform.position, nextPos, Time.deltaTime * velocity * rast);
    }
}
