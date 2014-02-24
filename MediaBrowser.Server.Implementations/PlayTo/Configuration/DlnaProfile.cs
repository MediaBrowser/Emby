using System;
using System.Xml.Serialization;

namespace MediaBrowser.Server.Implementations.PlayTo.Configuration
{    
        [Serializable]
        public class DlnaProfile
        {
            /// <summary>
            /// Gets or sets the name to be displayed.
            /// </summary>
            /// <value>
            /// The name.
            /// </value>
            [XmlAttribute("DisplayName")]
            public string Name
            { get; set; }

            /// <summary>
            /// Gets or sets the type of the client.
            /// </summary>
            /// <value>
            /// The type of the client.
            /// </value>
            [XmlAttribute("ClientType")]
            public string ClientType
            { get; set; }

            /// <summary>
            /// Gets or sets the name of the friendly.
            /// </summary>
            /// <value>
            /// The name of the friendly.
            /// </value>
            [XmlElement("FriendlyName")]
            public string FriendlyName
            { get; set; }

            /// <summary>
            /// Gets or sets the model number.
            /// </summary>
            /// <value>
            /// The model number.
            /// </value>
            [XmlElement("ModelNumber")]
            public string ModelNumber
            { get; set; }

            /// <summary>
            /// Gets or sets the name of the model.
            /// </summary>
            /// <value>
            /// The name of the model.
            /// </value>
            [XmlElement("ModelName")]
            public string ModelName
            { get; set; }

            /// <summary>
            /// Gets or sets the transcode settings.
            /// </summary>
            /// <value>
            /// The transcode settings.
            /// </value>
            [XmlElement("TranscodeSettings")]
            public TranscodeSettings[] TranscodeSettings
            { get; set; }
        }          
}
