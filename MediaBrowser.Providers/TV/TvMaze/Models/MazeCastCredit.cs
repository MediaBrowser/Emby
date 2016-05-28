namespace MediaBrowser.Providers.TV.TvMaze.Models
{
    /// <summary>
    /// Another show the Actor has been in.
    /// </summary>
    public class MazeCastCredit
    {
        /// <summary>
        /// Accessible links for relevant Cast information.
        /// </summary>
        public CastLink _links { get; set; }
        /// <summary>
        /// Accessible links for relevant Cast information.
        /// </summary>
        public class CastLink
        {
            /// <summary>
            /// Link to the Character Page that this actor plays in a particular show.
            /// </summary>
            public MazeLink character { get; set; }
            /// <summary>
            /// Link to the show that this actor stars in.
            /// </summary>
            public MazeLink show { get; set; }
        }
    }
}
