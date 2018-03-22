using System;

namespace MediaBrowser.Model.Configuration
{
    public class LibraryOptions
    {
        public bool EnableArchiveMediaFiles { get; set; }
        public bool EnablePhotos { get; set; }
        public bool EnableRealtimeMonitor { get; set; }
        public bool EnableChapterImageExtraction { get; set; }
        public bool ExtractChapterImagesDuringLibraryScan { get; set; }
        public bool DownloadImagesInAdvance { get; set; }
        public MediaPathInfo[] PathInfos { get; set; }

        public bool SaveLocalMetadata { get; set; }
        public bool EnableInternetProviders { get; set; }
        public bool ImportMissingEpisodes { get; set; }
        public bool EnableAutomaticSeriesGrouping { get; set; }
        public bool EnableEmbeddedTitles { get; set; }

        public int AutomaticRefreshIntervalDays { get; set; }

        /// <summary>
        /// Gets or sets the preferred metadata language.
        /// </summary>
        /// <value>The preferred metadata language.</value>
        public string PreferredMetadataLanguage { get; set; }

        /// <summary>
        /// Gets or sets the metadata country code.
        /// </summary>
        /// <value>The metadata country code.</value>
        public string MetadataCountryCode { get; set; }

        public string SeasonZeroDisplayName { get; set; }
        public string[] MetadataSavers { get; set; }
        public string[] LocalMetadataReaders { get; set; }
        public string[] LocalMetadataReaderOrder { get; set; }

        public string[] SubtitleFetchers { get; set; }
        public string[] SubtitleFetcherOrder { get; set; }

        public bool SkipSubtitlesIfEmbeddedSubtitlesPresent { get; set; }
        public bool SkipSubtitlesIfAudioTrackMatches { get; set; }
        public string[] SubtitleDownloadLanguages { get; set; }
        public bool RequirePerfectSubtitleMatch { get; set; }

        public TypeOptions[] TypeOptions { get; set; }

        public TypeOptions GetTypeOptions(string type)
        {
            foreach (var options in TypeOptions)
            {
                if (string.Equals(options.Type, type, StringComparison.OrdinalIgnoreCase))
                {
                    return options;
                }
            }

            return null;
        }

        public LibraryOptions()
        {
            TypeOptions = new TypeOptions[] { };

            SkipSubtitlesIfAudioTrackMatches = true;
            RequirePerfectSubtitleMatch = true;

            EnablePhotos = true;
            EnableRealtimeMonitor = true;
            PathInfos = new MediaPathInfo[] { };
            EnableInternetProviders = true;
            EnableAutomaticSeriesGrouping = true;
            SeasonZeroDisplayName = "Specials";
        }
    }

    public class MediaPathInfo
    {
        public string Path { get; set; }
        public string NetworkPath { get; set; }
    }

    public class TypeOptions
    {
        public string Type { get; set; }
        public string[] MetadataFetchers { get; set; }
        public string[] MetadataFetcherOrder { get; set; }

        public string[] ImageFetchers { get; set; }
        public string[] ImageFetcherOrder { get; set; }

        public TypeOptions()
        {
            MetadataFetchers = new string[] { };
            MetadataFetcherOrder = new string[] { };
            ImageFetchers = new string[] { };
            ImageFetcherOrder = new string[] { };
        }
    }
}
