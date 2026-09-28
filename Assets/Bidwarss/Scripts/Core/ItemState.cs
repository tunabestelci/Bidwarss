using System;
using Unity.Netcode;
using UnityEngine;

namespace Bidwarss
{
    public struct ItemState : INetworkSerializable, IEquatable<ItemState>
    {
        public const ulong Nobody = ulong.MaxValue;
        public int id;
        public int kind;
        public Vector3 position;
        public float yaw;
        public ulong holder;
        public int slot;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref id);
            serializer.SerializeValue(ref kind);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref yaw);
            serializer.SerializeValue(ref holder);
            serializer.SerializeValue(ref slot);
        }

        public bool Equals(ItemState other) => id == other.id && kind == other.kind &&
            position.Equals(other.position) && yaw.Equals(other.yaw) && holder == other.holder && slot == other.slot;
    }
}
