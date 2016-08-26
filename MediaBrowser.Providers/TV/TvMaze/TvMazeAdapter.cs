using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Providers.TV.TvMaze.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.TV.TvMaze
{
    class TvMazeAdapter
    {
        public static Series Convert(MazeSeries mazeSeries)
        {
            var series = new Series();

            SetProviderIds(series, mazeSeries.externals, mazeSeries.id);

            series.Name = mazeSeries.name;
            series.Genres = mazeSeries.genres.ToList();

            // TODO: Do we have a Series property for original language?
            //series = mazeSeries.language;

            if (mazeSeries.network != null && !string.IsNullOrWhiteSpace(mazeSeries.network.name))
            {
                var networkName = mazeSeries.network.name;
                if (mazeSeries.network.country != null && !string.IsNullOrWhiteSpace(mazeSeries.network.country.code))
                {
                    networkName = string.Format("{0} ({1})", mazeSeries.network.name, mazeSeries.network.country.code);
                }

                series.Studios.Add(networkName);
            }

            if (mazeSeries.premiered.HasValue)
            {
                series.PremiereDate = mazeSeries.premiered.Value;
                series.ProductionYear = mazeSeries.premiered.Value.Year;
            }

            if (mazeSeries.rating != null && mazeSeries.rating.average.HasValue)
            {
                series.CommunityRating = (float)mazeSeries.rating.average.Value;
            }

            if (mazeSeries.runtime.HasValue)
            {
                series.RunTimeTicks = TimeSpan.FromMinutes(mazeSeries.runtime.Value).Ticks;
            }   
            
            switch (mazeSeries.status.ToLower())
            {
                case "running":
                    series.Status = SeriesStatus.Continuing;
                    break;
                case "ended":
                    series.Status = SeriesStatus.Ended;
                    break;
            }

            series.Overview = StripHtml(mazeSeries.summary);

            series.HomePageUrl = mazeSeries.url.ToString();

            return series;
        }

        public static Episode Convert(MazeEpisode mazeEpisode)
        {
            var episode = new Episode();

            episode.ProviderIds[MetadataProviders.TvMaze.ToString()] = mazeEpisode.id.ToString();

            episode.Name = mazeEpisode.name;

            episode.IndexNumber = mazeEpisode.number;
            episode.ParentIndexNumber = mazeEpisode.season;

            if (mazeEpisode.airdate.HasValue)
            {
                episode.PremiereDate = mazeEpisode.airdate.Value;
            }

            if (mazeEpisode.runtime.HasValue)
            {
                episode.RunTimeTicks = TimeSpan.FromMinutes(mazeEpisode.runtime.Value).Ticks;
            }

            episode.Overview = StripHtml(mazeEpisode.summary);

            return episode;
        }

        public static Season Convert(MazeSeason mazeSeason)
        {
            var season = new Season();

            season.ProviderIds[MetadataProviders.TvMaze.ToString()] = mazeSeason.id.ToString();

            season.Name = mazeSeason.name;

            season.IndexNumber = mazeSeason.number;

            if (mazeSeason.network != null && !string.IsNullOrWhiteSpace(mazeSeason.network.name))
            {
                var networkName = mazeSeason.network.name;
                if (mazeSeason.network.country != null && !string.IsNullOrWhiteSpace(mazeSeason.network.country.code))
                {
                    networkName = string.Format("{0} ({1})", mazeSeason.network.name, mazeSeason.network.country.code);
                }

                season.Studios.Add(networkName);
            }

            if (mazeSeason.premiereDate.HasValue)
            {
                season.PremiereDate = mazeSeason.premiereDate.Value;
                season.ProductionYear = mazeSeason.premiereDate.Value.Year;
            }

            if (mazeSeason.endDate.HasValue)
            {
                season.EndDate = mazeSeason.endDate.Value;
            }

            season.Overview = StripHtml(mazeSeason.summary);

            return season;
        }

        public static PersonInfo Convert(MazeCastMember mazeMember)
        {
            var personInfo = new PersonInfo();

            personInfo.ProviderIds[MetadataProviders.TvMaze.ToString()] = mazeMember.person.id.ToString();

            personInfo.Name = mazeMember.person.name;
            personInfo.Role = mazeMember.character.name;
            personInfo.Type = PersonType.Actor;

            if (mazeMember.person.image != null && mazeMember.person.image.original != null)
            {
                personInfo.ImageUrl = mazeMember.person.image.original.ToString();
            }

            return personInfo;
        }

        private static void SetProviderIds(BaseItem item, MazeExternals externals, uint mazeId)
        {
            item.ProviderIds[MetadataProviders.TvMaze.ToString()] = mazeId.ToString();

            if (externals.thetvdb.HasValue)
            {
                item.ProviderIds[MetadataProviders.Tvdb.ToString()] = externals.thetvdb.Value.ToString();
            }

            if (externals.tvrage.HasValue)
            {
                item.ProviderIds[MetadataProviders.TvRage.ToString()] = externals.tvrage.Value.ToString();
            }

            if (!string.IsNullOrWhiteSpace(externals.imdb))
            {
                item.ProviderIds[MetadataProviders.Imdb.ToString()] = externals.imdb;
            }
        }

        private static string StripHtml(string content)
        {
            var result = content.Replace("<br>", Environment.NewLine);
            result = result.Replace("<p>", "");
            result = result.Replace("</p>", "");
            result = result.Replace("<i>", "");
            result = result.Replace("</i>", "");
            result = result.Replace("<b>", "");
            result = result.Replace("</b>", "");
            result = result.Replace("<li>", "");
            result = result.Replace("</li>", "");
            result = result.Replace("<ul>", "");
            result = result.Replace("</ul>", "");
            result = result.Replace("<div>", "");
            result = result.Replace("<br />", "");
            result = result.Replace("<br/>", "");
            result = result.Replace("<em>", "");
            result = result.Replace("<em/>", "");

            return result;
        }
    }
}
