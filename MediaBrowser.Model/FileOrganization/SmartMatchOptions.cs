using System.Collections.Generic;

namespace MediaBrowser.Model.FileOrganization
{
    public class SmartMatchOptions
    {
        /// <summary>
        /// Gets or sets a list of smart match entries.
        /// </summary>
        /// <value>The smart match entries.</value>
        public List<SmartMatchInfo> SmartMatchInfos { get; set; }

        public SmartMatchOptions()
        {
            SmartMatchInfos = new List<SmartMatchInfo>();
        }
    }
}
