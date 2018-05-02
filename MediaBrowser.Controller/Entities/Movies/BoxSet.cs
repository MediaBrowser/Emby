using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Controller.Entities.Movies
{
    /// <summary>
    /// Class BoxSet
    /// </summary>
    public class BoxSet : Folder, IHasTrailers, IHasDisplayOrder, IHasLookupInfo<BoxSetInfo>
    {
        public BoxSet()
        {
            RemoteTrailers = EmptyMediaUrlArray;
            LocalTrailerIds = new Guid[] {};
            RemoteTrailerIds = new Guid[] {};

            DisplayOrder = ItemSortBy.PremiereDate;
        }

        [IgnoreDataMember]
        protected override bool FilterLinkedChildrenPerUser
        {
            get
            {
                return true;
            }
        }

        [IgnoreDataMember]
        public override bool SupportsInheritedParentImages
        {
            get
            {
                return false;
            }
        }

        [IgnoreDataMember]
        public override bool SupportsPeople
        {
            get { return true; }
        }

        public Guid[] LocalTrailerIds { get; set; }
        public Guid[] RemoteTrailerIds { get; set; }

        /// <summary>
        /// Gets or sets the remote trailers.
        /// </summary>
        /// <value>The remote trailers.</value>
        public MediaUrl[] RemoteTrailers { get; set; }

        /// <summary>
        /// Gets or sets the display order.
        /// </summary>
        /// <value>The display order.</value>
        public string DisplayOrder { get; set; }

        protected override bool GetBlockUnratedValue(UserPolicy config)
        {
            return config.BlockUnratedItems.Contains(UnratedItem.Movie);
        }

        public override double? GetDefaultPrimaryImageAspectRatio()
        {
            double value = 2;
            value /= 3;

            return value;
        }

        public override UnratedItem GetBlockUnratedType()
        {
            return UnratedItem.Movie;
        }

        protected override IEnumerable<BaseItem> GetNonCachedChildren(IDirectoryService directoryService)
        {
            if (IsLegacyBoxSet)
            {
                return base.GetNonCachedChildren(directoryService);
            }
            return new List<BaseItem>();
        }

        protected override List<BaseItem> LoadChildren()
        {
            if (IsLegacyBoxSet)
            {
                return base.LoadChildren();
            }

            // Save a trip to the database
            return new List<BaseItem>();
        }

        [IgnoreDataMember]
        private bool IsLegacyBoxSet
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                {
                    return false;
                }

                if (LinkedChildren.Length > 0)
                {
                    return false;
                }

                return !FileSystem.ContainsSubPath(ConfigurationManager.ApplicationPaths.DataPath, Path);
            }
        }

        [IgnoreDataMember]
        public override bool IsPreSorted
        {
            get
            {
                return true;
            }
        }

        public override bool IsAuthorizedToDelete(User user, List<Folder> allCollectionFolders)
        {
            return true;
        }

        public override bool IsSaveLocalMetadataEnabled()
        {
            return true;
        }

        public override List<BaseItem> GetChildren(User user, bool includeLinkedChildren)
        {
            var children = base.GetChildren(user, includeLinkedChildren);

            if (string.Equals(DisplayOrder, ItemSortBy.SortName, StringComparison.OrdinalIgnoreCase))
            {
                // Sort by name
                return LibraryManager.Sort(children, user, new[] { ItemSortBy.SortName }, SortOrder.Ascending).ToList();
            }

            if (string.Equals(DisplayOrder, ItemSortBy.PremiereDate, StringComparison.OrdinalIgnoreCase))
            {
                // Sort by release date
                return LibraryManager.Sort(children, user, new[] { ItemSortBy.ProductionYear, ItemSortBy.PremiereDate, ItemSortBy.SortName }, SortOrder.Ascending).ToList();
            }

            // Default sorting
            return LibraryManager.Sort(children, user, new[] { ItemSortBy.ProductionYear, ItemSortBy.PremiereDate, ItemSortBy.SortName }, SortOrder.Ascending).ToList();
        }

        public BoxSetInfo GetLookupInfo()
        {
            return GetItemLookupInfo<BoxSetInfo>();
        }

        public override bool IsVisible(User user)
        {
            if (base.IsVisible(user))
            {
                return base.GetChildren(user, true).Count > 0;
            }

            return false;
        }

        public override bool IsVisibleStandalone(User user)
        {
            return IsVisible(user);
        }
    }
}
