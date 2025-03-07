using System;
using System.Runtime.InteropServices;
using RB4InstrumentMapper.Vjoy;
using vJoyInterfaceWrap;

namespace RB4InstrumentMapper.Parsing
{
    /// <summary>
    /// A mapper that maps to a vJoy device.
    /// </summary>
    internal abstract class VjoyMapper : IDeviceMapper
    {
        public bool MapGuideButton { get; set; } = false;

        protected vJoy.JoystickState state = new vJoy.JoystickState();
        protected uint deviceId = 0;

        public VjoyMapper()
        {
            deviceId = VjoyClient.GetNextAvailableID();
            if (deviceId == 0)
            {
                throw new VjoyException("No vJoy devices are available.");
            }

            if (!VjoyClient.AcquireDevice(deviceId))
            {
                throw new VjoyException($"Could not claim vJoy device {deviceId}.");
            }

            state.bDevice = (byte)deviceId;
            Console.WriteLine($"Acquired vJoy device with ID of {deviceId}");
        }

        /// <summary>
        /// Performs cleanup on object finalization.
        /// </summary>
        ~VjoyMapper()
        {
            Dispose(false);
        }

        /// <summary>
        /// Handles an incoming packet.
        /// </summary>
        public void HandlePacket(CommandId command, ReadOnlySpan<byte> data)
        {
            if (deviceId == 0)
                throw new ObjectDisposedException("this");

            switch (command)
            {
                case CommandId.Keystroke:
                    HandleKeystroke(data);
                    break;

                default:
                    OnPacketReceived(command, data);
                    break;
            }
        }

        protected abstract void OnPacketReceived(CommandId command, ReadOnlySpan<byte> data);

        private unsafe void HandleKeystroke(ReadOnlySpan<byte> data)
        {
            if (!MapGuideButton || data.Length < sizeof(Keystroke))
                return;

            // Multiple keystrokes can be sent in a single message
            var keys = MemoryMarshal.Cast<byte, Keystroke>(data);
            foreach (var key in keys)
            {
                if ((KeyCode)key.Keycode == KeyCode.LeftWindows)
                {
                    state.SetButton(VjoyButton.Fourteen, key.Pressed);
                    VjoyClient.UpdateDevice(deviceId, ref state);
                }
            }
        }

        /// <summary>
        /// Parses the state of the d-pad.
        /// </summary>
        protected static void ParseDpad(ref vJoy.JoystickState state, GamepadButton buttons)
        {
            VjoyPoV direction;
            if ((buttons & GamepadButton.DpadUp) != 0)
            {
                if ((buttons & GamepadButton.DpadLeft) != 0)
                {
                    direction = VjoyPoV.UpLeft;
                }
                else if ((buttons & GamepadButton.DpadRight) != 0)
                {
                    direction = VjoyPoV.UpRight;
                }
                else
                {
                    direction = VjoyPoV.Up;
                }
            }
            else if ((buttons & GamepadButton.DpadDown) != 0)
            {
                if ((buttons & GamepadButton.DpadLeft) != 0)
                {
                    direction = VjoyPoV.DownLeft;
                }
                else if ((buttons & GamepadButton.DpadRight) != 0)
                {
                    direction = VjoyPoV.DownRight;
                }
                else
                {
                    direction = VjoyPoV.Down;
                }
            }
            else
            {
                if ((buttons & GamepadButton.DpadLeft) != 0)
                {
                    direction = VjoyPoV.Left;
                }
                else if ((buttons & GamepadButton.DpadRight) != 0)
                {
                    direction = VjoyPoV.Right;
                }
                else
                {
                    direction = VjoyPoV.Neutral;
                }
            }

            state.bHats = (uint)direction;
        }

        /// <summary>
        /// Performs cleanup for the vJoy mapper.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            // Reset report
            state.Reset();
            VjoyClient.UpdateDevice(deviceId, ref state);

            // Free device
            VjoyClient.ReleaseDevice(deviceId);
            deviceId = 0;
        }
    }
}
