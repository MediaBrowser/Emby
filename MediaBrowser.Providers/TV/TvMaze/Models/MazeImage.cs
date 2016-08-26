using System;

namespace MediaBrowser.Providers.TV.TvMaze.Models
{
    /// <summary>
    /// Holds links for Images of items.
    /// </summary>
    public class MazeImage
    {
        /// <summary>
        /// Resized and compressed for faster transfer.
        /// </summary>
        public Uri medium { get; set; }
        /// <summary>
        /// Original, best Image Quality.
        /// </summary>
        public Uri original { get; set; }

        public override string ToString()
        {
            if (medium != null && original != null) return "Original and Medium Quality";
            else if (medium != null) return "Medium Quality only";
            else return "Original Quality only";
        }
    }
}
