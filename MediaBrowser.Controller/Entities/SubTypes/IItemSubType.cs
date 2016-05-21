using MediaBrowser.Model.Dto;
using System.Collections.Generic;

namespace MediaBrowser.Controller.Entities.SubTypes
{
    public interface IItemSubType
    {
    }

    public interface IItemSubType<TBaseItemType> : IItemSubType
        where TBaseItemType : BaseItem, IHasSubType
    {
         string Name { get; }

        string Key { get; }

        bool Supports(IHasSubType item);
    }
}
