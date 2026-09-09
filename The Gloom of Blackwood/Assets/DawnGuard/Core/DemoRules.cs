using System.Collections.Generic;

namespace DawnGuard.Core
{
    public static class DemoRules
    {
        public static GameRules Create()
        {
            var rocks = new List<Cell>();
            for (int x = 0; x < 16; x++)
                if (x < 6 || x > 8) rocks.Add(new Cell(x, 10));
            return new GameRules {
                blockedCells = rocks.ToArray(),
                spawnCells = new[] { new Cell(6, 14), new Cell(7, 14), new Cell(8, 14) },
                buildings = new[] {
                    new BuildingDefinition { id="shelter", title="УБЕЖИЩЕ", role=BuildingRole.Shelter,
                        width=2, depth=2, maxHealth=600, maxLevel=1 },
                    new BuildingDefinition { id="camp", title="ЛАГЕРЬ", role=BuildingRole.Camp,
                        width=2, depth=2, cost=180, maxHealth=180, dailyIncome=40, upgradeCost=140, requiredDay=2 },
                    new BuildingDefinition { id="generator", title="ЭНЕРГИЯ", role=BuildingRole.Generator,
                        width=2, depth=2, cost=180, maxHealth=220, powerSupply=8, upgradeCost=120, requiredDay=2 },
                    new BuildingDefinition { id="wall", title="СТЕНА", role=BuildingRole.Wall,
                        cost=40, maxHealth=180, upgradeCost=50 },
                    new BuildingDefinition { id="gun", title="ПУЛЕМЁТ", role=BuildingRole.Turret,
                        cost=120, maxHealth=140, range=5, damage=9, shotsPerSecond=2, upgradeCost=100 },
                    new BuildingDefinition { id="lab", title="ЛАБОРАТОРИЯ", role=BuildingRole.Laboratory,
                        width=2, depth=2, cost=160, maxHealth=160, powerUse=2, requiredDay=2, maxLevel=1 },
                    new BuildingDefinition { id="tesla", title="ТЕСЛА", role=BuildingRole.Turret,
                        cost=200, maxHealth=140, range=4.5f, damage=25, shotsPerSecond=1.2f,
                        powerUse=4, requiredTech=1, requiredDay=3, upgradeCost=140 }
                },
                enemies = new[] {
                    new EnemyDefinition { id="walker", title="ЗОМБИ", health=48, speed=0.75f,
                        damagePerSecond=10, reward=4 },
                    new EnemyDefinition { id="runner", title="БЕГУН", health=32, speed=1.35f,
                        damagePerSecond=8, reward=5, size=0.5f },
                    new EnemyDefinition { id="brute", title="ГРОМИЛА", health=260, speed=0.55f,
                        damagePerSecond=28, reward=25, size=1.1f }
                },
                waves = new[] {
                    Wave("Первая атака: северный проход", 50, Group("walker", 8, 2, 1.4f)),
                    Wave("Бегуны", 55, Group("walker", 10, 1, 2.5f), Group("runner", 4, 17, 3)),
                    Wave("Первый громила", 65, Group("walker", 12, 1, 2), Group("brute", 1, 20, 1)),
                    Wave("Смешанная стая", 70, Group("walker", 14, 1, 2), Group("runner", 8, 12, 2)),
                    Wave("Последняя ночь прототипа", 80, Group("walker", 16, 1, 2),
                        Group("runner", 8, 18, 2), Group("brute", 3, 30, 6))
                }
            };
        }
        private static WaveDefinition Wave(string title, float duration, params SpawnGroup[] groups)
        { return new WaveDefinition { title=title, duration=duration, groups=groups }; }
        private static SpawnGroup Group(string id, int count, float start, float interval)
        { return new SpawnGroup { enemyId=id, count=count, startAt=start, interval=interval }; }
    }
}
