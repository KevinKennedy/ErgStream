namespace ErgComm.Models
{
    /// <summary>
    /// Represents information about a discovered erg device.
    /// </summary>
    public class ErgInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the erg.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the erg.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the signal strength (RSSI) in dBm. Higher (less negative) values indicate stronger signal.
        /// </summary>
        public int SignalStrength { get; set; }

        /// <summary>
        /// Gets or sets the type of erg.
        /// </summary>
        public ErgType ErgType { get; set; }

        /// <summary>
        /// Gets or sets whether this is a mock erg for testing.
        /// </summary>
        public bool IsMockErg { get; set; }

        public override string ToString() => $"{Name} ({ErgType}) - {SignalStrength}dBm";
    }
}