using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.PlayerBody
{
    // F7 tool window pages of the PlayerBody module (group "Player body & animation"). Live state on top, then the module's F6 toggles
    // that belong to the page and its F7 (tools.cfg) settings. Everything written here is live (SettingChanged / read every frame).
    internal static class PlayerBodyPages
    {
        const string G = ToolGroups.Player;
        static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        static string YesNo(bool b) { return b ? "yes" : "no"; }

        internal static IEnumerable<ToolPage> Create(PlayerBodyModule m)
        {
            var tools = m.Config.Tools;
            return new ToolPage[]
            {
                new ActionPage(m, G, "Animation framework", Framework),
                new ActionPage(m, G, "Body & model", BodyPage),
                new ActionPage(m, G, "Camera", CameraPage),
                new ActionPage(m, G, "Climbing", ClimbingPage),
                new ActionPage(m, G, "Weapon pose editor", PoseEditor) { ShowWhenModuleOff = false },
                new ConfigPage(m, G, "Car seat poses", tools, "PlayerBody / Car seat")
                {
                    Intro = "The driver's seat: your character is posed from the car's seated driver (PlayerModel_Sit, the game's own driving pose, retargeted bone by bone). "
                          + "SeatDrop lowers the body for the steering wheel; the first-person driving offsets move the body under the camera (added to the built-in base -0.03, 0.02, 0.02 m); "
                          + "CarCameraForward draws the first-person view a little in front of the eye when the body is shown. Passengers are not posed by this module."
                },
                new ConfigPage(m, G, "First person", tools, "PlayerBody / First person")
                { Intro = "First-person body placement under the camera (BodyFirstPerson / FirstPersonArms) and the jump knee correction." },
                new ConfigPage(m, G, "Animation tuning", tools, "PlayerBody / Animation", "PlayerBody / Animation (fallback clips)")
                { Intro = "Locomotion tuning of the player's body (and every ModAPI character, which reads the same values when it is attached). The fallback clips are only used without the animation bundle." },
                new ConfigPage(m, G, "Vehicles & ModAPI", tools, "PlayerBody / ModAPI")
                { Intro = "Vehicle hints read the game's Headlight and Handbrake Controls bindings. Cassette shortcuts are in F6 [Keys]; ignition is in the game's Controls screen (Ignition)." },
            };
        }

        // ------------------------------------------------------------------ Animation framework
        static void Framework(ToolLayout l)
        {
            l.Text("The Mixamo animation set of the player's body, offered to other modules as Core.Services.Animation (ex Apocaplayer ModAPI): NPC gunmen and patrol turret riders are animated with it.");
            l.Heading("State");
            l.Value("Service registered", YesNo(Services.Animation != null));
            l.Value("API version / source", ModAPI.ApiVersion + " / Apocaplayer " + ModAPI.PluginVersion);
            string bundle = PlayerBodyModule.ModPath(PlayerBodyModule.AnimBundleFile.Value);
            l.Value("Animation bundle", !Anims.Tried ? "not loaded yet (on first use)" : Anims.BundleOpen ? "loaded, " + Anims.Count + " clips" : File.Exists(bundle) ? "FAILED to open (see log)" : "missing: " + bundle);
            l.Value("Ready (player set present)", YesNo(ModAPI.Ready));
            l.Value("Clips offered", ModAPI.Ready ? ModAPI.ClipNames().Length.ToString(CultureInfo.InvariantCulture) : "-");
            l.Value("Attached characters", ModAPI.Count.ToString(CultureInfo.InvariantCulture));
            int shown = 0;
            for (int i = 0; i < ModAPI.Count && shown < 24; i++)
            {
                var c = ModAPI.CharacterAt(i);
                if (c == null || c.Animator == null) continue;
                shown++;
                l.Value("  " + c.Animator.gameObject.name, (c.Suspended ? "suspended" : c.WeaponKind) + (c.WeaponKey != "" ? " (" + c.WeaponKey + ")" : "")
                    + (c.HandsClip != "" ? ", hands " + c.HandsClip : "") + (c.ActionClip != "" ? ", " + c.ActionClip : ""));
            }
            if (ModAPI.Count > shown) l.Text("(" + (ModAPI.Count - shown) + " more)");
            l.Heading("Settings");
            l.Entry(PlayerBodyModule.AnimBundleFile);
            l.Entry(PlayerBodyModule.VerboseModApi);
        }

        // ------------------------------------------------------------------ Body & model
        static void BodyPage(ToolLayout l)
        {
            var b = BodyRunner.PlayerBody;
            l.Heading("State");
            l.Value("Player found", YesNo(Game.Ready));
            l.Value("Body", b == null ? "not built (rebuilt every 2 s while missing)" : b.IsRagdoll ? "ragdoll (dead)" : "built");
            l.Value("Character / model", PlayerBodyModule.Character.Value + " / " + PlayerBodyModule.BodyModelFile);
            l.Value("Driver's seat", CarSeat.Seat != null ? CarSeat.Seat.root.name : "-");
            if (l.Button("Rebuild the body now", Game.Ready, false, 260)) BodyRunner.CharacterChanged();
            l.Heading("Features (F6)");
            l.Entry(PlayerBodyModule.Character);
            l.Toggle(PlayerBodyModule.BodyFirstPerson);
            l.Toggle(PlayerBodyModule.EmptyHandArms);
            l.Toggle(PlayerBodyModule.FemaleArms);
            l.Toggle(PlayerBodyModule.ReplaceDriver);
            l.Toggle(PlayerBodyModule.EquipmentOnBody);
            l.Toggle(PlayerBodyModule.PainSounds);
            l.Heading("Model files (F7)");
            l.Text("Relative to BepInEx\\plugins\\WildWasteland\\PlayerBody. A changed model is used from the next body build (Rebuild above).");
            l.Entry(PlayerBodyModule.ModelFile);
            l.Entry(PlayerBodyModule.TextureFile);
            l.Entry(PlayerBodyModule.ArmsTextureFile);
        }

        // ------------------------------------------------------------------ Camera
        static ConfigEntryBase[] _cam, _cull;
        static void CameraPage(ToolLayout l)
        {
            l.Heading("State");
            l.Value("Third person", ThirdPerson.On ? (ThirdPerson.Peek ? "on (binoculars / scope: first-person view)" : "on") : "off");
            l.Value("Orbiting / aim zoom", YesNo(ThirdPerson.Orbiting) + " / " + YesNo(ThirdPerson.AimZoom));
            l.Value("Dynamic crosshair", ThirdPerson.HasCrosshair ? "at " + ThirdPerson.CrosshairScreen.ToString("F0") : "centred");
            l.Value("Camera culling", !PlayerBodyModule.OcclusionPrototype.Value ? "off" : Cave.Inside ? "paused (in a cave)" : OcclusionCutaway.Blockers + " blocker(s), " + OcclusionCutaway.Renders + " extra views so far");
            l.Heading("Features (F6)");
            l.Toggle(PlayerBodyModule.ThirdPersonOnFoot);
            l.Toggle(PlayerBodyModule.ToggleMiddleMouse);
            l.Toggle(PlayerBodyModule.DynamicCrosshair);
            l.Toggle(PlayerBodyModule.PickAssistOn);
            l.Toggle(PlayerBodyModule.FirstPersonSwayOn);
            l.Toggle(PlayerBodyModule.SmoothAimDownSights);
            l.Toggle(PlayerBodyModule.OcclusionPrototype);
            l.Toggle(PlayerBodyModule.OcclusionInVehicle);
            l.Entry(PlayerBodyModule.ObserveKey);
            l.Heading("Camera (F7)");
            if (_cam == null) _cam = new ConfigEntryBase[] { PlayerBodyModule.ThirdDistance, PlayerBodyModule.ThirdCarDistance, PlayerBodyModule.ThirdHeight, PlayerBodyModule.ThirdShoulder,
                PlayerBodyModule.ThirdCarHeight, PlayerBodyModule.OrbitSpeed, PlayerBodyModule.EnableMMB, PlayerBodyModule.AdsTime, PlayerBodyModule.PickAssistRadius };
            foreach (var e in _cam) l.Entry(e);
            l.Heading("Camera culling (F7)");
            if (_cull == null) _cull = new ConfigEntryBase[] { PlayerBodyModule.OcclusionOpacity, PlayerBodyModule.OcclusionRadius };
            foreach (var e in _cull) l.Entry(e);
        }

        // ------------------------------------------------------------------ Climbing
        static void ClimbingPage(ToolLayout l)
        {
            var c = ClimbingController.Instance;
            l.Heading("State");
            l.Value("Climbing clips", ClimbingController.ClipsLoaded ? "loaded" : "MISSING (Models/apocaplayer_climbing.bundle) - climbing off");
            l.Value("Climbing now", YesNo(c != null && c.Climbing));
            l.Value("Can climb here", YesNo(ModAPI.PlayerCanTraverse));
            l.Value("Last climb", string.IsNullOrEmpty(ClimbingController.LastResult) ? "-" : ClimbingController.LastResult);
            l.Value("Last rejection", string.IsNullOrEmpty(ClimbingController.LastReject) ? "-" : ClimbingController.LastReject);
            l.Value("Last step-up rejection", AutoStepUp.LastReject.ToString(CultureInfo.InvariantCulture) + " (0 = stepped; 1 no landing, 2 height, 3-5 blocked)");
            l.Heading("Features (F6)");
            l.Toggle(PlayerBodyModule.ClimbingOn);
            l.Toggle(PlayerBodyModule.JumpTriggersClimbing);
            l.Toggle(PlayerBodyModule.AutomaticStepUp);
            l.Entry(PlayerBodyModule.ClimbKey);
            l.Heading("Tuning (F7)");
            l.Entry(PlayerBodyModule.ClimbMinHeight);
            l.Entry(PlayerBodyModule.ClimbMaxHeight);
            l.Entry(PlayerBodyModule.ClimbReach);
            l.Entry(PlayerBodyModule.ClimbWaistLimit);
            l.Entry(PlayerBodyModule.ClimbSpeed);
        }

        // ------------------------------------------------------------------ Weapon pose editor
        static string _msg = "";
        static void PoseEditor(ToolLayout l)
        {
            l.Text("Edits where each weapon sits in the character's right hand, per animation clip, and the aim lift of the aim / fire clips. "
                 + "Switch it on, close this window, go to third person and draw a weapon: the editor's line shows at the bottom of the screen. "
                 + "Its keys do nothing while it is off.");
            l.Toggle(PlayerBodyModule.WeaponAdjust, "Weapon pose editor on");
            l.Heading("Keys (while on, third person, weapon drawn)");
            l.Text("Numpad 8/2 up/down, 6/4 right/left, 7/1 forward/back (or muzzle up/down, right/left, roll when ROTATING).\n"
                 + "Numpad 5 / 0 / . / Enter = MOVING <-> ROTATING.  Numpad 9/3 = next / previous clip of the drawn weapon (played standing still).\n"
                 + "Numpad - = delete the clip's own pose.  Numpad / = copy the shown pose.  Numpad * = paste it into the current clip.\n"
                 + "Aiming (right mouse, or an aim clip picked with 9/3): Page Up/Down = lift the hands, Home/End = head tilt, Insert/Delete = hands forward.");
            l.Heading("Current");
            bool live = PlayerBodyModule.WeaponAdjust.Value && GunPose.CurrentFrame >= 0 && !string.IsNullOrEmpty(GunPose.CurrentWeapon);
            l.Value("Weapon", live ? GunPose.CurrentWeapon : "(none: draw a weapon in third person)");
            l.Value("Clip", live ? GunPose.CurrentPose + (GunPose.Selected >= 0 ? "  (selected " + (GunPose.Selected + 1) + "/" + GunPose.ClipCount + ")" : "  (what she plays)") : "-");
            var v = GunPose.CurrentShown;
            l.Value("Pose (cm, degrees)", live && v != null && v.Length >= 6 ? F(v[0]) + ", " + F(v[1]) + ", " + F(v[2]) + "  /  " + F(v[3]) + ", " + F(v[4]) + ", " + F(v[5]) : "-");
            l.Value("Mode", GunPose.RotateMode ? "ROTATING" : "MOVING");
            l.Value("Clipboard", GunPose.HasCopy ? "a pose is copied" : "empty");
            l.Value("Tables", GunPose.UserCount + " own poses, " + GunPose.BuiltinCount + " built in; " + AimLift.UserCount + " aim-lift entries");
            int hit = l.Buttons("Move / rotate", "Previous clip", "Next clip", "Back to playing");
            if (hit == 0) GunPose.RotateMode = !GunPose.RotateMode;
            else if (hit == 1) GunPose.StepClip(-1);
            else if (hit == 2) GunPose.StepClip(+1);
            else if (hit == 3) GunPose.ResetSelection();
            hit = l.Buttons("Copy pose", "Paste pose", "Delete clip's pose");
            if (hit == 0) { GunPose.CopyShown(); _msg = live ? "copied" : "nothing to copy"; }
            else if (hit == 1) _msg = GunPose.PasteCurrent() ? "pasted into " + GunPose.CurrentWeapon + " | " + GunPose.CurrentPose : "nothing copied / no weapon";
            else if (hit == 2) _msg = GunPose.DeleteCurrent() ? "deleted " + GunPose.CurrentWeapon + " | " + GunPose.CurrentPose : "no weapon / clip";
            hit = l.Buttons("SAVE now", "RESET (reload files)");
            if (hit == 0) { GunPose.SaveNow(); AimLift.Save(); _msg = "saved " + GunPose.FilePath + " and aim-lift.txt"; }
            else if (hit == 1) { GunPose.ReloadFiles(); AimLift.Load(PlayerBodyModule.DataDir); _msg = "reloaded from " + PlayerBodyModule.DataDir + " (unsaved edits dropped)"; }
            if (_msg != "") l.Text(_msg, Ledger.Accent);
            l.Text("Every save also writes BuiltinPoses.generated.cs next to weapon-poses.txt: the whole table as C#, to hard-code the poses in the mod.");
        }
    }
}
