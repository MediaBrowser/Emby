namespace MediaBrowser.Providers.TV.TvMaze.Models
{
    /// <summary>
    /// Base Links Class points to itself as its only link.
    /// </summary>
    public class MazeLinks
    {
        /// <summary>
        /// Link to the Website's Page
        /// </summary>
        public MazeLink self { get; set; }
    }
}