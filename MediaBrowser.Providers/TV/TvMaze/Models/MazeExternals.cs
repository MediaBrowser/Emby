namespace MediaBrowser.Providers.TV.TvMaze.Models
{
    /// <summary>
    /// Identifiers for the series on other scrapers.
    /// </summary>
    public class MazeExternals
    {
        /// <summary>
        /// ID for TVRage.
        /// </summary>
        public uint? tvrage { get; set; }
        /// <summary>
        /// ID for TheTVDB.
        /// </summary>
        public uint? thetvdb { get; set; }
        /// <summary>
        /// ID for the imdb Scraper.
        /// </summary>
        public string imdb { get; set; }
    }
}
