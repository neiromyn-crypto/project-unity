using System;
using System.Collections.Generic;
using DawnGuard.Core;
using DawnGuard.BlackwoodV2;

public static class BlackwoodConceptAudit
{
    public static string Run()
    {
        var lines = new List<string>();
        foreach (var scenario in new[] { "no_build", "three_walls", "one_gun", "no_build_no_drone" })
        {
            var rules = BlackwoodBalance.Create();
            var game = new GameSession(rules);
            // Diagnostic only: disable damage after validating the unchanged current preset.
            if (scenario == "no_build_no_drone") rules.droneDamage = 0;
            string error;
            if (scenario == "three_walls")
                for (int x = 6; x <= 8; x++)
                    if (!game.Construction.TryBuild("wall", new Cell(x, 9), out error)) throw new Exception(error);
            if (scenario == "one_gun" && !game.Construction.TryBuild("gun", new Cell(7, 9), out error)) throw new Exception(error);
            game.StartNight();
            int steps = 0, lastAlive = 0;
            while (game.Phase == GamePhase.Night && steps < 4000)
            {
                lastAlive = game.Enemies.Count;
                game.Tick(.05f);
                steps++;
            }
            lines.Add(scenario + ": phase=" + game.Phase + ", day=" + game.Day +
                ", shelterHP=" + (game.Shelter == null ? 0 : game.Shelter.health) +
                ", elapsed=" + (steps * .05f) + ", alive_before_final_tick=" + lastAlive +
                ", alive_after=" + game.Enemies.Count + ", credits=" + game.Wallet.Credits);
        }
        return string.Join(Environment.NewLine, lines);
    }
}
