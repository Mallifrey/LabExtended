﻿using HarmonyLib;

using LabExtended.API;

using PlayerRoles.PlayableScps.Scp079;
using PlayerRoles.PlayableScps.Scp079.Rewards;

using PlayerStatsSystem;

namespace LabExtended.Patches.Functions.Scp079Rewards
{
    public static class Scp079TerminationPatch
    {
        [HarmonyPatch(typeof(TerminationRewards), nameof(TerminationRewards.GainReward))]
        public static bool Prefix(Scp079Role scp079, ReferenceHub deadPly, DamageHandlerBase damageHandler)
        {
            if (ExPlayer.TryGet(deadPly, out var player) && !player.Toggles.CanCountAs079ExpTarget)
                return false;

            return true;
        }
    }
}