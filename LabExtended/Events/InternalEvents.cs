﻿using LabApi.Events;
using LabApi.Events.Handlers;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;

using LabExtended.API;
using LabExtended.API.Enums;

using LabExtended.Core;

using LabExtended.Events.Player;

using LabExtended.Attributes;
using LabExtended.Extensions;

using NetworkManagerUtils;

namespace LabExtended.Events
{
    internal static class InternalEvents
    {
        internal static event Action OnRoundRestart;
        internal static event Action OnRoundWaiting;
        internal static event Action OnRoundStarted;
        internal static event Action OnRoundEnded;

        internal static event Action<ExPlayer> OnPlayerJoined;
        internal static event Action<ExPlayer> OnPlayerLeft; 
        
        internal static event Action<PlayerChangedRoleArgs> OnRoleChanged;
        internal static event Action<PlayerSpawningArgs> OnSpawning; 
        
        private static void InternalHandleRoundWaiting()
        {
            ExPlayer._hostPlayer?.Dispose();
            ExPlayer._hostPlayer = null;
            
            ExPlayer._preauthData.Clear();
            DamageInfo._wrappers.Clear();    

            // No reason not to reset the NPC connection ID
            DummyNetworkConnection._idGenerator = ushort.MaxValue;

            OnRoundWaiting.InvokeSafe();
            
            RoundEvents.InvokeWaiting();
        }

        private static void InternalHandleRoundRestart()
        {
            ExRound.State = RoundState.Restarting;
            ExMap._gates.Clear();
            
            OnRoundRestart.InvokeSafe();
            
            RoundEvents.InvokeRestarted();
        }

        private static void InternalHandleRoundStart()
        {
            ExRound.StartedAt = DateTime.Now;
            ExRound.State = RoundState.InProgress;

            OnRoundStarted.InvokeSafe();
            
            RoundEvents.InvokeStarted();
        }

        private static void InternalHandleRoundEnd(RoundEndedEventArgs _)
        {
            ExRound.State = RoundState.Ended;

            OnRoundEnded.InvokeSafe();
            
            RoundEvents.InvokeEnded();
        }

        private static void InternalHandlePlayerAuth(PlayerPreAuthenticatingEventArgs ev)
        {
            ExPlayer._preauthData[ev.UserId] = ev.Region;
        }

        internal static void InternalHandlePlayerJoin(ExPlayer player)
        {
            OnPlayerJoined.InvokeSafe(player);

            if (!player.IsServer && !player.IsNpc)
                ApiLog.Info("LabExtended", $"Player &3{player.Name}&r (&6{player.UserId}&r) &2joined&r from &3{player.Address} ({player.CountryCode})&r!");
        }

        internal static void InternalHandlePlayerLeave(ExPlayer player)
        {
            if (ExRound.State is RoundState.InProgress || ExRound.State is RoundState.WaitingForPlayers)
            {
                if (ApiLoader.BaseConfig.DisableRoundLockOnLeave && ExRound.RoundLock.HasValue && ExRound.RoundLock.Value.EnabledBy == player)
                {
                    ExRound.IsRoundLocked = false;
                    ApiLog.Warn("Round API", $"Round Lock disabled - the player who enabled it (&3{player.Name}&r &6{player.UserId}&r) left the server.");
                }

                if (ApiLoader.BaseConfig.DisableLobbyLockOnLeave && ExRound.LobbyLock.HasValue && ExRound.LobbyLock.Value.EnabledBy == player)
                {
                    ExRound.IsLobbyLocked = false;
                    ApiLog.Warn("Round API", $"Lobby Lock disabled - the player who enabled it (&3{player.Name}&r &6{player.UserId}&r) left the server.");
                }
            }
            
            OnPlayerLeft.InvokeSafe(player);
            
            player.Dispose();

            if (!player.IsServer && !player.IsNpc)
                ApiLog.Info("LabExtended", $"Player &3{player.Name}&r (&3{player.UserId}&r) &1left&r from &3{player.Address}&r!");
        }

        internal static void InternalHandleRoleChange(PlayerSpawningArgs args)
        {
            OnSpawning.InvokeSafe(args);
        }

        internal static void InternalHandleRoleChange(PlayerChangedRoleArgs args)
        {
            OnRoleChanged.InvokeSafe(args);
        }

        [LoaderInitialize(1)]
        private static void RegisterEvents()
        {
            EventExtensions.InsertFirst<LabEventHandler<PlayerPreAuthenticatingEventArgs>>(typeof(PlayerEvents), nameof(PlayerEvents.PreAuthenticating), InternalHandlePlayerAuth);
            EventExtensions.InsertFirst<LabEventHandler<RoundEndedEventArgs>>(typeof(ServerEvents), nameof(ServerEvents.RoundEnded), InternalHandleRoundEnd);
            EventExtensions.InsertFirst<LabEventHandler>(typeof(ServerEvents), nameof(ServerEvents.WaitingForPlayers), InternalHandleRoundWaiting);
            EventExtensions.InsertFirst<LabEventHandler>(typeof(ServerEvents), nameof(ServerEvents.RoundRestarted), InternalHandleRoundRestart);
            EventExtensions.InsertFirst<LabEventHandler>(typeof(ServerEvents), nameof(ServerEvents.RoundStarted), InternalHandleRoundStart);
        }
    }
}