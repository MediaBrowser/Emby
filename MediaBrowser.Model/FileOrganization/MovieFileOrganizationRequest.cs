using System.Collections.Generic;

namespace MediaBrowser.Model.FileOrganization
{
    public class MovieFileOrganizationRequest
    {
        public string ResultId { get; set; }

        public string Name { get; set; }

        public string Year { get; set; }

        public string TargetFolder { get; set; }
    }
}