using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Order : MonoBehaviour
{
[Header("Turn")]
public bool yourOrd = false;
public int orderKol = 0;
public int maxMana = 5;

[Header("Scene refs")]
public GameObject yourHolder;
public GameObject yourPlc;
public GameObject enemyHolder;
public GameObject enemyPlc;
public Button ChangeOrderButton;

[Header("UI")]
public TMP_Text manaYouT;
public TMP_Text manaEnemyT;
public TMP_Text timeT;

[Header("Mana")]
public int manaYou = 0;
public int manaEnemy = 0;

[Header("Enemy AI")]
public AiProfile enemyAiProfile = new AiProfile();

private List<int> enemyBoardSlotMap = new List<int>();
private List<int> yourBoardSlotMap = new List<int>();

int canPlace = -1;
int placeWhere = -1;
int canAttack = -1;
int attackHwo = -1;
bool readyToGo = false;
Vector3 thisPosLine = new Vector3();
float targTime = 30f;
IAiAgent currentAgent;
int failedAiActionsThisTurn = 0;
private const int MaxFailedAiActionsPerTurn = 3;

private void Awake()
{
    var diffic = PlayerPrefs.GetInt("Diffic", 0);
    switch (diffic)
    {
        case 0: enemyAiProfile = new AiProfile(AiType.RuleBased, 0, 0, 0, false); break;
        case 1: enemyAiProfile = new AiProfile(AiType.Minimax, 2, 0, 0, false); break;
        case 2: enemyAiProfile = new AiProfile(AiType.Minimax, 2, 0, 0, true); break;
        case 3: enemyAiProfile = new AiProfile(AiType.Minimax, 3, 0, 0, true); break;
        case 5: enemyAiProfile = new AiProfile(AiType.Mcts, 0, 100, 4, false); break;
        case 6: enemyAiProfile = new AiProfile(AiType.Mcts, 0, 300, 6, false); break;
        case 7: enemyAiProfile = new AiProfile(AiType.Mcts, 0, 1000, 12, false); break;
    }

    currentAgent = CreateAgent(enemyAiProfile);
}

private void Start()
{
    ChangeOrder();
}

public void ChangeManaYou(int kol)
{
    manaYou += kol;
    if (manaYou < 0) manaYou = 0;
    manaYouT.text = "mana: " + manaYou + "/" + maxMana;
}

public void ChangeManaEnemy(int kol)
{
    manaEnemy += kol;
    if (manaEnemy < 0) manaEnemy = 0;
    manaEnemyT.text = "mana: " + manaEnemy + "/" + maxMana;
}

public void ChangeOrder()
{
    yourOrd = !yourOrd;
    targTime = 30f;
    failedAiActionsThisTurn = 0;

    foreach (GameObject card in yourPlc.GetComponent<Placeholders>().placeholders)
        if (card != null && card.CompareTag("TableCard"))
        {
            CardOnTable c = card.GetComponent<CardOnTable>();
            if (c.Damage > 0) c.CanAttack = yourOrd;
        }

    foreach (GameObject card in enemyPlc.GetComponent<Placeholders>().placeholders)
        if (card != null && card.CompareTag("TableCard"))
        {
            CardOnTable c = card.GetComponent<CardOnTable>();
            if (c.Damage > 0) c.CanAttack = !yourOrd;
        }

    bool yourBoardEmpty = yourPlc.GetComponent<Placeholders>().placeholders.Count == 3 &&
                          yourPlc.GetComponent<Placeholders>().placeholders[0].CompareTag("Placeholder") &&
                          yourPlc.GetComponent<Placeholders>().placeholders[1].CompareTag("Placeholder") &&
                          yourPlc.GetComponent<Placeholders>().placeholders[2].CompareTag("Placeholder");

    bool enemyBoardEmpty = enemyPlc.GetComponent<Placeholders>().placeholders.Count == 3 &&
                           enemyPlc.GetComponent<Placeholders>().placeholders[0].CompareTag("Placeholder") &&
                           enemyPlc.GetComponent<Placeholders>().placeholders[1].CompareTag("Placeholder") &&
                           enemyPlc.GetComponent<Placeholders>().placeholders[2].CompareTag("Placeholder");

    bool yourHandEmpty = yourHolder.GetComponent<CardHolder>().cards.Count == 0;
    bool enemyHandEmpty = enemyHolder.GetComponent<CardHolder>().cards.Count == 0;
    bool yourDeckEmpty = yourHolder.GetComponent<Deck>().deck.Count == 0;
    bool enemyDeckEmpty = enemyHolder.GetComponent<Deck>().deck.Count == 0;

    if (yourBoardEmpty && enemyBoardEmpty && yourHandEmpty && enemyHandEmpty && yourDeckEmpty && enemyDeckEmpty)
    {
        PlayerPrefs.SetInt("WinOrNo", 3);
        SceneManager.LoadScene("Menu");
        return;
    }

    if (yourOrd)
    {
        orderKol++;
        if (orderKol > maxMana)
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
            yourHolder.GetComponent<CardHolder>().newCard(yourHolder.GetComponent<Deck>().RandCard());

        if (yourHolder.GetComponent<Deck>().deck.Count == 0)
            yourHolder.GetComponent<CardHolder>().Deck.SetActive(false);

        ChangeOrderButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "End turn";
        ChangeOrderButton.enabled = true;
    }
    else
    {
        if (enemyHolder.GetComponent<Deck>().deck.Count > 0 && enemyHolder.GetComponent<CardHolder>().cards.Count < enemyHolder.GetComponent<CardHolder>().maxCardKol)
            enemyHolder.GetComponent<CardHolder>().newCard(enemyHolder.GetComponent<Deck>().RandCard());

        if (enemyHolder.GetComponent<Deck>().deck.Count == 0)
            enemyHolder.GetComponent<CardHolder>().Deck.SetActive(false);

        ChangeOrderButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "Enemy turn";
        ChangeOrderButton.enabled = false;
        StartCoroutine(EnemyThinkCoroutine());
    }
}

private void Update()
{
    if (canPlace >= 0 && canPlace < enemyHolder.GetComponent<CardHolder>().cards.Count && !readyToGo)
    {
        Vector2 nextPos = enemyHolder.GetComponent<CardHolder>().cards[canPlace].transform.position;
        Vector2 thisPos = enemyHolder.GetComponent<CardHolder>().cards[canPlace].GetComponent<CardMoving>().startPos;
        float rast = Mathf.Sqrt((thisPos.x - nextPos.x) * (thisPos.x - nextPos.x) + (thisPos.y - nextPos.y) * (thisPos.y - nextPos.y));
        if (rast < 0.0001f) readyToGo = true;
    }

    if (canPlace >= 0 && canPlace < enemyHolder.GetComponent<CardHolder>().cards.Count && readyToGo)
    {
        Vector2 nextPos = enemyPlc.GetComponent<Placeholders>().placePositions[placeWhere];
        Vector2 thisPos = enemyHolder.GetComponent<CardHolder>().cards[canPlace].transform.position;
        enemyHolder.GetComponent<CardHolder>().cards[canPlace].GetComponent<CardMoving>().startPos = nextPos;
        float rast = Mathf.Sqrt((thisPos.x - nextPos.x) * (thisPos.x - nextPos.x) + (thisPos.y - nextPos.y) * (thisPos.y - nextPos.y));
        if (rast < 0.001f)
        {
            enemyPlc.GetComponent<Placeholders>().AddCard(enemyPlc.GetComponent<Placeholders>().placeholders[placeWhere], enemyHolder.GetComponent<CardHolder>().cards[canPlace].GetComponent<CardMoving>().tableCardPref);
            enemyHolder.GetComponent<CardHolder>().cards[canPlace].SetActive(false);
            canPlace = -1;
            placeWhere = -1;
            readyToGo = false;
            canAttack = -1;
            attackHwo = -1;
            StartCoroutine(EnemyThinkCoroutine());
        }
    }

    if (canAttack >= 0)
    {
        var enemySlots = enemyPlc.GetComponent<Placeholders>().placeholders;
        var yourSlots = yourPlc.GetComponent<Placeholders>().placeholders;

        if (canAttack >= enemySlots.Count || attackHwo < 0 || attackHwo >= yourSlots.Count)
        {
            FailAiAction("attack index out of range");
            return;
        }

        GameObject attackerObj = enemySlots[canAttack];
        GameObject targetObj = yourSlots[attackHwo];

        if (attackerObj == null || targetObj == null || !attackerObj.CompareTag("TableCard") || !targetObj.CompareTag("TableCard"))
        {
            FailAiAction("attacker or target object invalid");
            return;
        }

        Vector3 nextPos = new Vector3(yourPlc.GetComponent<Placeholders>().placePositions[attackHwo].x, yourPlc.GetComponent<Placeholders>().placePositions[attackHwo].y, -10);
        float rast = Mathf.Sqrt((thisPosLine.x - nextPos.x) * (thisPosLine.x - nextPos.x) + (thisPosLine.y - nextPos.y) * (thisPosLine.y - nextPos.y));
        thisPosLine = Vector3.MoveTowards(thisPosLine, nextPos, Time.deltaTime * 10f);
        attackerObj.GetComponent<CardOnTable>().thisPos = thisPosLine;

        if (rast < 0.01f)
        {
            attackerObj.GetComponent<CardOnTable>().thisPos = enemyPlc.GetComponent<Placeholders>().placePositions[canAttack];
            if (targetObj != null && targetObj.CompareTag("TableCard"))
                attackerObj.GetComponent<CardOnTable>().DealDamage(targetObj);

            canPlace = -1;
            placeWhere = -1;
            readyToGo = false;
            canAttack = -1;
            attackHwo = -1;
            StartCoroutine(EnemyThinkCoroutine());
        }
    }

    if (Input.GetKeyDown(KeyCode.Escape))
        SceneManager.LoadScene("Menu");

    targTime -= Time.deltaTime;
    if (targTime < 0)
        ChangeOrder();

    timeT.text = "Time: " + Mathf.Ceil(targTime);
}

IEnumerator EnemyThinkCoroutine()
{
    yield return new WaitForSeconds(0.8f);
    if (yourOrd) yield break;
    BotLogic();
}

void BotLogic()
{
    if (failedAiActionsThisTurn >= MaxFailedAiActionsPerTurn)
    {
        AiDebug.Log("AI forced end turn after repeated failed actions");
        ChangeOrder();
        return;
    }

    canPlace = -1;
    placeWhere = -1;
    readyToGo = false;
    canAttack = -1;
    attackHwo = -1;

    SimGameState state = BuildStateFromScene();
    //AiDebug.Log("STATE BEFORE\n" + SimStatePrinter.Print(state));

    List<SimAction> actions = SimActionGenerator.GenerateActions(state);
    if (actions == null || actions.Count == 0)
    {
        ChangeOrder();
        return;
    }

    SimAction action = currentAgent.ChooseAction(state, actions, enemyAiProfile);
    
    AiDebug.Log("TURN INFO | ai=" + enemyAiProfile.aiType +
                " depth=" + enemyAiProfile.minimaxDepth +
                " iters=" + enemyAiProfile.mctsIterations +
                " rollout=" + enemyAiProfile.rolloutDepth);

    AiDebug.Log("ACTIONS COUNT = " + actions.Count);
    for (int i = 0; i < actions.Count; i++)
        AiDebug.Log("ACTION[" + i + "] = " + actions[i]);
    
    ExecuteActionInScene(action);
}

private IAiAgent CreateAgent(AiProfile profile)
{
    switch (profile.aiType)
    {
        case AiType.Minimax: return new MinimaxAgent();
        case AiType.Mcts: return new MctsAgent();
        case AiType.Expectimax: return new MinimaxAgent();
        default: return new RuleBasedAgent();
    }
}

private SimCard BuildSimCardFromPrefab(GameObject cardPrefab)
{
    if (cardPrefab == null) return null;
    CardOnTable c = cardPrefab.GetComponent<CardOnTable>();
    if (c == null) return null;

    return new SimCard
    {
        id = cardPrefab.name,
        cardName = c.Name,
        mana = c.Mana,
        attack = c.Damage,
        hp = c.HP,
        canAttack = false,
        provoc = c.Provoc
    };
}

private List<SimCard> BuildEnemyDeck()
{
    List<SimCard> result = new List<SimCard>();
    Deck deckComponent = enemyHolder.GetComponent<Deck>();
    if (deckComponent == null || deckComponent.deck == null) return result;

    for (int i = 0; i < deckComponent.deck.Count; i++)
    {
        GameObject cardPrefab = deckComponent.deck[i];
        SimCard simCard = BuildSimCardFromPrefab(cardPrefab);
        if (simCard != null) result.Add(simCard);
    }

    return result;
}

private List<SimCard> BuildYourDeck()
{
    List<SimCard> result = new List<SimCard>();
    Deck deckComponent = yourHolder.GetComponent<Deck>();
    if (deckComponent == null || deckComponent.deck == null) return result;

    for (int i = 0; i < deckComponent.deck.Count; i++)
    {
        GameObject cardPrefab = deckComponent.deck[i];
        SimCard simCard = BuildSimCardFromPrefab(cardPrefab);
        if (simCard != null) result.Add(simCard);
    }

    return result;
}

private SimGameState BuildStateFromScene()
{
    SimGameState state = new SimGameState();
    enemyBoardSlotMap.Clear();
    yourBoardSlotMap.Clear();

    state.orderKol = orderKol;
    state.maxManaCap = maxMana;
    state.me.mana = manaEnemy;
    state.me.maxMana = maxMana;
    state.me.heroHp = GetEnemyHeroHp();
    state.me.deck = BuildEnemyDeck();

    state.enemy.mana = manaYou;
    state.enemy.maxMana = maxMana;
    state.enemy.heroHp = GetPlayerHeroHp();
    state.enemy.deck = BuildYourDeck();

    List<GameObject> enemyHand = enemyHolder.GetComponent<CardHolder>().cards;
    for (int i = 0; i < enemyHand.Count; i++)
    {
        GameObject cardObj = enemyHand[i];
        if (cardObj == null || !cardObj.activeSelf) continue;
        CardMoving moving = cardObj.GetComponent<CardMoving>();
        if (moving == null || moving.tableCardPref == null) continue;
        CardOnTable c = moving.tableCardPref.GetComponent<CardOnTable>();
        if (c == null) continue;

        state.me.hand.Add(new SimCard
        {
            id = cardObj.name,
            cardName = c.Name,
            mana = c.Mana,
            attack = c.Damage,
            hp = c.HP,
            canAttack = false,
            provoc = c.Provoc
        });
    }

    List<GameObject> enemySlots = enemyPlc.GetComponent<Placeholders>().placeholders;
    for (int i = 0; i < enemySlots.Count; i++)
    {
        GameObject cardObj = enemySlots[i];
        if (cardObj == null || !cardObj.CompareTag("TableCard")) continue;
        CardOnTable c = cardObj.GetComponent<CardOnTable>();
        if (c == null) continue;

        state.me.board.Add(new SimCard
        {
            id = cardObj.name,
            cardName = c.Name,
            mana = c.Mana,
            attack = c.Damage,
            hp = c.HP,
            canAttack = c.CanAttack,
            provoc = c.Provoc
        });
        enemyBoardSlotMap.Add(i);
    }

    List<GameObject> yourHand = yourHolder.GetComponent<CardHolder>().cards;
    for (int i = 0; i < yourHand.Count; i++)
    {
        GameObject cardObj = yourHand[i];
        if (cardObj == null || !cardObj.activeSelf) continue;
        CardMoving moving = cardObj.GetComponent<CardMoving>();
        if (moving == null || moving.tableCardPref == null) continue;
        CardOnTable c = moving.tableCardPref.GetComponent<CardOnTable>();
        if (c == null) continue;

        state.enemy.hand.Add(new SimCard
        {
            id = cardObj.name,
            cardName = c.Name,
            mana = c.Mana,
            attack = c.Damage,
            hp = c.HP,
            canAttack = false,
            provoc = c.Provoc
        });
    }

    List<GameObject> yourSlots = yourPlc.GetComponent<Placeholders>().placeholders;
    for (int i = 0; i < yourSlots.Count; i++)
    {
        GameObject cardObj = yourSlots[i];
        if (cardObj == null || !cardObj.CompareTag("TableCard")) continue;
        CardOnTable c = cardObj.GetComponent<CardOnTable>();
        if (c == null) continue;

        state.enemy.board.Add(new SimCard
        {
            id = cardObj.name,
            cardName = c.Name,
            mana = c.Mana,
            attack = c.Damage,
            hp = c.HP,
            canAttack = c.CanAttack,
            provoc = c.Provoc
        });
        yourBoardSlotMap.Add(i);
    }

    state.isMyTurn = true;
    return state;
}

private void ExecuteActionInScene(SimAction action)
{
    AiDebug.Log("EXECUTE ACTION = " + action);
    if (action == null)
    {
        FailAiAction("chosen action is null");
        return;
    }

    switch (action.actionType)
    {
        case SimActionType.PlayCard:
        {
            List<GameObject> hand = enemyHolder.GetComponent<CardHolder>().cards;
            placeWhere = FindFirstFreeEnemySlot();

            if (action.handIndex < 0 || action.handIndex >= hand.Count || placeWhere < 0)
            {
                FailAiAction("play card invalid handIndex or no free slot");
                return;
            }

            GameObject card = hand[action.handIndex];
            if (card == null || !card.activeSelf)
            {
                FailAiAction("play card object invalid or inactive");
                return;
            }

            CardMoving moving = card.GetComponent<CardMoving>();
            if (moving == null || moving.tableCardPref == null)
            {
                FailAiAction("play card missing CardMoving or tableCardPref");
                return;
            }

            CardOnTable cardData = moving.tableCardPref.GetComponent<CardOnTable>();
            if (cardData == null)
            {
                FailAiAction("play card missing CardOnTable");
                return;
            }

            int manaCost = cardData.Mana;
            if (manaCost > manaEnemy)
            {
                FailAiAction("play card not enough mana in real scene");
                return;
            }

            canPlace = action.handIndex;
            ChangeManaEnemy(-manaCost);
            readyToGo = false;
            break;
        }

        case SimActionType.AttackCard:
        {
            if (action.attackerIndex < 0 || action.attackerIndex >= enemyBoardSlotMap.Count)
            {
                FailAiAction("attack card invalid attackerIndex map");
                return;
            }

            if (action.targetIndex < 0 || action.targetIndex >= yourBoardSlotMap.Count)
            {
                FailAiAction("attack card invalid targetIndex map");
                return;
            }

            int realAttackerSlot = enemyBoardSlotMap[action.attackerIndex];
            int realTargetSlot = yourBoardSlotMap[action.targetIndex];

            List<GameObject> enemySlots = enemyPlc.GetComponent<Placeholders>().placeholders;
            List<GameObject> yourSlots = yourPlc.GetComponent<Placeholders>().placeholders;

            if (realAttackerSlot < 0 || realAttackerSlot >= enemySlots.Count || realTargetSlot < 0 || realTargetSlot >= yourSlots.Count)
            {
                FailAiAction("attack card real slot out of range");
                return;
            }

            GameObject attackerObj = enemySlots[realAttackerSlot];
            GameObject targetObj = yourSlots[realTargetSlot];

            if (attackerObj == null || !attackerObj.CompareTag("TableCard") || targetObj == null || !targetObj.CompareTag("TableCard"))
            {
                FailAiAction("attack card attacker or target invalid in scene");
                return;
            }

            CardOnTable attacker = attackerObj.GetComponent<CardOnTable>();
            if (attacker == null || !attacker.CanAttack)
            {
                FailAiAction("attack card attacker cannot attack");
                return;
            }

            bool enemyHasProvoc = false;
            foreach (var obj in yourSlots)
            {
                if (obj != null && obj.CompareTag("TableCard"))
                {
                    var card = obj.GetComponent<CardOnTable>();
                    if (card != null && card.Provoc && card.HP > 0)
                    {
                        enemyHasProvoc = true;
                        break;
                    }
                }
            }

            var targetCard = targetObj.GetComponent<CardOnTable>();
            if (enemyHasProvoc && (targetCard == null || !targetCard.Provoc))
            {
                FailAiAction("attack card target invalid because provoc exists");
                return;
            }

            canAttack = realAttackerSlot;
            attackHwo = realTargetSlot;
            thisPosLine = enemyPlc.GetComponent<Placeholders>().placePositions[canAttack];
            break;
        }

        case SimActionType.AttackHero:
        {
            if (action.attackerIndex < 0 || action.attackerIndex >= enemyBoardSlotMap.Count)
            {
                FailAiAction("attack hero invalid attackerIndex map");
                return;
            }

            List<GameObject> yourSlots = yourPlc.GetComponent<Placeholders>().placeholders;
            foreach (var obj in yourSlots)
            {
                if (obj != null && obj.CompareTag("TableCard"))
                {
                    var card = obj.GetComponent<CardOnTable>();
                    if (card != null && card.Provoc && card.HP > 0)
                    {
                        FailAiAction("attack hero blocked by provoc");
                        return;
                    }
                }
            }

            int realAttackerSlot = enemyBoardSlotMap[action.attackerIndex];
            List<GameObject> enemySlots = enemyPlc.GetComponent<Placeholders>().placeholders;
            if (realAttackerSlot < 0 || realAttackerSlot >= enemySlots.Count)
            {
                FailAiAction("attack hero real attacker slot out of range");
                return;
            }

            GameObject attackerObj = enemySlots[realAttackerSlot];
            if (attackerObj == null || !attackerObj.CompareTag("TableCard"))
            {
                FailAiAction("attack hero attacker object invalid");
                return;
            }

            CardOnTable attacker = attackerObj.GetComponent<CardOnTable>();
            if (attacker == null || !attacker.CanAttack)
            {
                FailAiAction("attack hero attacker cannot attack");
                return;
            }

            GameObject playerHero = GameObject.Find("Player");
            if (playerHero == null)
            {
                FailAiAction("attack hero player hero object not found");
                return;
            }

            CardOnTable hero = playerHero.GetComponent<CardOnTable>();
            if (hero == null)
            {
                FailAiAction("attack hero player CardOnTable missing");
                return;
            }

            hero.HP -= attacker.Damage;
            attacker.CanAttack = false;
            hero.isAlive();
            StartCoroutine(EnemyThinkCoroutine());
            break;
        }

        case SimActionType.EndTurn:
        default:
            ChangeOrder();
            break;
    }
}

private void FailAiAction(string reason)
{
    failedAiActionsThisTurn++;
    canPlace = -1;
    placeWhere = -1;
    readyToGo = false;
    canAttack = -1;
    attackHwo = -1;
    AiDebug.Log("AI action failed: " + reason + " | failedCount=" + failedAiActionsThisTurn);

    if (failedAiActionsThisTurn >= MaxFailedAiActionsPerTurn)
        ChangeOrder();
    else
        StartCoroutine(EnemyThinkCoroutine());
}

private int FindFirstFreeEnemySlot()
{
    List<GameObject> placeholders = enemyPlc.GetComponent<Placeholders>().placeholders;
    for (int i = 0; i < placeholders.Count; i++)
        if (placeholders[i] != null && placeholders[i].CompareTag("Placeholder"))
            return i;
    return -1;
}

private int GetPlayerHeroHp()
{
    GameObject playerHero = GameObject.Find("Player");
    if (playerHero != null)
        return playerHero.GetComponent<CardOnTable>().HP;
    return 30;
}

private int GetEnemyHeroHp()
{
    GameObject enemyHero = GameObject.Find("Enemy");
    if (enemyHero != null)
        return enemyHero.GetComponent<CardOnTable>().HP;
    return 30;
}
}