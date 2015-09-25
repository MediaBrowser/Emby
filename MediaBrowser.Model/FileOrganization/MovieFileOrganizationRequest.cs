namespace MediaBrowser.Model.FileOrganization
{
    public class MovieFileOrganizationRequest : BaseFileOrganizationRequest
    {
        public string Name  { get; set; }

        public string Year { get; set; }

        public string TargetFolder  { get; set; }
    }
}