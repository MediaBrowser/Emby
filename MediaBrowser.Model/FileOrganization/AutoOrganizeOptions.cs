
namespace MediaBrowser.Model.FileOrganization
{
    public class AutoOrganizeOptions
    {
        /// <summary>
        /// Gets or sets the tv options.
        /// </summary>
        /// <value>The tv options.</value>
        public TvFileOrganizationOptions TvOptions { get; set; }

        /// <summary>
        /// Gets or sets the smart match options.
        /// </summary>
        /// <value>The smart match options.</value>
        public SmartMatchOptions SmartMatchOptions { get; set; }

        public AutoOrganizeOptions()
        {
            TvOptions = new TvFileOrganizationOptions();
            SmartMatchOptions = new SmartMatchOptions();
        }
    }
}
