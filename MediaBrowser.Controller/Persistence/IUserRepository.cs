using MediaBrowser.Controller.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Persistence
{
    /// <summary>
    /// Provides an interface to implement a User repository
    /// </summary>
    public interface IUserRepository : IRepository
    {
        /// <summary>
        /// Deletes the user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteUser(User user, CancellationToken cancellationToken);

        /// <summary>
        /// Saves the user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        Task SaveUser(User user, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves all users.
        /// </summary>
        /// <returns>IEnumerable{User}.</returns>
        IEnumerable<User> RetrieveAllUsers();

        Task<User> RetrieveUser(Guid guid, CancellationToken cancellationToken);

        Task UpdateUserConfig(User user, CancellationToken cancellationToken);

        Task UpdateUserPolicy(User user, CancellationToken cancellationToken);

        Task<User> CreateUser(string name,string fqdn, CancellationToken cancellationToken);
    }
}
