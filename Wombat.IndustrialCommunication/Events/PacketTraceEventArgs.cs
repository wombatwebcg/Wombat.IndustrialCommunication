using System;

namespace Wombat.IndustrialCommunication
{
    public enum PacketTraceDirection
    {
        Received = 0,
        Sent = 1
    }

    public sealed class PacketTraceEventArgs : EventArgs
    {
        public PacketTraceEventArgs(PacketTraceDirection direction, string meaning, byte[] data)
        {
            Direction = direction;
            Meaning = meaning ?? string.Empty;
            Data = data ?? Array.Empty<byte>();
        }

        public PacketTraceDirection Direction { get; }

        public string Meaning { get; }

        public byte[] Data { get; }

        public string HexText => Data.Length == 0 ? "<empty>" : BitConverter.ToString(Data).Replace("-", " ");
    }
}
