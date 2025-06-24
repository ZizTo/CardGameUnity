using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CardOnTable : MonoBehaviour
{
    public int HP, Damage, Mana;
    public string Name, Desc;
    public bool CanAttack, Provoc;

    public LineRenderer lineRenderer;
    bool mouseOn = false;
    bool attacking = false;

    public GameObject canAttackBackg;
    public TMP_Text manaT, dmgT, hpT, nameT, DescT;
    public Vector3 thisPos = new Vector3();

    public GameObject explosionPref;

    AudioSource onTableAud;
    public AudioClip[] clip; 

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        thisPos = transform.position;
        if (gameObject.name == "Enemy") { HP += PlayerPrefs.GetInt("Diffic") * 10; }
        if (manaT != null) { manaT.text = "" + Mana; }
        if (dmgT != null) { dmgT.text = "" + Damage; }
        if (hpT != null) { hpT.text = "" + HP; }
        if (nameT != null) { nameT.text = Name; }
        if (DescT != null) { DescT.text = Desc; }

        if (transform.parent.CompareTag("PlsEnemy"))
        {
            onTableAud = Camera.main.GetComponent<Order>().enemyPlc.GetComponent<AudioSource>();
        }
        else { onTableAud = Camera.main.GetComponent<Order>().yourPlc.GetComponent<AudioSource>(); }
        
        onTableAud.clip = clip[0];
        onTableAud.Play();
    }

    private void OnMouseEnter()
    {
        mouseOn = true;
    }
    private void OnMouseExit()
    {
        mouseOn = false;
    }
    private void OnMouseDown()
    {
        attacking = mouseOn && CanAttack && !transform.parent.CompareTag("PlsEnemy");
    }
    private void OnMouseUp()
    {
        if (attacking) {
            Collider2D[] colliders = Physics2D.OverlapPointAll(Camera.main.ScreenPointToRay(Input.mousePosition).origin);
            foreach (Collider2D collider in colliders) {
                if (collider.transform.CompareTag("TableCard")) {
                    if ((collider.transform.parent.CompareTag("PlsEnemy") ||
                        collider.transform.parent.CompareTag("PlsYou")) &&
                        !collider.transform.parent.CompareTag(transform.parent.tag))
                    {
                        bool isProvoc = false;
                        foreach (GameObject card in Camera.main.GetComponent<Order>().enemyPlc.GetComponent<Placeholders>().placeholders)
                        {
                            if (card.CompareTag("TableCard"))
                            {
                                if (card.GetComponent<CardOnTable>().Provoc) { isProvoc = true; break; }
                            }
                        }
                        if (!isProvoc || collider.gameObject.GetComponent<CardOnTable>().Provoc)
                        {
                            DealDamage(collider.gameObject);
                            break;
                        }
                    }
                }
            }
        }
        attacking = false;
    }
    public void DealDamage(GameObject enemy)
    {
        enemy.GetComponent<CardOnTable>().HP -= Damage;
        HP -= enemy.GetComponent<CardOnTable>().Damage;
        CanAttack = false;
        onTableAud.clip = clip[1];
        onTableAud.Play();
        enemy.GetComponent<CardOnTable>().isAlive();
        isAlive();
    }

    public void isAlive()
    {
        hpT.text = "" + HP;
        if (HP <= 0)
        {
            GameObject newExp = Instantiate(explosionPref);
            newExp.transform.position = transform.position;
            if (gameObject.name == "Player") { PlayerPrefs.SetInt("WinOrNo", 2); SceneManager.LoadScene("Menu"); }
            if (gameObject.name == "Enemy") { PlayerPrefs.SetInt("WinOrNo", 1); SceneManager.LoadScene("Menu"); }
            gameObject.transform.parent.GetComponent<Placeholders>().DelCard(gameObject);
            onTableAud.clip = clip[2];
            onTableAud.Play();
        }
    }

    private void Update()
    {
        if (attacking)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, mousePos);
        }
        else {
            lineRenderer.SetPosition(0, transform.position); 
            lineRenderer.SetPosition(1, thisPos); }

        if (canAttackBackg != null) { canAttackBackg.SetActive(CanAttack); }
        
    }
}
