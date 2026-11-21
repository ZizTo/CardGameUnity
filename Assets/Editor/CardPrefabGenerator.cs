using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Генератор prefab'ов для расширенного набора карт.
///
/// Запуск из меню: Tools -> Cards -> Generate Expanded Set.
/// Batchmode:
///   Unity.exe -batchmode -quit -projectPath ... -executeMethod CardPrefabGenerator.GenerateFromCommandLine
///
/// Генератор создаёт prefab'ы из шаблонов Name/Card*.prefab, проставляет
/// статы и ключевые слова, а затем пересобирает оба Deck.deck в SampleScene.
/// </summary>
public static class CardPrefabGenerator
{
    private const string TemplateFolder = "Assets/Prefabs/Cards/Goblin";
    private const string GeneratedRoot = "Assets/Prefabs/Cards/Generated";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Cards/Generate Expanded Set", false, 1)]
    public static void GenerateFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Card generator", "Остановите Play Mode перед генерацией карт.", "OK");
            return;
        }

        Generate();
        EditorUtility.DisplayDialog("Card generator", "Готово. Проверьте SampleScene и карты в Assets/Prefabs/Cards/Generated.", "OK");
    }

    [MenuItem("Tools/Cards/Generate Expanded Set", true)]
    private static bool ValidateGenerateFromMenu()
    {
        return !EditorApplication.isPlaying;
    }

    public static void GenerateFromCommandLine()
    {
        try
        {
            Generate();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Generate()
    {
        GameObject cardTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(TemplateFolder + "/GoblinCard.prefab");
        GameObject enemyCardTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(TemplateFolder + "/GoblinCardEnemy.prefab");
        GameObject tableTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(TemplateFolder + "/GoblinCardTable.prefab");

        if (cardTemplate == null || enemyCardTemplate == null || tableTemplate == null)
            throw new InvalidOperationException("Не найдены шаблоны в " + TemplateFolder);

        if (AssetDatabase.IsValidFolder(GeneratedRoot))
            AssetDatabase.DeleteAsset(GeneratedRoot);

        EnsureFolder(GeneratedRoot);

        Dictionary<string, string> normalCardPaths = new Dictionary<string, string>();
        Dictionary<string, string> enemyCardPaths = new Dictionary<string, string>();

        for (int i = 0; i < CardCatalog.Cards.Count; i++)
        {
            CardDefinition definition = CardCatalog.Cards[i];
            string cardFolder = GeneratedRoot + "/" + definition.name;
            EnsureFolder(cardFolder);

            string cardPath = cardFolder + "/" + definition.name + "Card.prefab";
            string enemyCardPath = cardFolder + "/" + definition.name + "CardEnemy.prefab";
            string tablePath = cardFolder + "/" + definition.name + "CardTable.prefab";

            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(cardTemplate), cardPath);
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(enemyCardTemplate), enemyCardPath);
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(tableTemplate), tablePath);
            AssetDatabase.SaveAssets();

            ConfigureTablePrefab(tablePath, definition);
            GameObject tableAsset = AssetDatabase.LoadAssetAtPath<GameObject>(tablePath);

            ConfigureHandPrefab(cardPath, definition.name + "Card", tableAsset);
            ConfigureHandPrefab(enemyCardPath, definition.name + "CardEnemy", tableAsset);

            normalCardPaths[definition.name] = cardPath;
            enemyCardPaths[definition.name] = enemyCardPath;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RebuildScene(normalCardPaths, enemyCardPaths);

        Debug.Log("CardPrefabGenerator: generated " + CardCatalog.Cards.Count +
                  " card types, deck size " + CardCatalog.TotalCards);
    }

    private static void ConfigureTablePrefab(string path, CardDefinition definition)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            contents.name = definition.name + "CardTable";

            CardOnTable card = contents.GetComponent<CardOnTable>();
            if (card == null)
                throw new InvalidOperationException("В шаблоне CardTable нет CardOnTable: " + path);

            card.HP = definition.hp;
            card.Damage = definition.attack;
            card.Mana = definition.mana;
            card.Name = definition.name;
            card.Desc = CardCatalog.DescribeKeywords(definition);
            card.CanAttack = false;
            card.Provoc = definition.taunt;
            card.Charge = definition.charge;
            card.DivineShield = definition.divineShield;
            card.Lifesteal = definition.lifesteal;

            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void ConfigureHandPrefab(string path, string rootName, GameObject tableAsset)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            contents.name = rootName;

            CardMoving moving = contents.GetComponent<CardMoving>();
            if (moving == null)
                throw new InvalidOperationException("В шаблоне Card/CardEnemy нет CardMoving: " + path);

            moving.tableCardPref = tableAsset;
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void RebuildScene(Dictionary<string, string> normalCardPaths,
                                     Dictionary<string, string> enemyCardPaths)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        bool changed = false;

        Order order = UnityEngine.Object.FindObjectOfType<Order>();
        if (order != null)
        {
            order.maxMana = 10;
            order.heroMaxHp = 30;
            order.secondPlayerFirstTurnBonusMana = 1;
            order.fatigueYou = 0;
            order.fatigueEnemy = 0;
            EditorUtility.SetDirty(order);
            changed = true;
        }

        SetHeroHp("Player", 30);
        SetHeroHp("Enemy", 30);

        GameObject playerHolder = GameObject.Find("CardHolder");
        GameObject enemyHolder = GameObject.Find("CardHolderEnemy");

        if (playerHolder != null)
            changed |= RebuildDeck(playerHolder.GetComponent<Deck>(), normalCardPaths);

        if (enemyHolder != null)
            changed |= RebuildDeck(enemyHolder.GetComponent<Deck>(), enemyCardPaths);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
    }

    private static void SetHeroHp(string name, int hp)
    {
        GameObject heroObject = GameObject.Find(name);
        if (heroObject == null) return;

        CardOnTable hero = heroObject.GetComponent<CardOnTable>();
        if (hero == null) return;

        hero.HP = hp;
        EditorUtility.SetDirty(hero);
        if (hero.hpT != null)
            hero.hpT.text = "" + hp;
    }

    private static bool RebuildDeck(Deck deck, Dictionary<string, string> cardPaths)
    {
        if (deck == null) return false;

        List<GameObject> newDeck = new List<GameObject>();

        for (int i = 0; i < CardCatalog.Cards.Count; i++)
        {
            CardDefinition definition = CardCatalog.Cards[i];
            string path;
            if (!cardPaths.TryGetValue(definition.name, out path))
                continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException("Не найден prefab карты: " + path);

            for (int copy = 0; copy < definition.copies; copy++)
                newDeck.Add(prefab);
        }

        deck.deck = newDeck;
        EditorUtility.SetDirty(deck);
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string normalized = path.Replace('\\', '/');
        string parent = Path.GetDirectoryName(normalized);
        string name = Path.GetFileName(normalized);

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return;

        parent = parent.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}

