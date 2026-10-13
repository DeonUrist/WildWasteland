using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace WildWasteland
{
    /// <summary>
    /// Entry point (AGENTS.md 5.2): binds config, starts modules, applies the shared dispatchers and creates the Runner.
    /// The game destroys plugin GameObjects on scene load, so nothing runs on this component after Awake.
    /// The soft dependencies on the standalone mods only force them to load first, so the conflict check (5.8) sees them.
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("com.denis.apocalypter.apocasetter", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocaplayer", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocaclimber", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.npcai", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.gunplay", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocaunloader", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.gunplayhud", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocapatrol", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.warrig", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.merchantspotter", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocaraider", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.motorcyclemod", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("apocalypter.dogcompanion", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("local.apocalypter.duststorm", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.apocaspawner", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.denis.apocalypter.partadjustment", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.denis.apocalypter.wildwasteland";
        public const string NAME = "Wild Wasteland";
        public const string VERSION = "0.1.0";

        public static Plugin Instance;
        public static ConfigFile Main, Tools;
        public static ConfigEntry<Key> ToolWindowKey;
        public static ConfigEntry<bool> VerboseLog;
        public static ConfigEntry<float> FrameBudgetMs;

        void Awake()
        {
            Instance = this;
            Core.Log.Source = Logger;
            try
            {
                Core.Assets.Init(Info.Location);
                Core.UI.Skin.Init(Info.Location);
                Core.Legacy.FirstRunMain = !File.Exists(Config.ConfigFilePath);
                var toolsPath = Path.Combine(Core.Assets.ConfigDir, "tools.cfg");
                Directory.CreateDirectory(Core.Assets.ConfigDir);
                Core.Legacy.FirstRunTools = !File.Exists(toolsPath);
                Main = Config;
                Tools = new ConfigFile(toolsPath, true);

                // F6 (Apocasetter) page order: General, Keys, one section per module, Debug.
                Main.Bind("General", "Apocasetter", true, "Lists Wild Wasteland in the Apocasetter Mods menu (F6).");
                ToolWindowKey = Main.Bind("Keys", "Tool window", Key.F7,
                    "Opens the Wild Wasteland tool window: every debug, editor and technical setting of all modules (turret spots, seat poses, merchant layouts, patrol editor, AI tuning, performance).");

                RegisterModules();
                Core.ModuleHost.BindAll(Main, Tools);

                VerboseLog = Main.Bind("Debug", "VerboseLog", false, "Detailed info logging for every module (off for normal play).");
                FrameBudgetMs = Tools.Bind("Core", "Frame budget (ms)", 2f, new ConfigDescription("Sliced background work (bakes, scans) stops for the frame once Wild Wasteland used this much time.", new AcceptableValueRange<float>(0.5f, 8f)));
                Core.Log.Verbose = VerboseLog.Value;
                VerboseLog.SettingChanged += (s, a) => Core.Log.Verbose = VerboseLog.Value;
                Core.Scheduler.FrameBudgetMs = FrameBudgetMs.Value;
                FrameBudgetMs.SettingChanged += (s, a) => Core.Scheduler.FrameBudgetMs = FrameBudgetMs.Value;

                Core.Runner.Ensure();
                RegisterCoreJobs();
                Core.SaveTypes.Install();
                Core.UI.ToolWindow.Init();
                SceneManager.sceneLoaded += OnSceneLoaded;

                Core.ModuleHost.StartAll();
                Core.Log.Always(Core.ModuleHost.Summary());
            }
            catch (Exception e)
            {
                Core.Log.Error("Wild Wasteland failed to start", e);
            }
        }

        /// <summary>Module order = init order = config display order. Providers before consumers.</summary>
        static void RegisterModules()
        {
            Core.ModuleHost.Add(new Modules.PlayerBody.PlayerBodyModule());
            Core.ModuleHost.Add(new Modules.NPC.NpcModule());
            Core.ModuleHost.Add(new Modules.Guns.GunsModule());
            Core.ModuleHost.Add(new Modules.HUD.HudModule());
            Core.ModuleHost.Add(new Modules.Patrols.PatrolsModule());
        }

        static void RegisterCoreJobs()
        {
            Core.Scheduler.EveryFrame(null, "Core.Frame", CoreFrame, Core.Phase.EarlyUpdate);
        }

        static void CoreFrame()
        {
            Core.UI.EditorSession.Tick();
            Core.SaveBus.Tick();
            Core.Hotkeys.Tick();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            Core.Runner.Ensure();
            Core.GameRefs.OnSceneLoaded(s, m);
            Core.Origin.Reset();
            Core.SaveBus.OnSceneLoaded();
            Core.UI.EditorSession.CancelAll();
            Core.Log.ResetRateLimits();
            Core.ModuleHost.SceneLoaded(s, m);
        }
    }
}
