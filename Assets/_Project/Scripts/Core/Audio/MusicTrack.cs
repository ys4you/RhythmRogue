namespace RhythmRogue.Core.Audio
{
    /// <summary>
    /// Enumerated names for music tracks. Add new entries here when you drop
    /// new files into Resources/Audio/Music/.
    ///
    /// The track name maps directly to the filename (without extension) via
    /// MusicTrackExtensions.ToResourcePath, so renaming the enum requires
    /// renaming the file too.
    /// </summary>
    public enum MusicTrack
    {
        None = 0,
        MenuDrone,      // menu_drone.mp3 - eerie ambient drone for the main menu
        MapShamanic,    // map_shamanic.mp3 - shamanic horror for the map screen
    }

    public static class MusicTrackExtensions
    {
        /// <summary>
        /// Path under Resources/ that the manager loads from.
        /// Convention: lowercase snake_case filename, no extension.
        /// </summary>
        public static string ToResourcePath(this MusicTrack track) => track switch
        {
            MusicTrack.MenuDrone => "Audio/Music/menu_drone",
            MusicTrack.MapShamanic => "Audio/Music/map_shamanic",
            _ => null
        };
    }
}
