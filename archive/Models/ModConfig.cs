// Models/ModConfig.cs
namespace GoodFences.Models
{
    /// <summary>Host mode determines how the host player participates in the farm economy.</summary>
    public enum HostMode
    {
        /// <summary>Host claims an empty quadrant as private land (2-3 players only).</summary>
        Private,
        /// <summary>Host works commons + receives percentage cut from farmhands (2-4 players).</summary>
        Landlord
    }

    /// <summary>Configuration options for the GoodFences mod.</summary>
    public class ModConfig
    {
        /// <summary>Whether to show visual indicators when approaching boundaries.</summary>
        public bool ShowBoundaryWarnings { get; set; } = true;

        /// <summary>Distance (in tiles) at which to show boundary warnings.</summary>
        public int BoundaryWarningDistance { get; set; } = 2;

        /// <summary>Whether to enable debug logging.</summary>
        public bool DebugMode { get; set; } = false;

        /// <summary>Percentage of farmhand shipping revenue that goes to host in Landlord mode (0-100).</summary>
        public int LandlordCutPercent { get; set; } = 10;
    }
}
