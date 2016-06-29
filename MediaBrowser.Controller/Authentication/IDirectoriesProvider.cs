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
        Task<bool> AuthenticateUser(string cn, string fqdn, string password);

        Task UpdateUserPassword(string cn, string fqdn, string password, CancellationToken cancellationToken);

        Task<DirectoryEntry> RetrieveEntity(string cn, string fqdn, CancellationToken cancellationToken);

        Task DeleteEntity(string cn, string fqdn, CancellationToken cancellationToken);

        Task InsertEntity(DirectoryEntry entity, CancellationToken cancellationToken);

        Task UpdateEntity(DirectoryEntry entity, CancellationToken cancellationToken, string cn = null);

        Task<IEnumerable<DirectoryEntry>> RetrieveAll(string fqdn);

        IEnumerable<string> GetDirectories();
    }
}
