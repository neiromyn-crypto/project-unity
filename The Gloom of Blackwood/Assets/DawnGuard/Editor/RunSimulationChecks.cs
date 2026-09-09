using DawnGuard.Core.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace DawnGuard.Editor
{
    public static class RunSimulationChecks
    {
        [MenuItem("Dawn Guard/2 - Run core checks")]
        public static void Run() { Debug.Log(SimulationChecks.RunAll()); }
    }
}
