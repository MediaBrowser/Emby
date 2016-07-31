using MediaBrowser.Controller.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Health
{
    /// <summary>
    /// Health message regarding Emby configuration.
    /// </summary>
    /// <seealso cref="MediaBrowser.Controller.Health.HealthMessageBase" />
    public class HealthMessageConfigurationHint : HealthMessageBase
    {
        private readonly string messagePhrase;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthMessageConfigurationHint"/> class.
        /// </summary>
        /// <param name="reporter">The reporter.</param>
        /// <param name="infoGuid">The information unique identifier.</param>
        /// <param name="verdict">The verdict.</param>
        /// <param name="infoCategory">The information category.</param>
        /// <param name="localizationManager">The localization manager.</param>
        /// <param name="messagePhrase">The message phrase.</param>
        public HealthMessageConfigurationHint(Object reporter, Guid infoGuid, HealthMessageVerdict verdict, String infoCategory, ILocalizationManager localizationManager, string messagePhrase)
            : base (reporter, infoGuid, verdict, infoCategory, localizationManager)
        {
            this.messagePhrase = messagePhrase;
        }

        /// <summary>
        /// Gets the type of health message.
        /// </summary>
        public override string InfoType
        {
            get
            {
                return this.LocalizationManager.GetLocalizedString("HealthMessageConfigurationHint");
            }
        }

        /// <summary>
        /// Gets the message text.
        /// </summary>
        /// <value>
        /// The message text.
        /// </value>
        public string MessageText
        {
            get
            {
                return this.LocalizationManager.GetLocalizedString(messagePhrase);
            }
        }

    }
}
