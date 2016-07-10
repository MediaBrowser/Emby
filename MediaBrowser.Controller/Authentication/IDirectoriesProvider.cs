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
        Task<bool> AuthenticateUser(string loginName, string fqdn, string password);

        Task<DirectoryEntry> RetrieveEntry(string uid, string fqdn, CancellationToken cancellationToken);

        Task<IEnumerable<DirectoryEntry>> RetrieveAll(string fqdn);

        IEnumerable<string> GetDirectories();
    }
}
