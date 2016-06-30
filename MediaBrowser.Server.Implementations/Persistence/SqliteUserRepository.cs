using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Common.SQL;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace MediaBrowser.Server.Implementations.Persistence
{
    /// <summary>
    /// Class SQLiteUserRepository
    /// </summary>
    public class SqliteUserRepository : BaseSqliteRepository, IUserRepository
    {
        private readonly IJsonSerializer _jsonSerializer;
        private ILogger _logger;

        public SqliteUserRepository(ILogManager logManager, IServerApplicationPaths appPaths, IJsonSerializer jsonSerializer, IDbConnector dbConnector) : base(logManager, dbConnector)
        {
            _jsonSerializer = jsonSerializer;
            DbFilePath = Path.Combine(appPaths.DataPath, "users.db");
        }

        /// <summary>
        /// Gets the name of the repository
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get
            {
                return "SQLite";
            }
        }

        /// <summary>
        /// Opens the connection to the database
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Initialize()
        {
            using (var connection = await CreateConnection().ConfigureAwait(false))
            {
                string[] queries = {

                                "create table if not exists users (guid GUID primary key, config BLOB, policy BLOB, data BLOB)",
                                "create index if not exists idx_users on users(guid)",
                                "create table if not exists schema_version (table_name primary key, version)",

                                "pragma shrink_memory"
                               };

                connection.RunQueries(queries, Logger);
            }
            var cols = new Dictionary<string, string>() {
                { "config","BLOB"},{ "policy","BLOB"},{ "data","BLOB"}, {"guid","GUID" }
            };
            await AddColumns(cols,"users").ConfigureAwait(false);
        }
        public async Task<User> CreateUser(string name, string fqdn, CancellationToken cancellationToken)
        {
            var user = InstantiateNewUser(name);
            user.FQDN = fqdn;
            await SaveUser(user, cancellationToken).ConfigureAwait(false);
            return user;
        }
        /// <summary>
        /// Save a user in the repo
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">user</exception>
        public async Task SaveUser(User user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var data = _jsonSerializer.SerializeToBytes(user);
            var config = _jsonSerializer.SerializeToBytes(user.Configuration ?? new UserConfiguration());
            var policy = _jsonSerializer.SerializeToBytes(user.Policy ?? new UserPolicy());

            cancellationToken.ThrowIfCancellationRequested();

            var commit = new Query()
            {
                Text = "replace into users (guid, data, config, policy) values (@guid, @data, @config, @policy)",
            };

            commit.AddValue("@guid", user.Id);
            commit.AddValue("@data", data);
            commit.AddValue("@config", config);
            commit.AddValue("@policy", policy);

            user.DateLastSaved = DateTime.UtcNow;
            await Commit(commit, "Failed to Update User").ConfigureAwait(false);
 
        }

        private User GetUser(IDataReader reader)
        {
            var id = reader.GetGuid(0);
            var user = new User();
            using (var stream = reader.GetMemoryStream(1))
            {
                user = _jsonSerializer.DeserializeFromStream<User>(stream);
                user.Id = id;
                user.FQDN = "Local";
            }
            using (var stream = reader.GetMemoryStream(2)) { user.Policy = _jsonSerializer.DeserializeFromStream<UserPolicy>(stream); }
            using (var stream = reader.GetMemoryStream(3)) { user.Configuration = _jsonSerializer.DeserializeFromStream<UserConfiguration>(stream); }
            return user;
        }

        /// <summary>
        /// Retrieve all users from the database
        /// </summary>
        /// <returns>IEnumerable{User}.</returns>
        public IEnumerable<User> RetrieveAllUsers()
        {
            var query = new Query() { Text = "select guid,data,policy,config from users" };
            return Read(query, GetUser).Result;
         }

        /// <summary>
        /// Deletes the user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">user</exception>
        public async Task DeleteUser(User user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var commit = new Query()
            {
                Text = "delete from users where guid=@guid",
            };
            commit.AddValue("@guid", user.Id);

            await Commit(commit, "Failed to Delete User").ConfigureAwait(false);
        }

        public async Task UpdateUserPolicy(User user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var serialized = _jsonSerializer.SerializeToBytes(user.Policy);

            cancellationToken.ThrowIfCancellationRequested();

            var commit = new Query()
            {
                Text = "Update users SET policy = @policy where guid=@guid"
            };
            commit.AddValue("@guid", user.Id);
            commit.AddValue("@policy", serialized);

            await Commit(commit, "Failed to Update User Policy").ConfigureAwait(false);

        }

        public async Task UpdateUserConfig(User user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var serialized = _jsonSerializer.SerializeToBytes(user.Configuration);

            cancellationToken.ThrowIfCancellationRequested();

            var commit = new Query()
            {
                Text = "Update users SET config = @config where guid=@guid",
            };
            commit.AddValue("@guid", user.Id);
            commit.AddValue("@config", serialized);

            await Commit(commit, "Failed to Update User Configuration").ConfigureAwait(false);

        }

        public async Task<User> RetrieveUser(Guid guid, CancellationToken cancellationToken)
        {
            var query = new Query() { Text = "select guid,data,policy,config from users where guid=@guid" };
            query.AddValue("@guid", guid);
            var result = await Read(query, GetUser);
            return result.FirstOrDefault();
        }

        /// <summary>
        /// Instantiates the new user.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>User.</returns>
        private User InstantiateNewUser(string name)
        {
            return new User
            {
                Name = name,
                Id = Guid.NewGuid(),
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                UsesIdForConfigurationPath = true
            };
        }
    }
}