using System;

namespace MediaBrowser.Providers.TV.TvMaze.Models
{
    /// <summary>
    /// A collection of Information about a Season on TVMaze.
    /// </summary>
    public class MazeSeason
    {
        /// <summary>
        /// Unique TVMaze Season Identifier.
        /// </summary>
        public uint id { get; set; }
        /// <summary>
        /// Url to this Seasons's Page on the Website.
        /// </summary>
        public Uri url { get; set; }
        /// <summary>
        /// Name of the Season.
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// Season Number.
        /// </summary>
        public int? number { get; set; }
        /// <summary>
        /// Number of episodes in this season.
        /// </summary>
        public int? episodeOrder { get; set; }
        /// <summary>
        /// The Day that the first Episode was/is First Aired.
        /// </summary>
        public DateTime? premiereDate { get; set; }
        /// <summary>
        /// The Day that the last Episode was/is First Aired.
        /// </summary>
        public DateTime? endDate { get; set; }
        /// <summary>
        /// Network of Show.
        /// </summary>
        public MazeChannel network { get; set; }
        /// <summary>
        /// WebChannel of show.
        /// </summary>
        public MazeChannel webChannel { get; set; }
        /// <summary>
        /// Images of the Season.
        /// </summary>
        public MazeImage image { get; set; }
        /// <summary>
        /// A small description of the events of the Episode.
        /// </summary>
        public string summary { get; set; }
        /// <summary>
        /// Link to it's page on the website.
        /// </summary>
        public MazeLinks _links { get; set; }
    }
}
