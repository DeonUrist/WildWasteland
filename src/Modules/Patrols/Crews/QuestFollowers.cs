using System;
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
        public void Animated(GameObject npc, bool enabled)
        {
            var pose = npc != null ? npc.GetComponent<Pose>() : null;
            if (pose != null) pose.AnimatedFollower = enabled;
        }
        public void Combat(GameObject npc, GameObject target, float blockedUntil)
        {
            var guard = npc != null ? npc.GetComponent<PassengerGuard>() : null;
            if (guard != null) guard.QuestTarget(target, blockedUntil);
        }
    }
}
