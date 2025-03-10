﻿using HarmonyLib;

using LabExtended.API;
using LabExtended.Core.Hooking;
using LabExtended.Events.Scp0492;

using PlayerRoles;
using PlayerRoles.Ragdolls;
using PlayerRoles.PlayableScps.Scp049.Zombies;

using LabExtended.Attributes;

namespace LabExtended.Patches.Functions.Scp049
{
    public static class Scp0492ConsumePatch
    {
        [HookPatch(typeof(Scp0492ConsumingRagdollArgs), true)]
        [HarmonyPatch(typeof(ZombieConsumeAbility), nameof(ZombieConsumeAbility.ServerValidateBegin))]
        public static bool Prefix(ZombieConsumeAbility __instance, BasicRagdoll ragdoll, ref byte __result)
        {
            if (!ExPlayer.TryGet(__instance.Owner, out var scp))
                return true;

            if (!scp.Toggles.CanConsumeRagdollsAsZombie)
            {
                __result = 2;
                return false;
            }

            if (ZombieConsumeAbility.ConsumedRagdolls.Contains(ragdoll))
            {
                __result = 2;
                return false;
            }

            if (!ragdoll.Info.RoleType.IsHuman() || !__instance.IsCloseEnough(__instance.CastRole.FpcModule.Position, __instance._ragdollTransform.position))
            {
                __result = 3;
                return false;
            }

            if (scp.Stats.Health.NormalizedValue == 1f)
            {
                __result = 8;
                return false;
            }

            if (ZombieConsumeAbility.AllAbilities.Any(x => x.IsInProgress && x.CurRagdoll == ragdoll))
            {
                __result = 9;
                return false;
            }

            var target = ExPlayer.Get(ragdoll.Info.OwnerHub);

            if (target != null && !target.Toggles.CanBeConsumedByZombies)
            {
                __result = 2;
                return false;
            }

            var consumingArgs = new Scp0492ConsumingRagdollArgs(scp, target, ragdoll);

            if (!HookRunner.RunEvent(consumingArgs, true))
            {
                __result = (byte)(consumingArgs.Code == 0 ? 2 : consumingArgs.Code);
                return false;
            }

            __result = consumingArgs.Code;
            return false;
        }
    }
}