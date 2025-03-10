﻿using LabExtended.API;

namespace LabExtended.Utilities.Values
{
    public struct FakeValue<T>
    {
        private readonly Dictionary<uint, T> _values;

        public T? GlobalValue { get; set; }

        public bool KeepOnDeath { get; set; }
        public bool KeepOnRoleChange { get; set; }

        public T this[uint netId]
        {
            get => _values[netId];
            set => _values[netId] = value;
        }

        public T this[ExPlayer player]
        {
            get => _values[player.NetworkId];
            set => _values[player.NetworkId] = value;
        }

        public FakeValue()
            => _values = new();

        public T GetValue(uint netId, T defaultValue = default)
            => _values.TryGetValue(netId, out var fakedValue) ? fakedValue : defaultValue;

        public T GetValue(ReferenceHub hub, T defaultValue = default)
            => _values.TryGetValue(hub.netId, out var fakedValue) ? fakedValue : defaultValue;

        public T GetValue(ExPlayer player, T defaultValue = default)
            => _values.TryGetValue(player.NetworkId, out var fakedValue) ? fakedValue : defaultValue;

        public bool GetValue(uint netId, ref T value)
        {
            if (_values.TryGetValue(netId, out var fakedValue))
            {
                value = fakedValue;
                return true;
            }

            return false;
        }

        public bool GetValue(ReferenceHub hub, ref T value)
        {
            if (_values.TryGetValue(hub.netId, out var fakedValue))
            {
                value = fakedValue;
                return true;
            }

            return false;
        }

        public bool GetValue(ExPlayer player, ref T value)
        {
            if (_values.TryGetValue(player.NetworkId, out var fakedValue))
            {
                value = fakedValue;
                return true;
            }

            return false;
        }

        public bool TryGetValue(uint netId, out T fakedValue)
            => _values.TryGetValue(netId, out fakedValue);

        public bool TryGetValue(ReferenceHub hub, out T fakedValue)
            => _values.TryGetValue(hub.netId, out fakedValue);

        public bool TryGetValue(ExPlayer player, out T fakedValue)
            => _values.TryGetValue(player.NetworkId, out fakedValue);

        public void SetValue(uint netId, T value)
            => _values[netId] = value;

        public void SetValue(ReferenceHub hub, T value)
            => _values[hub.netId] = value;

        public void SetValue(ExPlayer player, T value)
            => _values[player.NetworkId] = value;

        public bool RemoveValue(uint netId)
            => _values.Remove(netId);

        public bool RemoveValue(ReferenceHub hub)
            => _values.Remove(hub.netId);

        public bool RemoveValue(ExPlayer player)
            => _values.Remove(player.NetworkId);

        public void ClearValues()
            => _values.Clear();

        public static implicit operator T(FakeValue<T> value)
            => value.GlobalValue;

        public static explicit operator FakeValue<T>(T value)
            => new() { GlobalValue = value };
    }
}