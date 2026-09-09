using System;

namespace DawnGuard.Core
{
    public sealed class BuildingService
    {
        private readonly GameSession game;
        internal BuildingService(GameSession game) { this.game=game; }

        public bool TryBuild(string id, Cell cell, out string error)
        {
            error="";
            if(game.Phase!=GamePhase.Day) { error="Строительство доступно днём"; return false; }
            var def=game.Rules.Building(id);
            if(def.role==BuildingRole.Shelter) { error="Убежище уже существует"; return false; }
            if(game.Day<def.requiredDay || game.Tech<def.requiredTech)
            { error="Нужен день "+def.requiredDay+" / технология "+def.requiredTech; return false; }
            if(!game.Board.CanPlace(cell,def.width,def.depth))
            { error="Место занято или за границей поля"; return false; }
            foreach(var spawn in game.Rules.spawnCells)
                if(spawn.x>=cell.x && spawn.x<cell.x+def.width && spawn.z>=cell.z && spawn.z<cell.z+def.depth)
                { error="Нельзя занять точку появления врагов"; return false; }
            if(def.powerUse>game.PowerAvailable) { error="Не хватает мощности"; return false; }
            if(!game.Wallet.TrySpend(def.cost)) { error="Недостаточно кредитов"; return false; }
            game.AddBuilding(def,cell,false,def.cost);
            game.RefreshPower();
            return true;
        }

        public bool TryUpgrade(int id, out string error)
        {
            error="";
            var b=game.FindBuilding(id);
            if(game.Phase!=GamePhase.Day || b==null) { error="Выберите здание днём"; return false; }
            var def=game.Rules.Building(b.definitionId);
            if(b.level>=def.maxLevel) { error="Максимальный уровень"; return false; }
            int cost=def.upgradeCost*b.level;
            if(!game.Wallet.TrySpend(cost)) { error="Нужно кредитов: "+cost; return false; }
            b.paidCredits+=cost;
            b.level++;
            b.health=Math.Min(game.MaxHealth(b),b.health+def.maxHealth*0.5f);
            game.RefreshPower();
            return true;
        }

        public bool TryRepair(int id, out string error)
        {
            error="";
            var b=game.FindBuilding(id);
            if(game.Phase!=GamePhase.Day || b==null) { error="Ремонт доступен днём"; return false; }
            int cost=(int)Math.Ceiling((game.MaxHealth(b)-b.health)*0.15f);
            if(cost<=0) { error="Здание не повреждено"; return false; }
            if(!game.Wallet.TrySpend(cost)) { error="Ремонт: "+cost+" кредитов"; return false; }
            b.health=game.MaxHealth(b);
            return true;
        }

        public bool TrySell(int id, out string error)
        {
            error="";
            var b=game.FindBuilding(id);
            if(game.Phase!=GamePhase.Day || b==null || b.permanent)
            { error="Этот объект нельзя разобрать"; return false; }
            // Used structures always refund 50%; new healthy same-day structures refund 100%.
            bool fresh=b.purchasedDay==game.Day && b.health>=game.MaxHealth(b);
            game.Wallet.Add(fresh ? b.paidCredits : b.paidCredits/2);
            game.RemoveBuilding(b);
            return true;
        }

        public bool TryMove(int id, Cell cell, out string error)
        {
            error="";
            var b=game.FindBuilding(id);
            if(game.Phase!=GamePhase.Day || b==null || b.permanent)
            { error="Этот объект нельзя переместить"; return false; }
            var def=game.Rules.Building(b.definitionId);
            if(!game.Board.CanPlace(cell,def.width,def.depth,id))
            { error="Место занято"; return false; }
            foreach(var spawn in game.Rules.spawnCells)
                if(spawn.x>=cell.x && spawn.x<cell.x+def.width && spawn.z>=cell.z && spawn.z<cell.z+def.depth)
                { error="Нельзя занять точку появления врагов"; return false; }
            game.Board.Remove(id);
            b.cell=cell;
            game.Board.Place(id,cell,def.width,def.depth);
            return true;
        }

        public bool TryResearch(out string error)
        {
            error="";
            if(game.Phase!=GamePhase.Day || !game.HasPoweredLab())
            { error="Нужна работающая лаборатория днём"; return false; }
            if(game.Tech>=1) { error="Технология уже открыта"; return false; }
            if(!game.Wallet.TrySpend(120)) { error="Исследование: 120 кредитов"; return false; }
            game.SetTech(1);
            return true;
        }

        public bool TryUpgradeDrone(out string error)
        {
            error="";
            if(game.Phase!=GamePhase.Day || !game.HasPoweredLab())
            { error="Нужна работающая лаборатория днём"; return false; }
            if(game.DroneLevel>=3) { error="Максимальный уровень дрона"; return false; }
            int cost=100*game.DroneLevel;
            if(!game.Wallet.TrySpend(cost)) { error="Нужно кредитов: "+cost; return false; }
            game.SetDroneLevel(game.DroneLevel+1);
            return true;
        }
    }
}
