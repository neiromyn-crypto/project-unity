using System;
using System.IO;
using DawnGuard.Core;
using DawnGuard.Unity;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DawnGuard.BlackwoodV2
{
    public sealed class BlackwoodRoot : MonoBehaviour
    {
        public GameCatalog catalog;
        public Texture2D menuBackdrop;
        public Transform sceneEnvironment;
        public GameObject startingPreview;
        public GameObject pinePrefab, rubblePrefab, padPrefab;
        public BlackwoodLightingProfile lightingProfile;
        [Header("Scene composition")]
        public Vector3 cameraFocus=new Vector3(8,0,7.5f);
        [Range(5,14)] public float cameraSize=9.6f;
        [Range(25,65)] public float cameraElevation=50.5f;
        public bool HasSavedCampaign { get; private set; }
        public GameSession Game { get; private set; }
        public BlackwoodHud Hud { get; private set; }
        public BlackwoodWorld World { get; private set; }
        public string SelectedBuild { get; private set; }
        public int SelectedBuilding { get; private set; }
        public bool MovingBuilding { get; private set; }
        public bool Paused { get; private set; }
        public bool Completed { get; private set; }
        public string Message { get; private set; }
        private SaveService save;
        private float accumulator, autosaveTimer;
        private bool systemPause;
        // Automated Play checks use a disposable profile within the project.
#if UNITY_EDITOR
        public static string ValidationSavePath;
#endif

        private void Start()
        {
            try
            {
                if(catalog==null || catalog.rules==null) throw new InvalidOperationException("Create scene using Dawn Guard menu.");
                catalog.rules.Validate();
                if(startingPreview!=null) Destroy(startingPreview);
                Application.targetFrameRate=PlayerPrefs.GetInt("BlackwoodV2.FPS",60);
                Screen.orientation=ScreenOrientation.LandscapeLeft;
                string savePath=Path.Combine(Application.persistentDataPath,"blackwood-v2.json");
#if UNITY_EDITOR
                if(!string.IsNullOrEmpty(ValidationSavePath)) savePath=ValidationSavePath;
#endif
                save=new SaveService(savePath);
                var data=save.Load(catalog.rules);
                HasSavedCampaign=data!=null; Paused=true;
                Attach(data==null ? new GameSession(catalog.rules) : GameSession.Restore(catalog.rules,data.resume,data.dayStart));
                Completed=data!=null && data.completed;
                World=gameObject.AddComponent<BlackwoodWorld>(); World.Initialize(this,catalog);
                Hud=gameObject.AddComponent<BlackwoodHud>(); Hud.Initialize(this);
                Message=string.IsNullOrEmpty(save.Warning) ? "120 кредитов: пулемёт ИЛИ три стены. Проход на севере." : save.Warning;
            }
            catch(Exception ex) { Debug.LogException(ex); enabled=false; }
        }
        private void Attach(GameSession session)
        {
            if(Game!=null) { Game.Shot-=OnShot; Game.PhaseChanged-=OnPhase; }
            Game=session; Game.Shot+=OnShot; Game.PhaseChanged+=OnPhase;
        }
        private void OnShot(ShotEvent shot) { if(World!=null) World.ShowShot(shot); }
        private void OnPhase()
        {
            SelectedBuild=null; SelectedBuilding=0; MovingBuilding=false;
            Completed=Game.Phase==GamePhase.Victory;
            if(Game.Phase==GamePhase.Day) Message="Доход получен. Подготовьте следующую оборону.";
            else if(Game.Phase==GamePhase.Defeat) Message="Убежище разрушено. Можно переиграть подготовку.";
            else if(Completed) Message="Экспедиция завершена: "+Game.Rules.waves.Length+" ночей!";
            else Message=Game.Rules.waves[Game.Day-1].title;
            SaveNow();
        }
        private void Update()
        {
            if(Game==null || Hud==null) return;
            if(!Paused && !systemPause && !Completed)
            {
                Vector2 input=Hud.Stick.Value;
#if ENABLE_INPUT_SYSTEM
                var k=Keyboard.current;
                if(k!=null)
                {
                    input.x+=(k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0)-(k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
                    input.y+=(k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0)-(k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                input+=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
#endif
                input=Vector2.ClampMagnitude(input,1);
                accumulator=Mathf.Min(accumulator+Time.unscaledDeltaTime,0.25f);
                while(accumulator>=0.05f) { Game.Tick(0.05f,input.x,input.y); accumulator-=0.05f; }
                autosaveTimer+=Time.unscaledDeltaTime;
                if(autosaveTimer>=10 && Game.Phase==GamePhase.Day) { SaveNow(); autosaveTimer=0; }
            }
            World.Refresh(); Hud.Refresh();
        }
        public void SelectBuild(string id)
        {
            if(Game.Phase!=GamePhase.Day || Paused || Completed) return;
            SelectedBuild=id; SelectedBuilding=0; MovingBuilding=false;
            var definition=Game.Rules.Building(id);
            Message=id=="camp" ? "Лагерь: +"+definition.dailyIncome+" за рассвет. Окупаемость "+Mathf.CeilToInt((float)definition.cost/definition.dailyIncome)+" выплаты. Осталось выплат: "+(Game.Rules.waves.Length-Game.Day) : "Выбрано: "+definition.title+". Нажмите свободную клетку.";
        }
        public void HandleBoardClick(Vector2 screen)
        {
            if(Paused || systemPause || Completed) return;
            Vector3 point;
            if(!World.TryGround(screen,out point)) return;
            var cell=new Cell(Mathf.FloorToInt(point.x),Mathf.FloorToInt(point.z));
            if(Game.Phase==GamePhase.Day)
            {
                string error;
                if(MovingBuilding)
                {
                    bool ok=Game.Construction.TryMove(SelectedBuilding,cell,out error);
                    Message=ok ? "Здание перемещено" : error;
                    if(ok) MovingBuilding=false;
                }
                else if(!string.IsNullOrEmpty(SelectedBuild))
                {
                    bool ok=Game.Construction.TryBuild(SelectedBuild,cell,out error);
                    Message=ok ? "Построено. Можно поставить ещё или нажать «Выбор»." : error;
                }
                else { SelectedBuilding=Game.Board.Occupant(cell); Message=SelectedBuilding>0 ? "Здание выбрано" : "Выберите стену или башню"; }
                SaveNow();
            }
            else if(Game.Phase==GamePhase.Night)
            {
                float best=1.5f*1.5f; int id=0;
                foreach(var e in Game.Enemies)
                {
                    float dx=e.x-point.x,dz=e.z-point.z,d=dx*dx+dz*dz;
                    if(d<best) { best=d; id=e.instanceId; }
                }
                Game.PriorityEnemyId=id; Message=id==0 ? "Автоматический выбор цели" : "Приоритетная цель назначена";
            }
        }
        public void PreviewAt(Vector2 screen)
        { if(World!=null) World.Preview(screen,SelectedBuild,MovingBuilding ? SelectedBuilding : 0); }
        public void ClearSelection() { SelectedBuild=null; SelectedBuilding=0; MovingBuilding=false; Message="Нажмите здание для выбора"; }
        public void StartNight() { if(!Paused && !Completed) { Game.StartNight(); Hud.Stick.ResetInput(); } }
        public void TogglePause() { Paused=!Paused; Hud.Stick.ResetInput(); accumulator=0; SaveNow(); }
        public void MoveSelected() { if(Game.Phase==GamePhase.Day && !Paused) { MovingBuilding=true; SelectedBuild=null; Message="Нажмите новое место"; } }
        public void UpgradeSelected() { string e; Report(Game.Construction.TryUpgrade(SelectedBuilding,out e),e,"Здание улучшено"); }
        public void RepairSelected() { string e; Report(Game.Construction.TryRepair(SelectedBuilding,out e),e,"Здание отремонтировано"); }
        public void SellSelected() { string e; Report(Game.Construction.TrySell(SelectedBuilding,out e),e,"Здание разобрано"); SelectedBuilding=0; }
        public void Research() { string e; Report(Game.Construction.TryResearch(out e),e,"Тесла открыта"); }
        public void UpgradeDrone() { string e; Report(Game.Construction.TryUpgradeDrone(out e),e,"Дрон улучшен"); }
        public void Rocket() { if(Paused || systemPause || Completed) return; string e; Report(Game.Combat.TryRocket(Game,out e),e,"Ракета!"); }
        public void Boost() { if(Paused || systemPause || Completed) return; string e; Report(Game.Combat.TryBoost(Game,out e),e,"Башня усилена: 5 секунд"); }
        private void Report(bool ok,string error,string success) { Message=ok ? success : error; if(Game.Phase==GamePhase.Day) SaveNow(); }
        public void Retry()
        {
            Attach(GameSession.Restore(catalog.rules,Game.DayStart));
            Completed=Paused=false; accumulator=0; Hud.Stick.ResetInput();
            ClearSelection(); World.RebuildUnits(); SaveNow();
            Message="Начало этого дня. Попробуйте другой план.";
        }
        public void NewGame()
        {
            try { save.BeginNewGame(); }
            catch(Exception ex) { Message="Не удалось начать новую игру: "+ex.Message; return; }
            Attach(new GameSession(catalog.rules));
            Completed=Paused=false; accumulator=0; Hud.Stick.ResetInput();
            ClearSelection(); World.RebuildUnits(); SaveNow(); HasSavedCampaign=true;
        }
        private void SaveNow()
        {
            if(save==null || Game==null) return;
            if(save.Save(Game,Completed)) HasSavedCampaign=true; else Message=save.Warning;
        }
        private void OnApplicationPause(bool paused)
        { systemPause=paused; accumulator=0; if(Hud!=null) Hud.Stick.ResetInput(); if(paused) SaveNow(); }
        private void OnApplicationFocus(bool focus)
        { if(!focus) { Paused=true; if(Hud!=null) Hud.Stick.ResetInput(); SaveNow(); } }
        private void OnApplicationQuit() { SaveNow(); }
        private void OnDestroy()
        { if(Game!=null) { Game.Shot-=OnShot; Game.PhaseChanged-=OnPhase; } }
    }
}
