using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Localization;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Querying;
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Health
{
    /// <summary>
    /// Base class for health messages.
    /// </summary>
    public abstract class HealthMessageBase
    {
        private readonly Guid infoGuid;
        private readonly HealthMessageVerdict verdict;
        private readonly Type reportingType;
        private readonly String infoCategory;
        private readonly ILocalizationManager localizationManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthMessageBase"/> class.
        /// </summary>
        /// <param name="reporter">The reporter.</param>
        /// <param name="infoGuid">The information unique identifier.</param>
        /// <param name="verdict">The verdict.</param>
        /// <param name="infoCategory">The information category.</param>
        protected HealthMessageBase(Object reporter, Guid infoGuid, HealthMessageVerdict verdict, String infoCategory, ILocalizationManager localizationManager)
        {
            this.reportingType = reporter.GetType();
            this.infoGuid = infoGuid;
            this.verdict = verdict;
            this.infoCategory = infoCategory;
            this.localizationManager = localizationManager;
        }

        /// <summary>
        /// Gets the type of health message.
        /// </summary>
        public abstract string InfoType { get; }

        /// <summary>
        /// Gets the unique identifier for this message.
        /// </summary>
        /// <value>
        /// The unique identifier for this message.
        /// </value>
        public Guid InfoGuid
        {
            get
            {
                return this.infoGuid;
            }
        }

        /// <summary>
        /// Gets the verdict.
        /// </summary>
        /// <value>
        /// The verdict.
        /// </value>
        public HealthMessageVerdict Verdict
        {
            get
            {
                return this.verdict;
            }
        }

        /// <summary>
        /// Gets the reporting component type.
        /// </summary>
        /// <value>
        /// The reporting component type.
        /// </value>
        public Type ReportingType
        {
            get
            {
                return this.reportingType;
            }
        }

        /// <summary>
        /// Gets the localization manager.
        /// </summary>
        /// <value>
        /// The localization manager.
        /// </value>
        protected ILocalizationManager LocalizationManager
        {
            get
            {
                return this.localizationManager;
            }
        }
    }
}
