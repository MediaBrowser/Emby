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
    /// Health message class with localized strings.
    /// </summary>
    public class HealthMessageLocalized : HealthMessage
    {
        private readonly string _messageText;
        private readonly string _severityText;
        private readonly string _messageTypeText;
        private readonly string _areaText;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="HealthMessageLocalized"/> class.
        /// </summary>
        public HealthMessageLocalized(HealthMessage baseMessage, ILocalizationManager localizationManager)
            : base(baseMessage)
        {
            if (localizationManager != null)
            {
                if (!string.IsNullOrWhiteSpace(MessageType))
                {
                    _messageTypeText = localizationManager.GetLocalizedString("HealthMessageType" + MessageType);
                }
                if (!string.IsNullOrWhiteSpace(MessageId))
                {
                    var idText = localizationManager.GetLocalizedString(MessageId);
                    _messageText = string.Format(idText, this.Parameters);
                }

                _severityText = localizationManager.GetLocalizedString("HealthMessageSeverity" + Severity.ToString());

                if (!string.IsNullOrWhiteSpace(Area))
                {
                    var areaStrings = Area.Split('|').Select(e => localizationManager.GetLocalizedString(e));

                    _areaText = string.Join(" >> ", areaStrings);
                }
            }
        }

        /// <summary>
        /// Gets the type of health message.
        /// </summary>
        public virtual string MessageTypeText
        {
            get
            {
                return _messageTypeText;
            }
        }

        /// <summary>
        /// Gets the message text.
        /// </summary>
        public string MessageText
        {
            get
            {
                return _messageText;
            }
        }

        /// <summary>
        /// Gets the area text.
        /// </summary>
        public string AreaText
        {
            get
            {
                return _areaText;
            }
        }

        /// <summary>
        /// Gets the severity text.
        /// </summary>
        public string SeverityText
        {
            get
            {
                return _severityText;
            }
        }
    }
}
