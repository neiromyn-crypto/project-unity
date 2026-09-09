using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DawnGuard.BlackwoodV2.Editor
{
    [InitializeOnLoad]
    public static class BlackwoodBatchValidation
    {
        static BlackwoodBatchValidation()
        {
            EditorApplication.update+=Monitor;
        }
        public static void Run()
        {
            try
            {
                Directory.CreateDirectory("IntegrationEvidence");
                BlackwoodLocalAutomation.Inventory();
                EditorSceneManager.OpenScene("Assets/DawnGuard/Generated/DawnGuardDemo.unity");
                BlackwoodUserAssetSetup.Integrate();
                BlackwoodIntegrationChecks.Run();
                SessionState.SetBool("Blackwood.Batch",true);
                SessionState.SetBool("Blackwood.VerifyPlay",true);
                SessionState.SetString("Blackwood.BatchDeadline",DateTime.UtcNow.AddMinutes(8).ToString("O"));
                EditorApplication.isPlaying=true;
            }
            catch(Exception ex)
            {
                File.WriteAllText("IntegrationEvidence/batch-result.txt","FAILED\n"+ex); Debug.LogException(ex);
                if(Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
        static void Monitor()
        {
            if(!Application.isBatchMode || !SessionState.GetBool("Blackwood.Batch",false)) return;
            if(File.Exists("IntegrationEvidence/play-result.txt") && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                string result=File.ReadAllText("IntegrationEvidence/play-result.txt");
                File.WriteAllText("IntegrationEvidence/batch-result.txt",result);
                SessionState.SetBool("Blackwood.Batch",false); EditorApplication.Exit(result.StartsWith("PASS") ? 0 : 1);
            }
            else if(DateTime.UtcNow>DateTime.Parse(SessionState.GetString("Blackwood.BatchDeadline",DateTime.UtcNow.ToString("O"))))
            {
                File.WriteAllText("IntegrationEvidence/batch-result.txt","FAILED: Play Mode timeout");
                EditorApplication.Exit(2);
            }
        }
    }
}
