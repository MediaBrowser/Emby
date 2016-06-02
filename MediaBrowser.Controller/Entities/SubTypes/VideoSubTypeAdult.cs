using MediaBrowser.Model.Dto;
using System.Collections.Generic;

namespace MediaBrowser.Controller.Entities.SubTypes
{
    public class VideoSubTypeAdult : IItemSubType<Video>
    {
        public string Name
        {
            get { return "Adult Video"; }
        }

        public string Key
        {
            get { return "VideoSubTypeAdult"; }
        }

        public bool Supports(IHasSubType item)
        {
            return item is Video;
        }
    }
}
