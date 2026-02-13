// Models/SaveData.cs
namespace GoodFences.Models
{
    /// <summary>Per-save data that persists across sessions.</summary>
    public class SaveData
    {
        /// <summary>Host mode for this save (Private or Landlord).</summary>
        public HostMode HostMode { get; set; } = HostMode.Private;

        /// <summary>Whether the game mode has been locked (set after Day 1 selection).</summary>
        public bool ModeLocked { get; set; } = false;

        /// <summary>Number of players when mode was locked.</summary>
        public int LockedPlayerCount { get; set; } = 0;

        /// <summary>The game day when the mode was locked.</summary>
        public int LockDay { get; set; } = 0;
    }
}
