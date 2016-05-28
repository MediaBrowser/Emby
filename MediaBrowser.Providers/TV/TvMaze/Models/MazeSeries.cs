using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.TV.TvMaze.Models
{
    /// <summary>
    /// A collection of Information about a Series on TVMaze.
    /// </summary>
    public class MazeSeries
    {
        /// <summary>
        /// Unique TVMaze Show Identifier (0 if Show can't be found).
        /// </summary>
        public uint id { get; set; }
        /// <summary>
        /// Url to this Show's Page on the Website.
        /// </summary>
        public Uri url { get; set; }
        /// <summary>
        /// Name of the Show.
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// Style of presentation of the Show.
        /// </summary>
        public string type { get; set; }
        /// <summary>
        /// Language the Show is spoken in.
        /// </summary>
        public string language { get; set; }
        /// <summary>
        /// Collection of Genres the Show has.
        /// </summary>
        public string[] genres { get; set; }
        /// <summary>
        /// Status of the Show (404 if no Result when scraping).
        /// </summary>
        public string status { get; set; }
        /// <summary>
        /// Images of the Episode.
        /// </summary>
        public int? runtime { get; set; }
        /// <summary>
        /// Specfic Date when the First Episode of the show aired.
        /// </summary>
        public DateTime? premiered { get; set; }
        /// <summary>
        /// Rating of the Show by the community.
        /// </summary>
        public MazeRating rating { get; set; }
        /// <summary>
        /// A Series Ranking system based on a combination of votes, follow counts and page views.
        /// </summary>
        public int weight { get; set; }
        /// <summary>
        /// Network of Show.
        /// </summary>
        public MazeChannel network { get; set; }
        /// <summary>
        /// WebChannel of show.
        /// </summary>
        public MazeChannel webChannel { get; set; }
        /// <summary>
        /// External ID's to other Scrapers.
        /// </summary>
        public MazeExternals externals { get; set; }
        /// <summary>
        /// Images of the Series.
        /// </summary>
        public MazeImage image { get; set; }
        /// <summary>
        /// A small description of the Series.
        /// </summary>
        public string summary { get; set; }
        /// <summary>
        /// Links to Itself, and the Next and Previous Episodes.
        /// </summary>
        public SeriesLinks _links { get; set; }
        /// <summary>
        /// Collection of Episodes in a Show.
        /// </summary>
        public IReadOnlyCollection<MazeEpisode> Episodes { get; set; }
        /// <summary>
        /// Collection of Actors in a Show.
        /// </summary>
        public IReadOnlyCollection<MazeCastMember> Cast { get; set; }

        public class SeriesLinks : MazeLinks
        {
            /// <summary>
            /// Link to the Webpage for the Previous Episode in the Series.
            /// </summary>
            public MazeLink previousepisode { get; set; }
            /// <summary>
            /// Link to the Webpage for the Next Episode in the Series.
            /// </summary>
            public MazeLink nextepisode { get; set; }
        }
    }
}
