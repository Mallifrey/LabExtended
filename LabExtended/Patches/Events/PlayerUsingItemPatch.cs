﻿using CustomPlayerEffects;

using HarmonyLib;

using InventorySystem.Items.Usables;

using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

using LabExtended.API;
using LabExtended.API.CustomItems;
using LabExtended.API.CustomUsables;
using LabExtended.Attributes;
using LabExtended.Core.Hooking;
using LabExtended.Events.Player;

using Mirror;

using UnityEngine;

using Utils.Networking;

namespace LabExtended.Patches.Events
{
    public static class PlayerUsingItemPatch
    {
        [HookPatch(typeof(PlayerUsingItemArgs))]
        [HarmonyPatch(typeof(UsableItemsController), nameof(UsableItemsController.ServerReceivedStatus))]
        public static bool Prefix(NetworkConnection conn, StatusMessage msg)
        {
            if (!ExPlayer.TryGet(conn, out var player))
                return true;

            if (player.Inventory.CurrentItem is null || player.Inventory.CurrentItem is not UsableItem curUsable)
                return false;

            if (curUsable.ItemSerial != msg.ItemSerial)
                return false;

            if (msg.Status is StatusMessage.StatusType.Start)
            {
                if (CustomItemManager.InventoryItems.TryGetValue(curUsable, out var customItemInstance)
                    && customItemInstance is CustomUsableInstance customUsableInstance)
                {
                    var usingEventArgs = new PlayerUsingItemEventArgs(player.ReferenceHub, curUsable);

                    PlayerEvents.OnUsingItem(usingEventArgs);

                    if (!usingEventArgs.IsAllowed)
                        return false;
                    
                    if (customUsableInstance.RemainingCooldown > 0f)
                    {
                        customUsableInstance.SendCooldown(customUsableInstance.RemainingCooldown);
                        return false;
                    }

                    if (!customUsableInstance.OnStartUsing())
                        return false;

                    customUsableInstance.IsUsing = true;
                    
                    customUsableInstance.RemainingTime = customUsableInstance.CustomData.UseTime;
                    customUsableInstance.OnStartedUsing();

                    msg.SendToAuthenticated();
                    return false;
                }
                
                if (!curUsable.ServerValidateStartRequest(player.Inventory.UsableItemsHandler))
                    return false;

                if (player.Inventory.UsableItemsHandler.CurrentUsable.ItemSerial != 0)
                    return false;

                if (!curUsable.CanStartUsing)
                    return false;

                var usingArgs = new PlayerUsingItemArgs(player, curUsable, UsableItemsController.GetCooldown(curUsable.ItemSerial, curUsable, player.Inventory.UsableItemsHandler), curUsable.ItemTypeId.GetSpeedMultiplier(player.ReferenceHub));

                if (!HookRunner.RunEvent(usingArgs, true))
                    return false;

                if (usingArgs.RemainingCooldown > 0f)
                {
                    player.Connection.Send(new ItemCooldownMessage(curUsable.ItemSerial, usingArgs.RemainingCooldown));
                    return false;
                }

                if (usingArgs.SpeedMultiplier > 0f)
                {
                    var usingEventArgs = new PlayerUsingItemEventArgs(player.ReferenceHub, curUsable);

                    PlayerEvents.OnUsingItem(usingEventArgs);

                    if (!usingEventArgs.IsAllowed)
                        return false;
                    
                    player.Inventory.UsableItemsHandler.CurrentUsable = new CurrentlyUsedItem(curUsable, curUsable.ItemSerial, Time.timeSinceLevelLoad);
                    player.Inventory.UsableItemsHandler.CurrentUsable.Item.OnUsingStarted();
                    
                    msg.SendToAuthenticated();
                    return false;
                }
            }
            else
            {
                if (CustomItemManager.InventoryItems.TryGetValue(curUsable, out var customItemInstance)
                    && customItemInstance is CustomUsableInstance customUsableInstance)
                {
                    var cancellingArgs = new PlayerCancellingUsingItemEventArgs(player.ReferenceHub, curUsable);

                    PlayerEvents.OnCancellingUsingItem(cancellingArgs);

                    if (!cancellingArgs.IsAllowed)
                        return false;
                    
                    if (!customUsableInstance.IsUsing)
                        return false;

                    if (!customUsableInstance.OnCancelling())
                        return false;

                    customUsableInstance.IsUsing = false;
                    
                    customUsableInstance.RemainingTime = 0f;
                    customUsableInstance.RemainingCooldown = customUsableInstance.CustomData.Cooldown;
                    
                    customUsableInstance.OnCancelled();
                    
                    msg.SendToAuthenticated();
                    return false;
                }
                
                if (!curUsable.ServerValidateCancelRequest(player.Inventory.UsableItemsHandler))
                    return false;

                if (player.Inventory.CurrentlyUsedItem.ItemSerial == 0)
                    return false;

                var speedMultiplier = curUsable.ItemTypeId.GetSpeedMultiplier(player.ReferenceHub);

                if (player.Inventory.CurrentlyUsedItem.StartTime + curUsable.MaxCancellableTime / speedMultiplier > Time.timeSinceLevelLoad)
                {
                    var cancellingArgs = new PlayerCancellingUsingItemEventArgs(player.ReferenceHub, curUsable);

                    PlayerEvents.OnCancellingUsingItem(cancellingArgs);

                    if (!cancellingArgs.IsAllowed)
                        return false;

                    player.Inventory.CurrentlyUsedItem.Item.OnUsingCancelled();
                    player.Inventory.CurrentlyUsedItem = CurrentlyUsedItem.None;

                    msg.SendToAuthenticated();

                    PlayerEvents.OnCancelledUsingItem(new PlayerCancelledUsingItemEventArgs(player.ReferenceHub, curUsable));
                }
            }

            return false;
        }
    }
}