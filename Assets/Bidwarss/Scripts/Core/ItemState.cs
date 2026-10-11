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
        // How a loose item rests: 0 = on the floor, 1 = standing inside its crate, 2 = lying on its back inside its crate.
        public byte pose;

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
            serializer.SerializeValue(ref pose);
        }

        public bool Equals(ItemState other) => id == other.id && kind == other.kind &&
            position.Equals(other.position) && yaw.Equals(other.yaw) && holder == other.holder && slot == other.slot &&
            stackIndex == other.stackIndex && crate == other.crate && dollars == other.dollars && condition == other.condition && location == other.location && pose == other.pose;
    }

    public struct CrateState : INetworkSerializable, IEquatable<CrateState>
    {
        public bool opened;
        public ulong opener;
        public float progress;
        public double openedAt;
        // Which model/size the crate uses this round (cardboard box: 0..3, chosen by the server to fit what is inside).
        public byte variant;
        // Shape of the crate this round, fitted to what is inside: cardboard = outer size in metres, wooden = scale
        // multipliers of the crate model. Zero = default shape.
        public Vector3 shape;
        // Random depot: this crate holds nothing this round. It is hidden and cannot be opened. (Inverted on purpose, so a
        // default CrateState is an ordinary active crate.)
        public bool inactive;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref opened); s.SerializeValue(ref opener); s.SerializeValue(ref progress); s.SerializeValue(ref openedAt); s.SerializeValue(ref variant); s.SerializeValue(ref shape); s.SerializeValue(ref inactive); }
        public bool Equals(CrateState o) => opened == o.opened && opener == o.opener && progress.Equals(o.progress) && openedAt.Equals(o.openedAt) && variant == o.variant && shape.Equals(o.shape) && inactive == o.inactive;
    }

    public struct StackState : INetworkSerializable, IEquatable<StackState>
    {
        public int kind, count, capacity;
        // Pallet size: 10, or fewer for the last pallet of an item type (manifest rounds).
        public int Cap => capacity > 0 ? capacity : Bidwarss.Domain.GameRules.StackSize;
        public bool Full => count >= Cap;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref kind); s.SerializeValue(ref count); s.SerializeValue(ref capacity); }
        public bool Equals(StackState o) => kind == o.kind && count == o.count && capacity == o.capacity;
    }
}
