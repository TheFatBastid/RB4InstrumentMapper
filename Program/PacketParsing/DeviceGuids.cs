using System;

namespace RB4InstrumentMapper.Parsing
{
    /// <summary>
    /// Xbox device interface GUIDs.
    /// </summary>
    internal static class DeviceGuids
    {
        public static readonly Guid MadCatzGuitar = Guid.Parse("0D2AE438-7F7D-4933-8693-30FC55018E77");
        public static readonly Guid MadCatzDrumkit = Guid.Parse("06182893-CCE0-4B85-9271-0A10DBAB7E07");
        // public static readonly Guid PdpGuitar = Guid.Parse(""); // Not known yet
        public static readonly Guid PdpDrumkit = Guid.Parse("A503F9B0-955E-47C4-A2ED-B1336FA7703E");

        public static readonly Guid MadCatzLegacyWireless = Guid.Parse("AF259D0F-76B0-4CDB-BFD1-CEA8C0A8F5EE");

        public static readonly Guid XboxInputDevice = Guid.Parse("9776FF56-9BFD-4581-AD45-B645BBA526D6");
        public static readonly Guid XboxNavigationController = Guid.Parse("B8F31FE7-7386-40E9-A9F8-2F21263ACFB7");
    }
}