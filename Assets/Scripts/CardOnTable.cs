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
    public bool Charge, DivineShield, Lifesteal;

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
        //if (gameObject.name == "Enemy") { HP += PlayerPrefs.GetInt("Diffic") * 10; }
        if (manaT != null) { manaT.text = "" + Mana; }
        if (dmgT != null) { dmgT.text = "" + Damage; }
        if (hpT != null) { hpT.text = "" + HP; }
        if (nameT != null) { nameT.text = Name; }
        if (DescT != null) { DescT.text = Desc; }

        if (Charge)
            CanAttack = true;

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
        CardOnTable target = enemy.GetComponent<CardOnTable>();
        if (target == null) return;

        int damageToTarget = Damage;
        int damageToAttacker = target.Damage;

        if (target.DivineShield && damageToTarget > 0)
        {
            target.DivineShield = false;
            damageToTarget = 0;
        }

        if (DivineShield && damageToAttacker > 0)
        {
            DivineShield = false;
            damageToAttacker = 0;
        }

        target.HP -= damageToTarget;
        HP -= damageToAttacker;
        CanAttack = false;

        if (Lifesteal && damageToTarget > 0)
            HealOwner(damageToTarget);

        if (target.Lifesteal && damageToAttacker > 0)
            target.HealOwner(damageToAttacker);

        onTableAud.clip = clip[1];
        onTableAud.Play();
        target.isAlive();
        isAlive();
    }

    private void HealOwner(int amount)
    {
        Order order = Camera.main.GetComponent<Order>();
        if (order == null) return;

        if (transform.parent.CompareTag("PlsEnemy"))
            order.HealEnemy(amount);
        else
            order.HealPlayer(amount);
    }

    public void isAlive()
    {
        hpT.text = "" + HP;
        if (HP <= 0)
        {
            GameObject newExp = Instantiate(explosionPref);
            newExp.transform.position = transform.position;
            if (gameObject.name == "Player") { PlayerPrefs.SetInt("WinOrNo", 2); SceneManager.LoadScene("Menu"); return; }
            if (gameObject.name == "Enemy") { PlayerPrefs.SetInt("WinOrNo", 1); SceneManager.LoadScene("Menu"); return; }
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
