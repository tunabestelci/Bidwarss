using System;
using Unity.Netcode;
using UnityEngine;
using Bidwarss.Domain;

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
        public int stackIndex;
        public int crate;
        public int dollars;
        public ItemCondition condition;
        public ItemLocation location;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref id);
            serializer.SerializeValue(ref kind);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref yaw);
            serializer.SerializeValue(ref holder);
            serializer.SerializeValue(ref slot);
            serializer.SerializeValue(ref stackIndex);
            serializer.SerializeValue(ref crate);
            serializer.SerializeValue(ref dollars);
            serializer.SerializeValue(ref condition);
            serializer.SerializeValue(ref location);
        }

        public bool Equals(ItemState other) => id == other.id && kind == other.kind &&
            position.Equals(other.position) && yaw.Equals(other.yaw) && holder == other.holder && slot == other.slot &&
            stackIndex == other.stackIndex && crate == other.crate && dollars == other.dollars && condition == other.condition && location == other.location;
    }

    public struct CrateState : INetworkSerializable, IEquatable<CrateState>
    {
        public bool opened;
        public ulong opener;
        public float progress;
        public double openedAt;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref opened); s.SerializeValue(ref opener); s.SerializeValue(ref progress); s.SerializeValue(ref openedAt); }
        public bool Equals(CrateState o) => opened == o.opened && opener == o.opener && progress.Equals(o.progress) && openedAt.Equals(o.openedAt);
    }

    public struct StackState : INetworkSerializable, IEquatable<StackState>
    {
        public int kind, count;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref kind); s.SerializeValue(ref count); }
        public bool Equals(StackState o) => kind == o.kind && count == o.count;
    }
}
