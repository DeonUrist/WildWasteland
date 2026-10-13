using UnityEngine;
using UnityEngine.SceneManagement;

namespace WildWasteland.Core
{
    /// <summary>
    /// The single hidden GameObject that hosts every WW loop (AGENTS.md 5.2). Three MonoBehaviours with different
    /// script execution orders give the scheduler its Early / normal / Last phases. Recreated on scene load if the game destroyed it.
    /// </summary>
    public sealed class Runner : MonoBehaviour
    {
        public static Runner Instance { get; private set; }
        public static GameObject Host { get; private set; }

        public static void Ensure()
        {
            if (Instance != null) return;
            Host = new GameObject("WildWasteland.Runner");
            Host.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(Host);
            Host.AddComponent<EarlyRunner>();
            Instance = Host.AddComponent<Runner>();
            Host.AddComponent<LateRunner>();
        }

        void Update() { Scheduler.Run(Phase.Update); }
        void LateUpdate() { Scheduler.Run(Phase.LateUpdate); }
        void FixedUpdate() { Scheduler.Run(Phase.FixedUpdate); }
        void OnGUI() { Scheduler.Run(Phase.Gui); UI.ToolWindow.Draw(); }
        void OnApplicationQuit() { ModuleHost.Quit(); }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }

    [DefaultExecutionOrder(-1000)]
    internal sealed class EarlyRunner : MonoBehaviour
    {
        void Update() { Scheduler.Run(Phase.EarlyUpdate); }
        void FixedUpdate() { Scheduler.Run(Phase.EarlyFixedUpdate); }
    }

    [DefaultExecutionOrder(20000)]
    internal sealed class LateRunner : MonoBehaviour
    {
        void LateUpdate() { Scheduler.Run(Phase.LateUpdateLast); }
    }
}
