using System;
using System.IO;
using System.Text;
using DawnGuard.Core;
using UnityEngine;

namespace DawnGuard.Unity
{
    [Serializable]
    public sealed class SaveEnvelope
    {
        public int version=1;
        public bool completed;
        public DaySnapshot resume, dayStart;
    }

    public sealed class SaveService
    {
        private readonly string path;
        public bool CanWrite { get; private set; } = true;
        public string Warning { get; private set; } = "";
        public SaveService(string path) { this.path=path; }
        public SaveEnvelope Load(GameRules rules)
        {
            if(!File.Exists(path) && !File.Exists(path+".bak")) return null;
            foreach(string candidate in new[] {path,path+".bak"})
            {
                try
                {
                    if(!File.Exists(candidate)) continue;
                    var result=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(candidate));
                    if(result==null || result.version!=1) throw new InvalidDataException("Save version.");
                    var checkedGame=GameSession.Restore(rules,result.resume,result.dayStart);
                    if(result.completed && checkedGame.Day!=rules.waves.Length)
                        throw new InvalidDataException("Completion marker.");
                    if(candidate!=path) Warning="Основное сохранение повреждено. Загружена резервная копия.";
                    return result;
                }
                catch(Exception ex) { Warning="Сохранение не прочитано: "+ex.Message; }
            }
            CanWrite=false;
            Warning+=" Автозапись отключена. «Новая игра» создаст новый прогресс.";
            return null;
        }

        public bool Save(GameSession game,bool completed)
        {
            if(!CanWrite) return false;
            try
            {
                var data=new SaveEnvelope { completed=completed,
                    resume=game.Phase==GamePhase.Day ? game.Snapshot() : game.DayStart,
                    dayStart=game.DayStart };
                string directory=Path.GetDirectoryName(path);
                if(!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(data,true));
                using(var stream=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None))
                { stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
                if(File.Exists(path))
                {
                    try { File.Replace(path+".tmp",path,path+".bak"); }
                    catch(PlatformNotSupportedException) { FallbackReplace(); }
                    catch(NotSupportedException) { FallbackReplace(); }
                }
                else File.Move(path+".tmp",path);
                return true;
            }
            catch(Exception ex) { Warning="Не удалось сохранить: "+ex.Message; Debug.LogWarning(Warning); return false; }
        }
        private void FallbackReplace()
        {
            File.Copy(path,path+".bak",true);
            File.Copy(path+".tmp",path,true);
            File.Delete(path+".tmp");
        }
        public void BeginNewGame()
        {
            // Preserve existing data under a unique archive name; explicit New Game action only.
            string suffix=".old-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            if(File.Exists(path)) File.Move(path,path+suffix);
            if(File.Exists(path+".bak")) File.Move(path+".bak",path+".bak"+suffix);
            CanWrite=true; Warning="";
        }
    }
}
