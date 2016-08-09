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
    public class HealthMessage
    {
        private readonly string _messageId;
        private readonly HealthMessageSeverity _severity;
        private readonly string _messageType;
        private readonly Type _reportingType;
        private readonly string _area;
        private readonly object[] _parameters;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthMessage" /> class.
        /// </summary>
        /// <param name="reporter">The reporter.</param>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="severity">The severity.</param>
        /// <param name="area">The area.</param>
        /// <param name="parameters">The parameters.</param>
        public HealthMessage(object reporter, string messageId, HealthMessageType messageType, HealthMessageSeverity severity, string area, params object[] parameters)
            : this(reporter, messageId, messageType.ToString(), severity, area, parameters)
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthMessage" /> class.
        /// </summary>
        protected HealthMessage(object reporter, string messageId, string messageType, HealthMessageSeverity severity, string area, params object[] parameters)
        {
            _reportingType = reporter.GetType();
            _messageId = messageId;
            _severity = severity;
            _messageType = messageType;
            _area = area;
            _parameters = parameters;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthMessage"/> class.
        /// </summary>
        /// <param name="baseMessage">The base message.</param>
        protected HealthMessage(HealthMessage baseMessage)
        {
            _reportingType = baseMessage._reportingType;
            _messageId = baseMessage._messageId;
            _severity = baseMessage._severity;
            _messageType = baseMessage._messageType;
            _area = baseMessage._area;
            _parameters = baseMessage._parameters;
        }

        /// <summary>
        /// Gets the type of health message.
        /// </summary>
        public string MessageType
        {
            get
            {
                return _messageType;
            }
        }

        /// <summary>
        /// Gets the message id.
        /// </summary>
        public string MessageId
        {
            get
            {
                return _messageId;
            }
        }

        /// <summary>
        /// Gets the area.
        /// </summary>
        public string Area
        {
            get
            {
                return _area;
            }
        }

        /// <summary>
        /// Gets the severity.
        /// </summary>
        public HealthMessageSeverity Severity
        {
            get
            {
                return _severity;
            }
        }

        /// <summary>
        /// Gets the reporting component type.
        /// </summary>
        public Type ReportingType
        {
            get
            {
                return _reportingType;
            }
        }

        protected object[] Parameters
        {
            get
            {
                return _parameters;
            }
        }

        public virtual HealthMessageLocalized ToLocalized(ILocalizationManager localizationManager)
        {
            return new HealthMessageLocalized(this, localizationManager);
        }
    }
}
