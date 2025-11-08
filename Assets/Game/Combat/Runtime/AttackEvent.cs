using System;
using Unity.Netcode;

namespace Embervale.Game.Combat
{
    [Serializable]
    public struct AttackEvent : INetworkSerializable, IEquatable<AttackEvent>
    {
        public ushort AttackId;
        public float ServerTime; // seconds since start

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref AttackId);
            serializer.SerializeValue(ref ServerTime);
        }

        public bool Equals(AttackEvent other) => AttackId == other.AttackId && Math.Abs(ServerTime - other.ServerTime) < 0.0001f;
        public override string ToString() => $"AttackId={AttackId} t={ServerTime:F3}";
    }
}

