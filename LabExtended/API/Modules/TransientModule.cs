﻿using LabExtended.API.Collections.Locked;

using LabExtended.Attributes;
using LabExtended.Extensions;
using LabExtended.Core;

using LabExtended.Utilities.Generation;
using LabExtended.Utilities.Unity;

using NorthwoodLib.Pools;

using UnityEngine;

namespace LabExtended.API.Modules
{
    /// <summary>
    /// A module that is reused once the targeted player re-joins the server.
    /// </summary>
    public class TransientModule : GenericModule<ExPlayer>
    {
        public struct TransientModuleUpdateLoop { }
        
        /// <summary>
        /// The reason for a module's removal.
        /// </summary>
        public enum RemovalReason : byte
        {
            /// <summary>
            /// The module has requested it by returning <see langword="true"/> in <see cref="OnLeaving"/>.
            /// </summary>
            Requested = 0,

            /// <summary>
            /// The module's lifetime has expired.
            /// </summary>
            Expired = 1,

            /// <summary>
            /// The module's removal is forced by using <see cref="Module.RemoveModule{T}"/>.
            /// </summary>
            Forced = 2
        }

        internal static readonly LockedDictionary<string, List<TransientModule>> _cachedModules = new LockedDictionary<string, List<TransientModule>>();
        internal static readonly Dictionary<string, List<TransientModule>> _removalDict = new Dictionary<string, List<TransientModule>>();
        internal static float _tickTimer = 0f;

        internal DateTime? _addedAt;
        internal DateTime? _removedAt;

        internal bool _isCached;
        internal bool _isForced;

        /// <summary>
        /// Gets or sets the delay between ticks for removed modules. Values below one will disable ticking removed modules entirely.
        /// </summary>
        public static int TickDelay { get; set; } = 500;

        /// <summary>
        /// Gets a value indicating whether the owner is offline or not.
        /// </summary>
        public bool IsOffline => CastParent is null;

        /// <summary>
        /// Gets the module's ID.
        /// </summary>
        public string ModuleId { get; } = RandomGen.Instance.GetString(10);

        /// <summary>
        /// Gets the owner's user ID.
        /// </summary>
        public string OwnerId { get; private set; }

        /// <summary>
        /// Gets the time that has passed since the owning player left.
        /// </summary>
        public TimeSpan TimeSinceRemoval => _removedAt.HasValue ? DateTime.Now - _removedAt.Value : TimeSpan.Zero;

        /// <summary>
        /// Gets the maximum amount of time that can pass.
        /// </summary>
        public virtual TimeSpan? LifeTime { get; }

        /// <summary>
        /// Whether or not to keep this module active once the player leaves.
        /// </summary>
        public virtual bool KeepActive { get; set; }

        /// <summary>
        /// Gets called when the player joins back / when the module is added for the first time.
        /// </summary>
        public virtual void OnJoined() { }

        /// <summary>
        /// Gets called when the player leaves.
        /// </summary>
        /// <returns><see langword="true"/> if you want to re-add the module once the player returns <i>(default behaviour)</i>, otherwise <see langword="false"/>.</returns>
        public virtual bool OnLeaving() => true;

        /// <summary>
        /// Gets called when the module gets removed from the dictionary.
        /// </summary>
        public virtual void OnRemoved(RemovalReason removalReason) { }

        /// <inheritdoc/>
        public override void OnStarted()
        {
            base.OnStarted();

            if (string.IsNullOrWhiteSpace(OwnerId))
                OwnerId = CastParent.UserId;
            else if (CastParent.UserId != OwnerId)
                throw new InvalidOperationException($"This module belongs to {OwnerId} and cannot be added to {CastParent.UserId}");

            if (!_cachedModules.TryGetValue(CastParent.UserId, out var transientModules))
                _cachedModules[CastParent.UserId] = transientModules = new List<TransientModule>();

            if (!transientModules.Contains(this))
                transientModules.Add(this);

            _addedAt = DateTime.Now;
            _removedAt = null;
            _isCached = false;

            OnJoined();
        }

        /// <inheritdoc/>
        public override void OnStopped()
        {
            base.OnStopped();

            _addedAt = null;
            _removedAt = DateTime.Now;

            if (!_cachedModules.TryGetValue(CastParent.UserId, out var transientModules))
                _cachedModules[CastParent.UserId] = transientModules = new List<TransientModule>();

            if (!OnLeaving() || _isForced)
            {
                if (transientModules.Remove(this))
                {
                    OnRemoved(RemovalReason.Requested);
                }
            }
            else
            {
                _isCached = true;
            }
        }

        private static void OnUpdate()
        {
            if (TickDelay < 1)
                return;

            if (TickDelay > 0)
            {
                _tickTimer -= Time.deltaTime;

                if (_tickTimer > 0f)
                    return;

                _tickTimer = TickDelay;
            }

            _removalDict.Clear();

            foreach (var modulePair in _cachedModules)
            {
                foreach (var module in modulePair.Value)
                {
                    var type = module.GetType();

                    if (!module.IsActive)
                        continue;

                    if (!module._isCached)
                        continue;

                    if (string.IsNullOrWhiteSpace(module.OwnerId))
                        continue;

                    if (module.LifeTime.HasValue && module.TimeSinceRemoval >= module.LifeTime.Value)
                    {
                        if (!_removalDict.TryGetValue(module.OwnerId, out var removedModules))
                            _removalDict[module.OwnerId] = removedModules = ListPool<TransientModule>.Shared.Rent();

                        if (!removedModules.Contains(module))
                            removedModules.Add(module);

                        module.OnRemoved(RemovalReason.Expired);
                        continue;
                    }

                    if (module.UpdateDelay > 0f)
                    {
                        module._moduleTime -= Time.deltaTime;
                        
                        if (module._moduleTime > 0f)
                            continue;

                        module._moduleTime = module.UpdateDelay;
                    }
                    
                    try
                    {
                        module.Update();
                    }
                    catch (Exception ex)
                    {
                        ApiLog.Error("Transient Modules", $"Module &3{type.Name}&r (&6{module.ModuleId}&r) failed to tick!\n{ex.ToColoredString()}");
                    }
                }
            }

            foreach (var removedPair in _removalDict)
            {
                if (removedPair.Value is null)
                    continue;

                if (!_cachedModules.TryGetValue(removedPair.Key, out var cachedModules))
                    continue;

                foreach (var removedModule in removedPair.Value)
                    cachedModules.Remove(removedModule);

                ListPool<TransientModule>.Shared.Return(removedPair.Value);
            }
        }

        [LoaderInitialize(1)]
        private static void Init()
        {
            PlayerLoopHelper.ModifySystem(x =>
                x.InjectAfter<ModuleUpdateLoop>(OnUpdate, typeof(TransientModuleUpdateLoop)) ? x : null);
        }
    }
}