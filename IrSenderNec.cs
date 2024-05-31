using nanoFramework.Hardware.Esp32.Rmt;
using System;

#nullable enable

namespace IRWA
{
    /// <summary>
    /// Provides an implementation of the NEC Infrared Transmission protocol.
    /// This protocol is used by many different device vendors.
    /// </summary>
    /// <remarks>
    /// See the "examples" section of the documentation of this class for more information
    /// on the protocol.
    /// </remarks>
    /// <example>
    /// The NEC Infrared Transmission protocol encodes message bits by "pulse distance encoding" - 
    /// in other words, through the durations of bursts of a 38kHz carrier frequency and
    /// the durations of "silence" (space) between these bursts.
    /// 
    /// For example, a logical 0 is encoded as 562.5µs of pulse, followed by 562.5µs of space,
    /// while a logical 1 is encoded as 562.5µs of pulse, followed by 1687.5µs of space.
    /// 
    /// Pressing a key on an infrared remote control triggers the following sequence:
    /// - 9000µs leading pulse burst
    /// - 4500µs space
    /// - The (encoded) 8-bit address of the receiving device
    /// - The logical inverse of previously sent address
    /// - The (encoded) 8-bit command
    /// - The logical inverse of previously sent command
    /// - 562.5µs trailing pulse burst
    /// 
    /// Source: <see cref="https://techdocs.altium.com/display/FPGA/NEC+Infrared+Transmission+Protocol"/> 
    /// (retrieved on 2024-05).
    /// </example>
    public class IrSenderNec
    {
        private const ushort LeadingPulseBurstDuration = 9000;
        private const ushort SpaceAfterLeadingPulseDuration = 4500;
        private const ushort LogicalZeroPulseBurstDuration = 562; // Rounded down from 562.5µs
        private const ushort LogicalZeroSpaceAfterPulseBurstDuration = 563; // Rounded up from 562.5µs
        private const ushort LogicalOnePulseBurstDuration = 562; // Rounded down from 562.5µs
        private const ushort LogicalOneSpaceAfterPulseBurstDuration = 1688; // Rounded up from 1.6875ms
        private const ushort TrailingPulseBurstDuration = 562; // Rounded down from 562.5µs
        private const ushort SpaceAfterTrailingPulseDuration = 563; // Not from specification, just a "magic number"
        private const int CarrierWaveFrequency = 38222; // Recommended by specification for optimal performance

        private static readonly RmtCommand transmissionStart =
            new(LeadingPulseBurstDuration, true, SpaceAfterLeadingPulseDuration, false);
        private static readonly RmtCommand transmissionEnd =
            new(TrailingPulseBurstDuration, true, SpaceAfterTrailingPulseDuration, false);
        private static readonly RmtCommand logicalZero =
            new(LogicalZeroPulseBurstDuration, true, LogicalZeroSpaceAfterPulseBurstDuration, false);
        private static readonly RmtCommand logicalOne =
            new(LogicalOnePulseBurstDuration, true, LogicalOneSpaceAfterPulseBurstDuration, false);

        private readonly TransmitterChannel transmitterChannel;

        /// <summary>
        /// Creates a new <see cref="IrSenderNec"/> instance.
        /// </summary>
        /// <param name="pin">The (PWM-compatible) PIN to be used for sending out the signals.</param>
        public IrSenderNec(int pin)
        {
            var channelSettings = new TransmitChannelSettings(pin)
            {
                CarrierWaveFrequency = CarrierWaveFrequency,
                EnableCarrierWave = true
            };
            transmitterChannel = new TransmitterChannel(channelSettings);
        }

        /// <summary>
        /// Encodes and sends a raw message.
        /// </summary>
        /// <param name="message">
        /// The content of the whole message.
        /// </param>
        /// <param name="repetitions">
        /// The amount of times the specified message should be repeated.
        /// Specify 0 (default) for no repetitions so that the message will only be sent once.
        /// </param>
        public void SendRaw(uint message, uint repetitions = 0)
        {
            transmitterChannel.ClearCommands();

            transmitterChannel.AddCommand(transmissionStart);
            AddAsRmtCommands(message, transmitterChannel);
            transmitterChannel.AddCommand(transmissionEnd);

            for (uint i = 0; i <= repetitions; i++)
            {
                transmitterChannel.Send(true);
            }

            transmitterChannel.ClearCommands();
        }

        /// <summary>
        /// Creates, encodes and sends a message.
        /// </summary>
        /// <param name="address">
        /// The address of the receiving device.
        /// </param>
        /// <param name="command">
        /// The command to be sent to the device.
        /// </param>
        /// <param name="repetitions">
        /// The amount of times the specified message should be repeated.
        /// Specify 0 (default) for no repetitions so that the message will only be sent once.
        /// </param>
        /// <remarks>
        /// As per specification, a NEC message consists of the address (8 bit), 
        /// the logical inverse of the address (8 bit), the command (8 bit)
        /// and the logical inverse of the command (8 bit).
        /// </remarks>
        public void Send(byte address, byte command, uint repetitions = 0)
        {
            byte addressInverse = (byte)~address;
            byte commandInverse = (byte)~command;

            uint messageContent = (uint)address << 0x18 |
                (uint)addressInverse << 0x10 |
                (uint)command << 0x08 |
                (uint)commandInverse;

            SendRaw(messageContent, repetitions);
        }

        private static void AddAsRmtCommands(uint value, TransmitterChannel targetChannel)
        {
            // Pull apart the specified 32-bit (4 byte) value into 4 byte chunks manually,
            // as the BitConverter method would return them in the "wrong" order
            // (and it's good to know how this actually works under the hood).
            byte block1 = (byte)((value >> 0x18) & 0xFF);
            byte block2 = (byte)((value >> 0x10) & 0xFF);
            byte block3 = (byte)((value >> 0x08) & 0xFF);
            byte block4 = (byte)((value >> 0x00) & 0xFF);

            AddAsRmtCommands(block1, targetChannel);
            AddAsRmtCommands(block2, targetChannel);
            AddAsRmtCommands(block3, targetChannel);
            AddAsRmtCommands(block4, targetChannel);
        }

        private static void AddAsRmtCommands(byte value, TransmitterChannel targetChannel)
        {
            // Convert the provided byte value into individual bits and, from left to right,
            // add the matching logicalOne/logicalZero commands to the channel.
            for (int i = 7; i >= 0; i--)
            {
                byte bitmask = (byte)Math.Pow(2, i);
                bool isBitSet = (value & bitmask) == bitmask;
                targetChannel.AddCommand(isBitSet ? logicalOne : logicalZero);
            }
        }
    }
}
