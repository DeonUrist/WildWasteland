using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Patrols
{
    internal sealed class QuestFollowers : IFollowerVehicles
    {
        public Type ResolveType(string name)
        {
            switch (name)
            {
                case "Apocapatrol.Plugin": return typeof(PatrolsModule);
                case "Apocapatrol.PassengerGuard": return typeof(PassengerGuard);
                case "Apocapatrol.Pose": return typeof(Pose);
                default: return null;
            }
        }
        public bool IsCrew(GameObject npc)
        { return npc != null && (npc.GetComponent<SimpleBanditTag>() != null || npc.GetComponent<Pose>() != null || (Settled.Count > 0 && Settled.Contains(npc.GetInstanceID()))); }
        // foot patrol walkers that became camp residents (their crew tags dropped): still former crew for the quest restrictions
        private static readonly HashSet<int> Settled = new HashSet<int>();
        internal static void FormerCrew(GameObject npc) { if (npc != null) Settled.Add(npc.GetInstanceID()); }
        // NPCs Quests has driven as hired followers (Animated/Combat calls): CrewReturn and foot patrols leave them alone. Instance ids, never
        // cleared during a session (a hired former crew member stays excluded); a handful of entries at most.
        private static readonly HashSet<int> Hired = new HashSet<int>();
        internal static bool IsHired(GameObject npc) { return npc != null && Hired.Count > 0 && Hired.Contains(npc.GetInstanceID()); }
        public void Animated(GameObject npc, bool enabled)
        {
            if (npc != null) Hired.Add(npc.GetInstanceID());
            var pose = npc != null ? npc.GetComponent<Pose>() : null;
            if (pose != null) pose.AnimatedFollower = enabled;
        }
        public void Combat(GameObject npc, GameObject target, float blockedUntil)
        {
            if (npc != null) Hired.Add(npc.GetInstanceID());
            var guard = npc != null ? npc.GetComponent<PassengerGuard>() : null;
            if (guard != null) guard.QuestTarget(target, blockedUntil);
        }
    }
}
