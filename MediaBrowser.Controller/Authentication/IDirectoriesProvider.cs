using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.Authentication
{
    public interface IDirectoriesProvider
    {
        Task<bool> Authenticate(string uid, string fqdn, string password, CancellationToken cancellationToken = default(CancellationToken));

        Task UpdatePassword(string uid, string fqdn, string password, CancellationToken cancellationToken = default(CancellationToken));

        Task<DirectoryEntry> RetrieveEntry(string uid, string fqdn, CancellationToken cancellationToken = default(CancellationToken));

        Task DeleteEntry(string uid, string fqdn, CancellationToken cancellationToken = default(CancellationToken));

        Task<DirectoryEntry> CreateEntry(string cn, string fqdn, IEnumerable<string> memberOf = null, IDictionary<string, string> attributes = null,
            EntryType type = EntryType.User, CancellationToken cancellationToken = default(CancellationToken));

        Task UpdateEntry(string uid, DirectoryEntry entry, CancellationToken cancellationToken = default(CancellationToken));

        Task<IEnumerable<DirectoryEntry>> RetrieveAll(string fqdn);

        IEnumerable<string> GetDomains();
    }
}
