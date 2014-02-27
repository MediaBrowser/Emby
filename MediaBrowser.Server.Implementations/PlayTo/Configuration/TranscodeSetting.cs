using MediaBrowser.Server.Implementations.PlayTo.Managed;
using System;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace MediaBrowser.Server.Implementations.PlayTo.Configuration
{
    [Serializable]
    public class TranscodeSettings
    {
        /// <summary>
        /// Gets or sets the container.
        /// </summary>
        /// <value>
        /// The container.
        /// </value>
        [XmlAttribute("Container")]
        public string Container
        { get; set; }

        /// <summary>
        /// Gets or sets the target container.
        /// </summary>
        /// <value>
        /// The target container.
        /// </value>
        [XmlAttribute("TargetContainer")]
        public string TargetContainer
        { get; set; }

        /// <summary>
        /// The default transcoding settings
        /// </summary>
        [XmlIgnore]
        public static TranscodeSettings[] Default = new TranscodeSettings[] { new TranscodeSettings { Container = "mkv", TargetContainer = "ts" }, 
                                                                              new TranscodeSettings { Container = "flac", TargetContainer = "mp3" },
                                                                              new TranscodeSettings { Container = "m4a", TargetContainer = "mp3" }};
        /// <summary>
        /// Gets the profile settings.
        /// </summary>
        /// <param name="deviceProperties">The device properties.</param>
        /// <returns>The TranscodeSettings for the device</returns>
        public static TranscodeSettings[] GetProfileSettings(DeviceProperties deviceProperties)
        {
            foreach (DlnaProfile profile in PlayToConfiguration.Profiles)
            {
                if (!string.IsNullOrEmpty(profile.FriendlyName))
                {
                    if (!Regex.IsMatch(deviceProperties.Name, profile.FriendlyName))
                        continue;
                }

                if (!string.IsNullOrEmpty(profile.ModelNumber))
                {
                    if (!Regex.IsMatch(deviceProperties.ModelNumber, profile.ModelNumber))
                        continue;
                }

                if (!string.IsNullOrEmpty(profile.ModelName))
                {
                    if (!Regex.IsMatch(deviceProperties.ModelName, profile.ModelName))
                        continue;
                }

                deviceProperties.DisplayName = profile.Name;
                deviceProperties.ClientType = profile.ClientType;
                return profile.TranscodeSettings;

            }

            // Since we don't have alot of info about different devices we go down the safe
            // route abd use the default transcoding settings if no profile exist
            return TranscodeSettings.Default;
        }
    }
}
