using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DawnGuard.BlackwoodLTD
{
    public sealed class CoastRoot : MonoBehaviour
    {
        public CoastCatalog catalog;
        public CoastSession Game {get;private set;}
        public CoastWorld World {get;private set;}
        public CoastHud Hud {get;private set;}
        public CoastAudio Audio {get;private set;}
        public bool MenuOpen {get;private set;}=true;
        public bool ManualPause {get;private set;}
        public bool Paused {get{return MenuOpen||ManualPause;}}
        public bool HasSave {get;private set;}
        public bool HasCheckpoint {get{return !string.IsNullOrEmpty(savePath)&&File.Exists(savePath+".day-start");}}
        public string SelectedTool {get;private set;}
        public int SelectedBuilding {get;private set;}
        public bool Moving {get;private set;}
        public string Notice {get;private set;}="Выберите барьер или защитника. Север отмечен стрелкой.";
        public static string TestSavePath;
        string savePath;
        float accumulator,saveTimer,noticeTime;
        public int WatchedFront;
#if UNITY_EDITOR
        public void InitializePreview(CoastCatalog assets)
        {catalog=assets;Game=new CoastSession(catalog.rules);MenuOpen=false;World=gameObject.AddComponent<CoastWorld>();World.Initialize(this,catalog);World.Refresh(0);}
#endif
        void Start()
        {
            Application.targetFrameRate=60;Application.runInBackground=true;Screen.sleepTimeout=SleepTimeout.NeverSleep;
            savePath=string.IsNullOrEmpty(TestSavePath)?Path.Combine(Application.persistentDataPath,"blackwood-coast-map2-v1.json"):TestSavePath;
            CoastState saved=null;
            try{if(File.Exists(savePath)){saved=JsonUtility.FromJson<CoastState>(File.ReadAllText(savePath));Game=new CoastSession(catalog.rules,saved);HasSave=true;}}
            catch(Exception e){Debug.LogWarning("Coast save not loaded: "+e.Message);Notice="Сохранение повреждено. Можно начать новую экспедицию.";}
            if(Game==null)Game=new CoastSession(catalog.rules);
            World=gameObject.AddComponent<CoastWorld>();World.Initialize(this,catalog);
            Audio=gameObject.AddComponent<CoastAudio>();Audio.Initialize(this);
            Hud=gameObject.AddComponent<CoastHud>();Hud.Initialize(this);
            Game.Event+=HandleEvent;
        }
        void HandleEvent(CoastEvent e){if(e.kind=="delivery")e.x+=Game.Map.CampOffsetX;World.OnEvent(e);Audio.OnEvent(e);if(e.kind=="delivery"&&Hud!=null)Hud.Float(e);}
        void Update()
        {
            if(Game==null)return;float dt=Mathf.Min(Time.unscaledDeltaTime,.15f);
#if ENABLE_INPUT_SYSTEM
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(Hud.ServiceOpen)Hud.CloseService();else if(SelectedTool!=null||Moving)ClearSelection();else if(MenuOpen)EnterGame();else TogglePause();}
            if(Keyboard.current!=null&&!MenuOpen&&!Hud.ServiceOpen)
            {
                var k=Keyboard.current;Vector2 move=new Vector2((k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0),(k.wKey.isPressed||k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed||k.downArrowKey.isPressed?1:0));
                World.CameraRig.Move(Vector2.ClampMagnitude(move,1),dt);
                if(k.homeKey.wasPressedThisFrame)World.CameraRig.SetFocus(new Vector3(Game.Map.CampX,0,10.5f));
                if(k.spaceKey.wasPressedThisFrame)FocusDrone();
            }
            if(Mouse.current!=null&&!MenuOpen){World.Preview(Mouse.current.position.ReadValue());if(Mouse.current.rightButton.wasPressedThisFrame)ClearSelection();float scroll=Mouse.current.scroll.ReadValue().y;if(Mathf.Abs(scroll)>.01f)World.Zoom(-Mathf.Sign(scroll)*.6f);}
#endif
            if(!Paused)
            {
                accumulator+=dt;while(accumulator>=.05f){Game.Tick(.05f);accumulator-=.05f;}
                saveTimer+=dt;if(saveTimer>8){Save();saveTimer=0;}
            }else accumulator=0;
            World.Refresh(dt);Hud.Refresh();Audio.Refresh(dt);
            if(noticeTime>0){noticeTime-=dt;if(noticeTime<=0)Notice="";}
        }
        public void Toast(string message){Notice=message;noticeTime=6;}
        public void FocusDrone(){if(!MenuOpen&&!Hud.ServiceOpen)World.FocusDrone();}
        public void Act(Func<bool> action){if(Paused){Toast("Вернитесь в игру, чтобы выполнить действие");return;}bool ok=action();Toast(ok?"Готово":Game.LastError);if(ok){Save();Audio.Click();}Hud.Refresh();}
        public void ChooseTool(string kind){SelectedTool=kind;SelectedBuilding=0;Moving=false;Hud.CloseService();Toast(catalog.rules.Defense(kind).description);}
        public void ClearSelection(){SelectedTool=null;SelectedBuilding=0;Moving=false;World.ClearPreview();}
        public void ClickBoard(Vector2 screen)
        {
            if(Paused||Hud.ServiceOpen)return;Vector3 p;if(!World.TryGround(screen,out p))return;
            int x=Mathf.FloorToInt(p.x),z=Mathf.FloorToInt(p.z);
            if(SelectedTool!=null&&Game.S.phase==CoastPhase.Day){string kind=SelectedTool;Act(()=>Game.Build(kind,x,z));return;}
            if(Moving&&SelectedBuilding>0){int id=SelectedBuilding;Act(()=>Game.Move(id,x,z));Moving=false;return;}
            if(Game.Map.InBounds(x,z)&&Game.Map.Occupancy[x,z]>0){SelectedBuilding=Game.Map.Occupancy[x,z];SelectedTool=null;Hud.ShowSelection();return;}
            if(z<8&&z>1){float serviceX=p.x-Game.Map.CampOffsetX;Hud.OpenService(serviceX<6?"barracks":serviceX<11?"armory":serviceX<17?"camp":serviceX<23?"lab":"power");return;}
            SelectedBuilding=0;
        }
        public void MoveSelected(){if(Game.S.phase==CoastPhase.Day&&SelectedBuilding>0){Moving=true;Toast("Выберите новое место. Перенос использованной защиты стоит камень.");}}
        public void EnterGame(){MenuOpen=false;ManualPause=false;Hud.CloseService();World.ClearPreview();}
        public void OpenMenu(){MenuOpen=true;ManualPause=false;Save();Hud.CloseService();}
        public void TogglePause(){ManualPause=!ManualPause;accumulator=0;Save();}
        public void NewGame()
        {
            Game.Event-=HandleEvent;Game=new CoastSession(catalog.rules);Game.Event+=HandleEvent;World.ResetUnits();ClearSelection();MenuOpen=false;ManualPause=false;Hud.CloseService();Save();SaveCheckpoint();Toast("Ночь 1: север. Барьеры должны создавать повороты в радиусе башни.");
        }
        public void NextDay(){Game.ContinueDay();ClearSelection();Save();SaveCheckpoint();}
        void SaveCheckpoint(){try{File.WriteAllText(savePath+".day-start",JsonUtility.ToJson(Game.S,true));}catch(Exception e){Debug.LogWarning("Checkpoint: "+e.Message);}}
        public void RetryDay()
        {
            if(!HasCheckpoint){NewGame();return;}
            try{var restored=new CoastSession(catalog.rules,JsonUtility.FromJson<CoastState>(File.ReadAllText(savePath+".day-start")));Game.Event-=HandleEvent;Game=restored;Game.Event+=HandleEvent;World.ResetUnits();ClearSelection();MenuOpen=false;ManualPause=false;Hud.CloseService();accumulator=0;Save();Toast("Начало дня восстановлено. Измените оборону перед новой попыткой.");}
            catch(Exception e){Toast("Не удалось восстановить день: "+e.Message);}
        }
        public void Save()
        {
            if(Game==null||string.IsNullOrEmpty(savePath))return;
            try{string dir=Path.GetDirectoryName(savePath);Directory.CreateDirectory(dir);string temp=savePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(Game.S,true));
                if(File.Exists(savePath))File.Replace(temp,savePath,savePath+".bak");else File.Move(temp,savePath);HasSave=true;}
            catch(Exception e){Toast("Не удалось сохранить: "+e.Message);Debug.LogWarning(e);}
        }
        void OnApplicationPause(bool value){if(value){ManualPause=true;Save();}}
        void OnApplicationFocus(bool value){if(!value&&!MenuOpen){ManualPause=true;Save();}}
        void OnApplicationQuit(){Save();}
        void OnDestroy(){if(Game!=null)Game.Event-=HandleEvent;}
        public void Quit(){Save();Application.Quit();}
    }
    public sealed class CoastPointer : MonoBehaviour,IPointerClickHandler,IPointerMoveHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        public CoastRoot root;bool dragging;float released;
        public void OnPointerClick(PointerEventData e){if(!dragging&&Time.unscaledTime-released>.1f)root.ClickBoard(e.position);}
        public void OnPointerMove(PointerEventData e){if(!root.MenuOpen)root.World.Preview(e.position);}
        public void OnBeginDrag(PointerEventData e){dragging=true;}
        public void OnDrag(PointerEventData e){if(!root.MenuOpen&&!root.Hud.ServiceOpen)root.World.Pan(e.delta);}
        public void OnEndDrag(PointerEventData e){dragging=false;released=Time.unscaledTime;}
    }
}
