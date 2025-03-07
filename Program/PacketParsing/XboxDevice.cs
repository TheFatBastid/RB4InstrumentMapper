using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RB4InstrumentMapper.Parsing
{
    public enum MappingMode
    {
        ViGEmBus = 1,
        vJoy = 2
    }

    /// <summary>
    /// Interface for Xbox devices.
    /// </summary>
    public class XboxDevice : IDisposable
    {
        public static MappingMode MapperMode;

        public ushort VendorId { get; private set; }
        public ushort ProductId { get; private set; }

        /// <summary>
        /// The descriptor of the device.
        /// </summary>
        public XboxDescriptor Descriptor { get; private set; }

        /// <summary>
        /// Mapper interface to use.
        /// </summary>
        private IDeviceMapper deviceMapper;

        /// <summary>
        /// Buffer used to assemble chunked packets.
        /// </summary>
        private byte[] chunkBuffer;

        /// <summary>
        /// The previous sequence ID for received command IDs.
        /// </summary>
        private readonly Dictionary<CommandId, byte> previousSequenceIds = new Dictionary<CommandId, byte>();

        /// <summary>
        /// Creates a new XboxDevice with the given device ID and parsing mode.
        /// </summary>
        public XboxDevice()
        {
            deviceMapper = MapperFactory.GetFallbackMapper(MapperMode);
        }

        /// <summary>
        /// Performs cleanup on object finalization.
        /// </summary>
        ~XboxDevice()
        {
            Dispose(false);
        }

        /// <summary>
        /// Handles an incoming packet for this device.
        /// </summary>
        public unsafe void HandlePacket(ReadOnlySpan<byte> data)
        {
            // Some devices may send multiple messages in a single packet, placing them back-to-back
            // The header length is very important in these scenarios, as it determines which bytes are part of the message
            // and where the next message's header begins.
            while (data.Length > 0)
            {
                if (!CommandHeader.TryParse(data, out var header, out int bytesRead) || data.Length < (bytesRead + header.DataLength))
                {
                    return;
                }
                var commandData = data.Slice(bytesRead, header.DataLength);
                data = data.Slice(bytesRead + header.DataLength);

                HandleMessage(header, commandData);
            }
        }

        /// <summary>
        /// Parses command data from a packet.
        /// </summary>
        private unsafe void HandleMessage(CommandHeader header, ReadOnlySpan<byte> commandData)
        {
            // Chunked packets
            if ((header.Flags & CommandFlags.ChunkPacket) != 0)
            {
                // Get sequence length/index
                if (!ParsingUtils.DecodeLEB128(commandData, out int bufferIndex, out int bytesRead))
                {
                    return;
                }
                commandData = commandData.Slice(bytesRead);

                // Do nothing with chunks of length 0
                if (bufferIndex > 0)
                {
                    // Buffer index equalling buffer length signals the end of the sequence
                    if (chunkBuffer != null && bufferIndex >= chunkBuffer.Length)
                    {
                        Debug.Assert(commandData.Length == 0);
                        commandData = chunkBuffer;
                    }
                    else
                    {
                        if ((header.Flags & CommandFlags.ChunkStart) != 0)
                        {
                            Debug.Assert(chunkBuffer == null);
                            // Buffer index is the total size of the buffer on the starting packet
                            chunkBuffer = new byte[bufferIndex];
                        }

                        Debug.Assert(chunkBuffer != null);
                        Debug.Assert((bufferIndex + commandData.Length) >= chunkBuffer.Length);
                        if (chunkBuffer == null || ((bufferIndex + commandData.Length) >= chunkBuffer.Length))
                        {
                            return;
                        }

                        commandData.CopyTo(chunkBuffer.AsSpan(bufferIndex, commandData.Length));
                        return;
                    }
                }
            }

            // Ensure lengths match
            if (header.DataLength != commandData.Length)
            {
                // This is probably a bug
                Debug.Fail($"Command header length does not match buffer length! Header: {header.DataLength}  Buffer: {commandData.Length}");
                return;
            }

            // Don't handle the same packet twice
            if (!previousSequenceIds.TryGetValue(header.CommandId, out byte previousSequence))
            {
                previousSequenceIds.Add(header.CommandId, header.SequenceCount);
            }
            else if (header.SequenceCount == previousSequence)
            {
                return;
            }

            switch (header.CommandId)
            {
                case CommandId.Arrival:
                    HandleArrival(commandData);
                    break;

                case CommandId.Descriptor:
                    HandleDescriptor(commandData);
                    break;

                default:
                    // Hand off unrecognized commands to the mapper
                    deviceMapper.HandlePacket(header.CommandId, commandData);
                    break;
            }
        }

        /// <summary>
        /// Handles the arrival message of the device.
        /// </summary>
        private unsafe void HandleArrival(ReadOnlySpan<byte> data)
        {
            if (data.Length < sizeof(DeviceArrival) || MemoryMarshal.TryRead(data, out DeviceArrival arrival))
                return;

            VendorId = arrival.VendorId;
            ProductId = arrival.ProductId;
        }

        /// <summary>
        /// Handles the Xbox One descriptor of the device.
        /// </summary>
        private void HandleDescriptor(ReadOnlySpan<byte> data)
        {
            if (!XboxDescriptor.Parse(data, out var descriptor))
                return;

            Descriptor = descriptor;
            deviceMapper = MapperFactory.GetMapper(descriptor.InterfaceGuids, MapperMode);
        }

        /// <summary>
        /// Performs cleanup for the device.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                deviceMapper?.Dispose();
                deviceMapper = null;
            }
        }
    }
}