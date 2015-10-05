using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using System;
using System.Collections.Generic;

namespace MediaBrowser.Controller.TV
{
    public class SeriesCreatedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the collection.
        /// </summary>
        /// <value>The collection.</value>
        public Series TvShow { get; set; }

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        /// <value>The options.</value>
        public SeriesCreationOptions Options { get; set; }
    }
}
