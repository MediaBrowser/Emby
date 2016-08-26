using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.TV.TvMaze.Models
{
    /// <summary>
    /// Information about a particular Actor.
    /// </summary>
    public class MazeCastMember
    {
        /// <summary>
        /// Actor's information.
        /// </summary>
        public MazeHuman person { get; set; }
        /// <summary>
        /// Actors' Character information.
        /// </summary>
        public MazeHuman character { get; set; }
        /// <summary>
        /// Collection of all Shows the Actor has played a part in.
        /// </summary>
        public IReadOnlyCollection<MazeCastCredit> castCredit { get; set; }
        /// <summary>
        /// Collection of all shows the Actor has done some behind the scenes work in.
        /// </summary>
        public IReadOnlyCollection<MazeCrewCredit> crewCredit { get; set; }
    }
}
