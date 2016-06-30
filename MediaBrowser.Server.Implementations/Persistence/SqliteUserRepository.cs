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
using MediaBrowser.Model.Entities;

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

                                "create table if not exists users (guid GUID primary key, domain_uid VARCHAR(255), fqdn VARCHAR(255), config BLOB, policy BLOB, data BLOB)",
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
        public async Task<User> CreateUser(DirectoryEntry directoryEntry, CancellationToken cancellationToken = default(CancellationToken))
        {
            var user = InstantiateNewUser(directoryEntry.CN);

            var q = new Query()
            {
                Cmd = "insert into users (guid, domain_uid, fqdn, data, config, policy) values (@guid, @uid, @fqdn, @data, @config, @policy)",
            };

            var data = _jsonSerializer.SerializeToBytes(user);
            var config = _jsonSerializer.SerializeToBytes(user.Configuration ?? new UserConfiguration());
            var policy = _jsonSerializer.SerializeToBytes(user.Policy ?? new UserPolicy());

            q.AddValue("@guid", user.Id);
            q.AddValue("@fqdn", directoryEntry.FQDN);
            q.AddValue("@uid", directoryEntry.UID);
            q.AddValue("@data", data);
            q.AddValue("@config", config);
            q.AddValue("@policy", policy);

            await Commit(q).ConfigureAwait(false);

            return await RetrieveUser(user.Id, cancellationToken);
        }
        /// <summary>
        /// Save a user in the repo
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">user</exception>
        public async Task UpdateEntry(User user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            cancellationToken.ThrowIfCancellationRequested();


            user.DateModified = DateTime.UtcNow;
            user.DateLastSaved = DateTime.UtcNow;

            var data = _jsonSerializer.SerializeToBytes(user);
            var config = _jsonSerializer.SerializeToBytes(user.Configuration);
            var policy = _jsonSerializer.SerializeToBytes(user.Policy);

            cancellationToken.ThrowIfCancellationRequested();

            var commit = new Query()
            {
                Cmd = "UPDATE users SET data=@data, config=@config, policy=@policy WHERE guid=@guid",
                ErrorMsg = "Failed to Update User"
            };

            commit.AddValue("@guid", user.Id);
            commit.AddValue("@data", data);
            commit.AddValue("@config", config);
            commit.AddValue("@policy", policy);
                        
            await Commit(commit).ConfigureAwait(false);
 
        }

        private User GetUser(IDataReader reader)
        {
            var user = new User();
            using (var stream = reader.GetMemoryStream(1))
            {
                user = _jsonSerializer.DeserializeFromStream<User>(stream);
                user.Id = reader.GetGuid(0);
                user.DomainUid = reader["domain_uid"] as string;
                user.FQDN = reader["fqdn"] as string;
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
            var query = new Query() { Cmd = "select guid,data,policy,config,domain_uid,fqdn from users" };
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
                Cmd = "delete from users where guid=@guid",
                ErrorMsg = "Failed to Delete User"
            };
            commit.AddValue("@guid", user.Id);

            await Commit(commit).ConfigureAwait(false);
        }

        public async Task UpdateUserPolicy(User user, UserPolicy policy =  null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var serialized = _jsonSerializer.SerializeToBytes(policy ?? user.Policy);

            cancellationToken.ThrowIfCancellationRequested();

            var commit = new Query()
            {
                Cmd = "Update users SET policy=@policy WHERE guid=@guid",
                ErrorMsg = "Failed to Update User Policy"
            };
            commit.AddValue("@guid", user.Id);
            commit.AddValue("@policy", serialized);

            await Commit(commit).ConfigureAwait(false);

        }

        public async Task UpdateUserConfig(User user, UserConfiguration config = null, CancellationToken cancellationToken=default(CancellationToken))
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var serialized = _jsonSerializer.SerializeToBytes(config ?? user.Configuration);

            cancellationToken.ThrowIfCancellationRequested();

            var commit = new Query()
            {
                Cmd = "Update users SET config=@config where guid=@guid",
                ErrorMsg = "Failed to update Configuration"
            };
            commit.AddValue("@guid", user.Id);
            commit.AddValue("@config", serialized);

            await Commit(commit).ConfigureAwait(false);

        }

        public async Task<User> RetrieveUser(Guid guid, CancellationToken cancellationToken)
        {
            var query = new Query() { Cmd = "select guid,data,policy,config,domain_uid,fqdn from users where guid=@guid" };
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
                DateLastSaved = DateTime.UtcNow,
                Policy = new UserPolicy(),
                Configuration = new UserConfiguration()
            };
        }
    }
}