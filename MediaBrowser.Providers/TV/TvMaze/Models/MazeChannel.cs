namespace MediaBrowser.Providers.TV.TvMaze.Models
{
    /// <summary>
    /// Information about a particular Network or WebChannel.
    /// </summary>
    public class MazeChannel
    {
        /// <summary>
        /// Channel ID (Network or WebChannel, check Required for Ambiguity.)
        /// </summary>
        public int id { get; set; }
        /// <summary>
        /// Name of the Channel.
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// Country the Network originates from.
        /// </summary>
        public MazeCountry country { get; set; }
    }
}
