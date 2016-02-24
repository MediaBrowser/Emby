using System.Net;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using static MediaBrowser.Server.Implementations.LiveTv.EmbyTV.EmbyTV;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.LiveTv.Listings
{
    public abstract class BaseListingsProvider
    {
        protected readonly ILogger _logger;
        protected readonly IJsonSerializer _jsonSerializer;

        protected readonly ConcurrentDictionary<string, Station> _stations = new ConcurrentDictionary<string, Station>();

        public BaseListingsProvider(ILogger logger, IJsonSerializer jsonSerializer)
        {
            _logger = logger;
            _jsonSerializer = jsonSerializer;
        }

        protected abstract Task<IEnumerable<Station>> GetStations(ListingsProviderInfo info, CancellationToken cancellationToken);

        protected abstract Task<IEnumerable<ProgramInfo>> GetProgramsAsyncInternal(ListingsProviderInfo info, string station, IEnumerable<string> dates, CancellationToken cancellationToken);


        private static List<string> GetScheduleRequestDates(DateTime startDateUtc, DateTime endDateUtc)
        {
            List<string> dates = new List<string>();

            var start = new List<DateTime> { startDateUtc, startDateUtc.ToLocalTime() }.Min().Date;
            var end = new List<DateTime> { endDateUtc, endDateUtc.ToLocalTime() }.Max().Date;

            while (start <= end)
            {
                dates.Add(start.ToString("yyyy-MM-dd"));
                start = start.AddDays(1);
            }

            return dates;
        }

        public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(ListingsProviderInfo info, ChannelInfo channel, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
        {
            IEnumerable<ProgramInfo> programsInfo = new List<ProgramInfo>();

            if (channel.GuideGroup != info.GuideGroup || string.IsNullOrWhiteSpace(info.ListingsId)) { return programsInfo; }

            var channelNumber = channel.Number;
            var channelName = channel.Name;

            var dates = GetScheduleRequestDates(startDateUtc, endDateUtc);

            var station = GetStation(channel);

            if (station == null)
            {
                _logger.Info("No Guide Station found for channel {0} with name {1}", channelNumber, channelName);
                return programsInfo;
            }

            programsInfo = await GetProgramsAsyncInternal(info, station.Id, dates, cancellationToken);

            foreach (var program in programsInfo)
            {
                program.Id = CreateTag("Id", program.Id) + CreateTag("Time", program.StartDate.ToString("u")) + channel.Id;
                program.ChannelId = channel.Id;
            }

            return programsInfo;
        }
        private Station GetStation(ChannelInfo channel)
        {
            Station station = null;
            if (_stations.TryGetValue(channel.StationId ?? "", out station)) { return station; }
            return GetStation(channel, _stations.Values.Where(s => s.GuideGroup == channel.GuideGroup));
        }
        private Station GetStation(ChannelInfo channel, IEnumerable<Station> filteredStations)
        {
            Station station = null;
            if (!string.IsNullOrWhiteSpace(channel.Number))
            {
                var result = filteredStations.FirstOrDefault(s => s.ChannelNumbers.Contains(channel.Number));
                if (result != null) { return result; }
            }

            if (!string.IsNullOrWhiteSpace(channel.Name))
            {
                var channelName = NormalizeName(channel.Name);

                var result = filteredStations.FirstOrDefault(i => string.Equals(NormalizeName(i.Callsign ?? string.Empty), channelName, StringComparison.OrdinalIgnoreCase));
                if (result != null) { return result; }

                result = filteredStations.FirstOrDefault(i => string.Equals(NormalizeName(i.Name ?? string.Empty), channelName, StringComparison.OrdinalIgnoreCase));
                if (result != null) { return result; }
            }
            return station;
        }

        private string NormalizeName(string value)
        {
            return value.Replace(" ", string.Empty).Replace("-", string.Empty);
        }

        public async Task AddMetadata(ListingsProviderInfo info, List<ChannelInfo> channels, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(info.ListingsId))
            {
                throw new Exception("ListingsId required");
            }

            var channelsFiltered = channels.Where(c => c.GuideGroup == info.GuideGroup);

            _stations.Clear();

            var stations = await GetStations(info, cancellationToken);

            foreach (var channel in channelsFiltered)
            {
                var station = GetStation(channel, stations);
                if (station != null)
                {
                    _stations.TryAdd(CreateTag("Station", station.Id) + CreateTag("GuideGroup", info.GuideGroup), station);
                    if (station.ImageUrl != null)
                    {
                        channel.ImageUrl = station.ImageUrl;
                        channel.HasImage = true;
                    }
                    channel.Name = station.Name ?? station.Callsign;
                    channel.StationId = station.Id;
                    station.GuideGroup = info.GuideGroup;
                }
                else
                {
                    _logger.Info("Listing provider doesnt have data for channel: " + channel.Number + " " + channel.Name);
                }

            }
            _logger.Info("Added " + _stations.Count + " stations to the dictionary");
        }


        private DateTime GetDate(string value)
        {
            var date = DateTime.ParseExact(value, "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture);

            if (date.Kind != DateTimeKind.Utc)
            {
                date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            }
            return date;
        }

    }
    public class Station
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Callsign { get; set; }
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }
        /// <summary>
        /// Gets or sets the number.
        /// </summary>
        /// <value>The number.</value>
        public List<string> ChannelNumbers { get; set; }

        /// <summary>
        /// Get or sets the Id.
        /// </summary>
        /// <value>The id of the channel.</value>
        public string Id { get; set; }

        /// <summary>
        /// Get or sets the Affiliate
        /// </summary>
        /// <value>The id of the channel.</value>
        public string Affiliate { get; set; }

        /// <summary>
        /// Gets or sets the type of the channel.
        /// </summary>
        /// <value>The type of the channel.</value>
        public ChannelType ChannelType { get; set; }

        /// <summary>
        /// Supply the image path if it can be accessed directly from the file system
        /// </summary>
        /// <value>The image path.</value>
        public string ImagePath { get; set; }

        /// <summary>
        /// Supply the image url if it can be downloaded
        /// </summary>
        /// <value>The image URL.</value>
        public string ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is favorite.
        /// </summary>
        /// <value><c>null</c> if [is favorite] contains no value, <c>true</c> if [is favorite]; otherwise, <c>false</c>.</value>
        public string GuideGroup { get; set; }

    }
}