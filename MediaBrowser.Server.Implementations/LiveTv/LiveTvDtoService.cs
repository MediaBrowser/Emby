﻿using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.LiveTv
{
    public class LiveTvDtoService
    {
        private readonly ILogger _logger;
        private readonly IImageProcessor _imageProcessor;

        private readonly IUserDataManager _userDataManager;
        private readonly IDtoService _dtoService;

        public LiveTvDtoService(IDtoService dtoService, IUserDataManager userDataManager, IImageProcessor imageProcessor, ILogger logger)
        {
            _dtoService = dtoService;
            _userDataManager = userDataManager;
            _imageProcessor = imageProcessor;
            _logger = logger;
        }

        public TimerInfoDto GetTimerInfoDto(TimerInfo info, ILiveTvService service, LiveTvProgram program)
        {
            var dto = new TimerInfoDto
            {
                Id = GetInternalTimerId(service.Name, info.Id).ToString("N"),
                ChannelName = info.ChannelName,
                Overview = info.Overview,
                EndDate = info.EndDate,
                Name = info.Name,
                StartDate = info.StartDate,
                ExternalId = info.Id,
                ChannelId = GetInternalChannelId(service.Name, info.ChannelId, info.ChannelName).ToString("N"),
                Status = info.Status,
                SeriesTimerId = string.IsNullOrEmpty(info.SeriesTimerId) ? null : GetInternalSeriesTimerId(service.Name, info.SeriesTimerId).ToString("N"),
                PrePaddingSeconds = info.PrePaddingSeconds,
                PostPaddingSeconds = info.PostPaddingSeconds,
                IsPostPaddingRequired = info.IsPostPaddingRequired,
                IsPrePaddingRequired = info.IsPrePaddingRequired,
                ExternalChannelId = info.ChannelId,
                ExternalSeriesTimerId = info.SeriesTimerId,
                ServiceName = service.Name,
                ExternalProgramId = info.ProgramId,
                Priority = info.Priority,
                RunTimeTicks = (info.EndDate - info.StartDate).Ticks
            };

            if (!string.IsNullOrEmpty(info.ProgramId))
            {
                dto.ProgramId = GetInternalProgramId(service.Name, info.ProgramId).ToString("N");
            }

            if (program != null)
            {
                dto.ProgramInfo = GetProgramInfoDto(program);

                dto.ProgramInfo.TimerId = dto.Id;
                dto.ProgramInfo.SeriesTimerId = dto.SeriesTimerId;
            }

            return dto;
        }

        public SeriesTimerInfoDto GetSeriesTimerInfoDto(SeriesTimerInfo info, ILiveTvService service)
        {
            var dto = new SeriesTimerInfoDto
            {
                Id = GetInternalSeriesTimerId(service.Name, info.Id).ToString("N"),
                ChannelName = info.ChannelName,
                Overview = info.Overview,
                EndDate = info.EndDate,
                Name = info.Name,
                StartDate = info.StartDate,
                ExternalId = info.Id,
                PrePaddingSeconds = info.PrePaddingSeconds,
                PostPaddingSeconds = info.PostPaddingSeconds,
                IsPostPaddingRequired = info.IsPostPaddingRequired,
                IsPrePaddingRequired = info.IsPrePaddingRequired,
                Days = info.Days,
                Priority = info.Priority,
                RecordAnyChannel = info.RecordAnyChannel,
                RecordAnyTime = info.RecordAnyTime,
                RecordNewOnly = info.RecordNewOnly,
                ExternalChannelId = info.ChannelId,
                ExternalProgramId = info.ProgramId,
                ServiceName = service.Name
            };

            if (!string.IsNullOrEmpty(info.ChannelId))
            {
                dto.ChannelId = GetInternalChannelId(service.Name, info.ChannelId, info.ChannelName).ToString("N");
            }

            if (!string.IsNullOrEmpty(info.ProgramId))
            {
                dto.ProgramId = GetInternalProgramId(service.Name, info.ProgramId).ToString("N");
            }

            dto.DayPattern = info.Days == null ? null : GetDayPattern(info.Days);

            return dto;
        }

        public DayPattern? GetDayPattern(List<DayOfWeek> days)
        {
            DayPattern? pattern = null;

            if (days.Count > 0)
            {
                if (days.Count == 7)
                {
                    pattern = DayPattern.Daily;
                }
                else if (days.Count == 2)
                {
                    if (days.Contains(DayOfWeek.Saturday) && days.Contains(DayOfWeek.Sunday))
                    {
                        pattern = DayPattern.Weekends;
                    }
                }
                else if (days.Count == 5)
                {
                    if (days.Contains(DayOfWeek.Monday) && days.Contains(DayOfWeek.Tuesday) && days.Contains(DayOfWeek.Wednesday) && days.Contains(DayOfWeek.Thursday) && days.Contains(DayOfWeek.Friday))
                    {
                        pattern = DayPattern.Weekdays;
                    }
                }
            }

            return pattern;
        }

        /// <summary>
        /// Convert the provider 0-5 scale to our 0-10 scale
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private float? GetClientCommunityRating(float? val)
        {
            if (!val.HasValue)
            {
                return null;
            }

            return val.Value * 2;
        }

        public RecordingInfoDto GetRecordingInfoDto(LiveTvRecording recording, ILiveTvService service, User user = null)
        {
            var info = recording.RecordingInfo;

            var dto = new RecordingInfoDto
            {
                Id = GetInternalRecordingId(service.Name, info.Id).ToString("N"),
                SeriesTimerId = string.IsNullOrEmpty(info.SeriesTimerId) ? null : GetInternalSeriesTimerId(service.Name, info.SeriesTimerId).ToString("N"),
                Type = recording.GetClientTypeName(),
                ChannelName = info.ChannelName,
                Overview = info.Overview,
                EndDate = info.EndDate,
                Name = info.Name,
                StartDate = info.StartDate,
                ExternalId = info.Id,
                ChannelId = GetInternalChannelId(service.Name, info.ChannelId, info.ChannelName).ToString("N"),
                Status = info.Status,
                Path = info.Path,
                Genres = info.Genres,
                IsRepeat = info.IsRepeat,
                EpisodeTitle = info.EpisodeTitle,
                ChannelType = info.ChannelType,
                MediaType = info.ChannelType == ChannelType.Radio ? MediaType.Audio : MediaType.Video,
                CommunityRating = GetClientCommunityRating(info.CommunityRating),
                OfficialRating = info.OfficialRating,
                Audio = info.Audio,
                IsHD = info.IsHD,
                ServiceName = service.Name,
                Url = info.Url,
                IsMovie = info.IsMovie,
                IsSeries = info.IsSeries,
                IsSports = info.IsSports,
                IsLive = info.IsLive,
                IsNews = info.IsNews,
                IsKids = info.IsKids,
                IsPremiere = info.IsPremiere,
                RunTimeTicks = (info.EndDate - info.StartDate).Ticks
            };

            var imageTag = GetImageTag(recording);

            if (imageTag.HasValue)
            {
                dto.ImageTags[ImageType.Primary] = imageTag.Value;
            }

            if (user != null)
            {
                dto.UserData = _dtoService.GetUserItemDataDto(_userDataManager.GetUserData(user.Id, recording.GetUserDataKey()));
            }

            if (!string.IsNullOrEmpty(info.ProgramId))
            {
                dto.ProgramId = GetInternalProgramId(service.Name, info.ProgramId).ToString("N");
            }

            return dto;
        }

        /// <summary>
        /// Gets the channel info dto.
        /// </summary>
        /// <param name="info">The info.</param>
        /// <param name="user">The user.</param>
        /// <returns>ChannelInfoDto.</returns>
        public ChannelInfoDto GetChannelInfoDto(LiveTvChannel info, User user = null)
        {
            var channelInfo = info.ChannelInfo;

            var dto = new ChannelInfoDto
            {
                Name = info.Name,
                ServiceName = info.ServiceName,
                ChannelType = channelInfo.ChannelType,
                Number = channelInfo.Number,
                Type = info.GetClientTypeName(),
                Id = info.Id.ToString("N"),
                MediaType = info.MediaType,
                ExternalId = channelInfo.Id
            };

            if (user != null)
            {
                dto.UserData = _dtoService.GetUserItemDataDto(_userDataManager.GetUserData(user.Id, info.GetUserDataKey()));
            }

            var imageTag = GetImageTag(info);

            if (imageTag.HasValue)
            {
                dto.ImageTags[ImageType.Primary] = imageTag.Value;
            }

            return dto;
        }

        public ProgramInfoDto GetProgramInfoDto(LiveTvProgram item, User user = null)
        {
            var program = item.ProgramInfo;
            
            var dto = new ProgramInfoDto
            {
                Id = GetInternalProgramId(item.ServiceName, program.Id).ToString("N"),
                ChannelId = GetInternalChannelId(item.ServiceName, program.ChannelId, program.ChannelName).ToString("N"),
                Overview = program.Overview,
                EndDate = program.EndDate,
                Genres = program.Genres,
                ExternalId = program.Id,
                Name = program.Name,
                ServiceName = item.ServiceName,
                StartDate = program.StartDate,
                OfficialRating = program.OfficialRating,
                IsHD = program.IsHD,
                OriginalAirDate = program.OriginalAirDate,
                Audio = program.Audio,
                CommunityRating = GetClientCommunityRating(program.CommunityRating),
                AspectRatio = program.AspectRatio,
                IsRepeat = program.IsRepeat,
                EpisodeTitle = program.EpisodeTitle,
                ChannelName = program.ChannelName,
                IsMovie = program.IsMovie,
                IsSeries = program.IsSeries,
                IsSports = program.IsSports,
                IsLive = program.IsLive,
                IsNews = program.IsNews,
                IsKids = program.IsKids,
                IsPremiere = program.IsPremiere,
                RunTimeTicks = (program.EndDate - program.StartDate).Ticks
            };

            if (user != null)
            {
                dto.UserData = _dtoService.GetUserItemDataDto(_userDataManager.GetUserData(user.Id, item.GetUserDataKey()));
            }

            return dto;
        }

        private Guid? GetImageTag(BaseItem info)
        {
            var path = info.PrimaryImagePath;

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                return _imageProcessor.GetImageCacheTag(info, ImageType.Primary, path);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting image info for {0}", ex, info.Name);
            }

            return null;
        }

        public Guid GetInternalChannelId(string serviceName, string externalId, string channelName)
        {
            var name = serviceName + externalId + channelName;

            return name.ToLower().GetMBId(typeof(LiveTvChannel));
        }

        public Guid GetInternalTimerId(string serviceName, string externalId)
        {
            var name = serviceName + externalId;

            return name.ToLower().GetMD5();
        }

        public Guid GetInternalSeriesTimerId(string serviceName, string externalId)
        {
            var name = serviceName + externalId;

            return name.ToLower().GetMD5();
        }

        public Guid GetInternalProgramId(string serviceName, string externalId)
        {
            var name = serviceName + externalId;

            return name.ToLower().GetMD5();
        }

        public Guid GetInternalRecordingId(string serviceName, string externalId)
        {
            var name = serviceName + externalId;

            return name.ToLower().GetMD5();
        }

        public async Task<TimerInfo> GetTimerInfo(TimerInfoDto dto, bool isNew, ILiveTvManager liveTv, CancellationToken cancellationToken)
        {
            var info = new TimerInfo
            {
                ChannelName = dto.ChannelName,
                Overview = dto.Overview,
                EndDate = dto.EndDate,
                Name = dto.Name,
                StartDate = dto.StartDate,
                Status = dto.Status,
                PrePaddingSeconds = dto.PrePaddingSeconds,
                PostPaddingSeconds = dto.PostPaddingSeconds,
                IsPostPaddingRequired = dto.IsPostPaddingRequired,
                IsPrePaddingRequired = dto.IsPrePaddingRequired,
                Priority = dto.Priority,
                SeriesTimerId = dto.ExternalSeriesTimerId,
                ProgramId = dto.ExternalProgramId,
                ChannelId = dto.ExternalChannelId,
                Id = dto.ExternalId
            };

            // Convert internal server id's to external tv provider id's
            if (!isNew && !string.IsNullOrEmpty(dto.Id) && string.IsNullOrEmpty(info.Id))
            {
                var timer = await liveTv.GetSeriesTimer(dto.Id, cancellationToken).ConfigureAwait(false);

                info.Id = timer.ExternalId;
            }

            if (!string.IsNullOrEmpty(dto.ChannelId) && string.IsNullOrEmpty(info.ChannelId))
            {
                var channel = await liveTv.GetChannel(dto.ChannelId, cancellationToken).ConfigureAwait(false);

                if (channel != null)
                {
                    info.ChannelId = channel.ExternalId;
                }
            }

            if (!string.IsNullOrEmpty(dto.ProgramId) && string.IsNullOrEmpty(info.ProgramId))
            {
                var program = await liveTv.GetProgram(dto.ProgramId, cancellationToken).ConfigureAwait(false);

                if (program != null)
                {
                    info.ProgramId = program.ExternalId;
                }
            }

            if (!string.IsNullOrEmpty(dto.SeriesTimerId) && string.IsNullOrEmpty(info.SeriesTimerId))
            {
                var timer = await liveTv.GetSeriesTimer(dto.SeriesTimerId, cancellationToken).ConfigureAwait(false);

                if (timer != null)
                {
                    info.SeriesTimerId = timer.ExternalId;
                }
            }

            return info;
        }

        public async Task<SeriesTimerInfo> GetSeriesTimerInfo(SeriesTimerInfoDto dto, bool isNew, ILiveTvManager liveTv, CancellationToken cancellationToken)
        {
            var info = new SeriesTimerInfo
            {
                ChannelName = dto.ChannelName,
                Overview = dto.Overview,
                EndDate = dto.EndDate,
                Name = dto.Name,
                StartDate = dto.StartDate,
                PrePaddingSeconds = dto.PrePaddingSeconds,
                PostPaddingSeconds = dto.PostPaddingSeconds,
                IsPostPaddingRequired = dto.IsPostPaddingRequired,
                IsPrePaddingRequired = dto.IsPrePaddingRequired,
                Days = dto.Days,
                Priority = dto.Priority,
                RecordAnyChannel = dto.RecordAnyChannel,
                RecordAnyTime = dto.RecordAnyTime,
                RecordNewOnly = dto.RecordNewOnly,
                ProgramId = dto.ExternalProgramId,
                ChannelId = dto.ExternalChannelId,
                Id = dto.ExternalId
            };

            // Convert internal server id's to external tv provider id's
            if (!isNew && !string.IsNullOrEmpty(dto.Id) && string.IsNullOrEmpty(info.Id))
            {
                var timer = await liveTv.GetSeriesTimer(dto.Id, cancellationToken).ConfigureAwait(false);

                info.Id = timer.ExternalId;
            }

            if (!string.IsNullOrEmpty(dto.ChannelId) && string.IsNullOrEmpty(info.ChannelId))
            {
                var channel = await liveTv.GetChannel(dto.ChannelId, cancellationToken).ConfigureAwait(false);

                if (channel != null)
                {
                    info.ChannelId = channel.ExternalId;
                }
            }

            if (!string.IsNullOrEmpty(dto.ProgramId) && string.IsNullOrEmpty(info.ProgramId))
            {
                var program = await liveTv.GetProgram(dto.ProgramId, cancellationToken).ConfigureAwait(false);

                if (program != null)
                {
                    info.ProgramId = program.ExternalId;
                }
            }

            return info;
        }
    }
}
