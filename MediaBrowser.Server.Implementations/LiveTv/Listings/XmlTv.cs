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
        public XmlTv(ILogger logger, IJsonSerializer jsonSerializer)
            : base(logger, jsonSerializer)
        {

        }
        public string Name
        {
            get { return "XmlTV"; }
        }

        public string Type
        {
            get { return "xmltv"; }
        }

        public Task<List<NameIdPair>> GetLineups(ListingsProviderInfo info, string country, string location)
        {
            throw new NotImplementedException();
        }

        public Task Validate(ListingsProviderInfo info, bool validateLogin, bool validateListings)
        {
            throw new NotImplementedException();
        }

        protected override Task<IEnumerable<ProgramInfo>> GetProgramsAsyncInternal(ListingsProviderInfo info, string station, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override Task<IEnumerable<Station>> GetStations(ListingsProviderInfo info, string lineup, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
