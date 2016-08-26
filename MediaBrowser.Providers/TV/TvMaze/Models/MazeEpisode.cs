using System;

namespace MediaBrowser.Providers.TV.TvMaze.Models
{
    /// <summary>
    /// A collection of Information about an Episode on TVMaze.
    /// </summary>
    public class MazeEpisode
    {
        /// <summary>
        /// Unique TVMaze Episode Identifier.
        /// </summary>
        public uint id { get; set; }
        /// <summary>
        /// Url to this Episode's Page on the Website.
        /// </summary>
        public Uri url { get; set; }
        /// <summary>
        /// Name of the Episode.
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// Season Number of the Episode.
        /// </summary>
        public int? season { get; set; }
        /// <summary>
        /// Episode Number in Season for the Episode.
        /// </summary>
        public int? number { get; set; }
        /// <summary>
        /// The Day that the Episode was/is First Aired.
        /// </summary>
        public DateTime? airdate { get; set; }
        /// <summary>
        /// Specfic Timezone offset time, for the AirTime of the Episode.
        /// </summary>
        public DateTimeOffset? airstamp { get; set; }
        /// <summary>
        /// How many minutes the Episode ran for.
        /// </summary>
        public int? runtime { get; set; }
        /// <summary>
        /// Images of the Episode.
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
        /// <summary>
        /// The show that this Episode originates from.
        /// </summary>
        public MazeSeries show { get; set; }
    }
}
