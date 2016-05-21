using MediaBrowser.Model.Dto;
using System.Collections.Generic;

namespace MediaBrowser.Controller.Entities.SubTypes
{
    public interface IHasSubType
    {
        string SubTypeId { get; set; }
    }
}
