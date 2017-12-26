using MediaBrowser.Model.Dto;
using System.Collections.Generic;
using MediaBrowser.Model.Entities;
using System;

namespace MediaBrowser.Controller.Entities
{
    public interface IHasMediaSources
    {
        /// <summary>
        /// Gets the media sources.
        /// </summary>
        /// <param name="enablePathSubstitution">if set to <c>true</c> [enable path substitution].</param>
        /// <returns>Task{IEnumerable{MediaSourceInfo}}.</returns>
        List<MediaSourceInfo> GetMediaSources(bool enablePathSubstitution);
        List<MediaStream> GetMediaStreams();
        Guid Id { get; set; }
        long? RunTimeTicks { get; set; }
        string Path { get; }
    }
}
