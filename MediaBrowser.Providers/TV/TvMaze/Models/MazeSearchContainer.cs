namespace MediaBrowser.Providers.TV.TvMaze.Models
{
    /// <summary>
    /// A DeSerializable container to display the Show result and score of how close to query it is.
    /// </summary>
    public class MazeSearchContainerShow
    {
        public MazeSeries show { get; set; }
        /// <summary>
        /// Score rank of how close result is to query.
        /// </summary>
        public double score { get; set; }
    }
    /// <summary>
    /// A DeSerializable container to display the Person result and score of how close to query it is.
    /// </summary>
    public class MazeSearchContainerPerson
    {
        public MazeHuman person { get; set; }
        /// <summary>
        /// Score rank of how close result is to query.
        /// </summary>
        public double score { get; set; }
    }
}
