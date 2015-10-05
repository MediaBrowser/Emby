using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Controller.TV
{
    public class SeriesCreationOptions : IHasProviderIds
    {
        public string Name { get; set; }

        public string Location { get; set; }

        public bool IsLocked { get; set; }

        public Dictionary<string, string> ProviderIds { get; set; }

        public List<Guid> ItemIdList { get; set; }
        public List<Guid> UserIds { get; set; }

        public SeriesCreationOptions()
        {
            ProviderIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ItemIdList = new List<Guid>();
            UserIds = new List<Guid>();
        }
    }
}
