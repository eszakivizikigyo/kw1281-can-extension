using System;

namespace BitFab.KW1281Test.Interface
{
    /// <summary>
    /// Represents a CAN bus message
    /// </summary>
    public class CanMessage
    {
        /// <summary>
        /// CAN identifier (11-bit standard or 29-bit extended)
        /// </summary>
        public uint Id { get; set; }

        /// <summary>
        /// True if this is an extended (29-bit) CAN ID
        /// </summary>
        public bool IsExtended { get; set; }

        /// <summary>
        /// Data bytes (0-8 bytes for CAN 2.0)
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Length of data
        /// </summary>
        public int DataLength => Data?.Length ?? 0;

        public CanMessage()
        {
            Data = Array.Empty<byte>();
        }

        public CanMessage(uint id, byte[] data, bool isExtended = false)
        {
            if (isExtended && id > 0x1FFFFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Extended CAN ID cannot exceed 29 bits (0x1FFFFFFF)");
            }
            if (!isExtended && id > 0x7FF)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Standard CAN ID cannot exceed 11 bits (0x7FF)");
            }

            Id = id;
            Data = data ?? Array.Empty<byte>();
            IsExtended = isExtended;

            if (Data.Length > 8)
            {
                throw new ArgumentException("CAN message data cannot exceed 8 bytes", nameof(data));
            }
        }

        public override string ToString()
        {
            var idStr = IsExtended ? $"{Id:X8}" : $"{Id:X3}";
            var dataStr = BitConverter.ToString(Data).Replace("-", " ");
            return $"CAN ID: {idStr} [{DataLength}] {dataStr}";
        }
    }
}
