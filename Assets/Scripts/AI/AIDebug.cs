using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class AiDebug
{
    public static bool Enabled = true;

    public static void Log(string text)
    {
        if (!Enabled) return;
        Debug.Log(text);
    }

    public static void LogBlock(string title, List<string> lines)
    {
        if (!Enabled) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("==== " + title + " ====");
        for (int i = 0; i < lines.Count; i++)
            sb.AppendLine(lines[i]);

        Debug.Log(sb.ToString());
    }
}

public static class SimStatePrinter
{
    public static string Print(SimGameState s)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine("ME hero=" + s.me.heroHp + " mana=" + s.me.mana + "/" + s.me.maxMana + " hand=" + s.me.hand.Count + " deck=" + s.me.deck.Count);
        for (int i = 0; i < s.me.board.Count; i++)
        {
            var c = s.me.board[i];
            sb.AppendLine("ME board[" + i + "] " + c.cardName + " " + c.attack + "/" + c.hp + " atk=" + c.canAttack + " prov=" + c.provoc);
        }

        sb.AppendLine("ENEMY hero=" + s.enemy.heroHp + " mana=" + s.enemy.mana + "/" + s.enemy.maxMana + " hand=" + s.enemy.hand.Count + " deck=" + s.enemy.deck.Count);
        for (int i = 0; i < s.enemy.board.Count; i++)
        {
            var c = s.enemy.board[i];
            sb.AppendLine("ENEMY board[" + i + "] " + c.cardName + " " + c.attack + "/" + c.hp + " atk=" + c.canAttack + " prov=" + c.provoc);
        }

        return sb.ToString();
    }
}