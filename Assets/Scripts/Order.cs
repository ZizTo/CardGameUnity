using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Order : MonoBehaviour
{
    public bool yourOrd = false;
    public GameObject yourHolder, yourPlc, enemyHolder, enemyPlc;
    public Button ChangeOrderButton;
    public int orderKol = 0, maxMana = 5;

    int canPlace = -1, placeWhere = -1, canAttack = -1, attackHwo = -1;
    bool readyToGo = false;
    Vector3 thisPosLine = new Vector3();

    public int manaYou = 0, manaEnemy = 0;
    public TMP_Text manaYouT, manaEnemyT, timeT;

    float targTime = 30f;


    public void ChangeManaYou(int kol)
    {
        manaYou += kol;
        manaYouT.text = "mana " + manaYou + "/" + maxMana;
    }

    public void ChangeManaEnemy(int kol)
    {
        manaEnemy += kol;
        manaEnemyT.text = "mana " + manaEnemy + "/" + maxMana;
    }


    public void ChangeOrder()
    {
        yourOrd = !yourOrd;
        targTime = 30f;
        foreach (GameObject card in yourPlc.GetComponent<Placeholders>().placeholders)
        {
            if (card.transform.CompareTag("TableCard"))
            {
                if (card.GetComponent<CardOnTable>().Damage > 0)
                {
                    card.GetComponent<CardOnTable>().CanAttack = yourOrd;
                }
            }
        }
        foreach (GameObject card in enemyPlc.GetComponent<Placeholders>().placeholders)
        {
            if (card.transform.CompareTag("TableCard"))
            {
                if (card.GetComponent<CardOnTable>().Damage > 0)
                {
                    card.GetComponent<CardOnTable>().CanAttack = !yourOrd;
                }
            }
        }
        if (yourPlc.GetComponent<Placeholders>().placeholders.Count < 3) { }
        else if (yourPlc.GetComponent<Placeholders>().placeholders[0].CompareTag("Placeholder") &&
            yourPlc.GetComponent<Placeholders>().placeholders[1].CompareTag("Placeholder") &&
            yourPlc.GetComponent<Placeholders>().placeholders[2].CompareTag("Placeholder") &&
            enemyPlc.GetComponent<Placeholders>().placeholders[0].CompareTag("Placeholder") &&
            enemyPlc.GetComponent<Placeholders>().placeholders[1].CompareTag("Placeholder") &&
            enemyPlc.GetComponent<Placeholders>().placeholders[2].CompareTag("Placeholder") &&
            yourHolder.GetComponent<CardHolder>().cards.Count == 0 &&
            enemyHolder.GetComponent<CardHolder>().cards.Count == 0 &&
            yourHolder.GetComponent<Deck>().deck.Count == 0 &&
            yourHolder.GetComponent<Deck>().deck.Count == 0)
        {
            PlayerPrefs.SetInt("WinOrNo", 3); SceneManager.LoadScene("Menu");
        }
        if (yourOrd)
        {
            orderKol++;
            if(orderKol >= maxMana)
            {
                manaYou = maxMana;
                manaEnemy = maxMana;
            }
            else
            {
                manaYou = orderKol;
                manaEnemy = orderKol;
            }
            ChangeManaEnemy(0);
            ChangeManaYou(0);
            
            if (yourHolder.GetComponent<Deck>().deck.Count > 0 && yourHolder.GetComponent<CardHolder>().cards.Count < yourHolder.GetComponent<CardHolder>().maxCardKol)
            {
                yourHolder.GetComponent<CardHolder>().newCard(yourHolder.GetComponent<Deck>().RandCard());
                if (yourHolder.GetComponent<Deck>().deck.Count <= 0) { yourHolder.GetComponent<CardHolder>().Deck.SetActive(false); }
            }
            ChangeOrderButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "End turn";
            ChangeOrderButton.enabled = true;
        }
        else
        {
            if (enemyHolder.GetComponent<Deck>().deck.Count > 0)
            {
                enemyHolder.GetComponent<CardHolder>().newCard(enemyHolder.GetComponent<Deck>().RandCard());
                if (enemyHolder.GetComponent<Deck>().deck.Count <= 0) { enemyHolder.GetComponent<CardHolder>().Deck.SetActive(false); }
            }
            ChangeOrderButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "Enemy turn";
            ChangeOrderButton.enabled = false;

            BotLogic();
        }
    }

    private void Start()
    {
        ChangeOrder();
    }

    private void Update()
    {
        if (canPlace >= 0 && canPlace < enemyHolder.GetComponent<CardHolder>().cards.Count && !readyToGo)
        {
            Vector2 nextPos = enemyHolder.GetComponent<CardHolder>().cards[canPlace].transform.position;
            Vector2 thisPos = enemyHolder.GetComponent<CardHolder>().cards[canPlace].GetComponent<CardMoving>().startPos;

            float rast = Mathf.Sqrt((thisPos.x - nextPos.x) * (thisPos.x - nextPos.x) + (thisPos.y - nextPos.y) * (thisPos.y - nextPos.y));
            if (rast < 0.0001f)
            {
                readyToGo = true;
            }
        }
        if (canPlace >= 0 && canPlace < enemyHolder.GetComponent<CardHolder>().cards.Count && readyToGo)
        {
            Vector2 nextPos = enemyPlc.GetComponent<Placeholders>().placePositions[placeWhere];
            Vector2 thisPos = enemyHolder.GetComponent<CardHolder>().cards[canPlace].transform.position;
            enemyHolder.GetComponent<CardHolder>().cards[canPlace].GetComponent<CardMoving>().startPos = nextPos;

            float rast = Mathf.Sqrt((thisPos.x - nextPos.x) * (thisPos.x - nextPos.x) + (thisPos.y - nextPos.y) * (thisPos.y - nextPos.y));
            if (rast < 0.001f)
            {
                enemyPlc.GetComponent<Placeholders>().AddCard(enemyPlc.GetComponent<Placeholders>().placeholders[placeWhere],
                    enemyHolder.GetComponent<CardHolder>().cards[canPlace].GetComponent<CardMoving>().tableCardPref);
                enemyHolder.GetComponent<CardHolder>().cards[canPlace].SetActive(false);
                canPlace = -1;
                placeWhere = -1;
                readyToGo = false;
                canAttack = -1;
                attackHwo = -1;
                StartCoroutine(ExampleCoroutine());
            }
        }
        if (canAttack >= 0)
        {
            Vector3 nextPos = new Vector3(yourPlc.GetComponent<Placeholders>().placePositions[attackHwo].x, yourPlc.GetComponent<Placeholders>().placePositions[attackHwo].y, -10);
            float rast = Mathf.Sqrt((thisPosLine.x - nextPos.x) * (thisPosLine.x - nextPos.x) + (thisPosLine.y - nextPos.y) * (thisPosLine.y - nextPos.y));
            thisPosLine = Vector3.MoveTowards(thisPosLine, nextPos, Time.deltaTime * 10f * rast);
            enemyPlc.GetComponent<Placeholders>().placeholders[canAttack].GetComponent<CardOnTable>().thisPos = thisPosLine;
            if (rast < 0.01f)
            { 
                enemyPlc.GetComponent<Placeholders>().placeholders[canAttack].GetComponent<CardOnTable>().thisPos = enemyPlc.GetComponent<Placeholders>().placePositions[canAttack];
                enemyPlc.GetComponent<Placeholders>().placeholders[canAttack].GetComponent<CardOnTable>().DealDamage(yourPlc.GetComponent<Placeholders>().placeholders[attackHwo]);
                canPlace = -1;
                placeWhere = -1;
                readyToGo = false;
                canAttack = -1;
                attackHwo = -1;
                StartCoroutine(ExampleCoroutine());
            }
        }


        if (Input.GetKeyDown(KeyCode.Escape)) { SceneManager.LoadScene("Menu"); }

        targTime -= Time.deltaTime;
        if (targTime <= 0) { ChangeOrder(); }
        timeT.text = "Time: " + Mathf.Ceil(targTime);
    }

    void BotLogic()
    {
        canPlace = -1;
        placeWhere = -1;
        readyToGo = false;
        canAttack = -1;
        attackHwo = -1;

        for (int i = 0; i < enemyPlc.GetComponent<Placeholders>().placeholders.Count; i++)
        {
            if (enemyPlc.GetComponent<Placeholders>().placeholders[i].CompareTag("Placeholder"))
            {
                for (int j = 0; j < enemyHolder.GetComponent<CardHolder>().cards.Count; j++)
                {
                    if (enemyHolder.GetComponent<CardHolder>().cards[j].GetComponent<CardMoving>().tableCardPref.GetComponent<CardOnTable>().Mana <= manaEnemy)
                    {
                        canPlace = Random.Range(0, enemyHolder.GetComponent<CardHolder>().cards.Count);
                        placeWhere = i;
                        ChangeManaEnemy(-enemyHolder.GetComponent<CardHolder>().cards[j].GetComponent<CardMoving>().tableCardPref.GetComponent<CardOnTable>().Mana);
                        break;
                    }
                }
                break;
            }
        }
        if (canPlace == -1)
        {
            for (int i = 0; i < enemyPlc.GetComponent<Placeholders>().placeholders.Count; i++)
            {
                if (enemyPlc.GetComponent<Placeholders>().placeholders[i].CompareTag("TableCard")) {
                    if (enemyPlc.GetComponent<Placeholders>().placeholders[i].GetComponent<CardOnTable>().CanAttack)
                    {
                        bool isProvoc = false;
                        foreach (GameObject card in yourPlc.GetComponent<Placeholders>().placeholders)
                        {
                            if (card.CompareTag("TableCard"))
                            {
                                if (card.GetComponent<CardOnTable>().Provoc) { isProvoc = true; break; }
                            }
                        }
                        for (int j = 0; j < yourPlc.GetComponent<Placeholders>().placeholders.Count; j++)
                        {
                            if (yourPlc.GetComponent<Placeholders>().placeholders[j].CompareTag("TableCard") && (!isProvoc || 
                                yourPlc.GetComponent<Placeholders>().placeholders[j].GetComponent<CardOnTable>().Provoc))
                            {
                                canAttack = i;
                                attackHwo = j;
                                thisPosLine = enemyPlc.GetComponent<Placeholders>().placePositions[i];
                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }
        if (canPlace == -1 && canAttack == -1)
        {
            ChangeOrder();
        }
    }

    IEnumerator ExampleCoroutine()
    {
        yield return new WaitForSeconds(1);
        BotLogic();
    }
}