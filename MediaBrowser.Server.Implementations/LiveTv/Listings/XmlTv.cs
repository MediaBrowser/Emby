using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace MediaBrowser.Server.Implementations.LiveTv.Listings
{
    public class XmlTv : BaseListingsProvider, IListingsProvider
    {
        private readonly IXmlSerializer XmlSerializer;

        public XmlTv(ILogger logger, IJsonSerializer jsonSerializer, IXmlSerializer xmlSerializer)
            : base(logger, jsonSerializer)
        {
            XmlSerializer = xmlSerializer;
        }
        public string Name
        {
            get { return "XmlTV"; }
        }

        public string Type
        {
            get { return "xmltv"; }
        }

        public async Task Validate(ListingsProviderInfo info, bool validateLogin, bool validateListings)
        {
            // Check that the path or url is valid. If not, throw a file not found exception
        }

        public Task<List<NameIdPair>> GetLineups(ListingsProviderInfo info, string country, string location)
        {
            // In theory this should never be called because there is always only one lineup
            throw new NotImplementedException();
        }

        protected override Task<IEnumerable<Station>> GetStations(ListingsProviderInfo info, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override Task<IEnumerable<ProgramInfo>> GetProgramsAsyncInternal(ListingsProviderInfo info, string station, IEnumerable<string> dates, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}