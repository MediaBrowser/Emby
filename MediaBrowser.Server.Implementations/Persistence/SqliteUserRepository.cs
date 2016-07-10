using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Entities;
using MediaBrowser.Common.Security;
using System.Linq;

namespace MediaBrowser.Server.Implementations.Persistence
{
    /// <summary>
    /// Class SQLiteUserRepository
    /// </summary>
    public class SqliteUserRepository : BaseSqliteRepository, IUserRepository
    {
        private static bool init = false;
        private readonly IJsonSerializer _jsonSerializer;

        public SqliteUserRepository(ILogManager logManager, IServerApplicationPaths appPaths, IJsonSerializer jsonSerializer, IDbConnector dbConnector) : base(logManager, dbConnector)
        {
            _jsonSerializer = jsonSerializer;
            DbFilePath = Path.Combine(appPaths.DataPath, "users.db");

            try
            {
                Initialize().Wait();
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error opening user db", ex);
                throw;
            }
            
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
            init = true;
            using (var connection = await CreateConnection().ConfigureAwait(false))
            {
                string[] queries = {

                                "create table if not exists users (guid GUID primary key, data BLOB, password CHAR(40), login_name varchar(255))",
                                "create index if not exists idx_users on users(guid)",
                                "create table if not exists schema_version (table_name primary key, version)",

                                "pragma shrink_memory"
                               };

                connection.RunQueries(queries, Logger);
            }
        }

        public List<Params> GetUserParams(User user)
        {
            return new List<Params>() {
                new Params("@guid",DbType.Guid,user.Id),
                new Params("@data",DbType.Binary,user),
                new Params("@login_name",DbType.String,user.Name)
            };
        }

        /// <summary>
        /// Save a user in the repo
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">user</exception>
        public async Task CreateUser(User user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var serialized = _jsonSerializer.SerializeToBytes(user);

            cancellationToken.ThrowIfCancellationRequested();

            await Commit("insert into users (guid, data, login_name, password) values (@guid, @data, @login_name)", GetUserParams(user));
   
        }

        public async Task UpdateUser(User user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var serialized = _jsonSerializer.SerializeToBytes(user);

            cancellationToken.ThrowIfCancellationRequested();

            await Commit("update users set data=@data, login_name=@login_name where guid=@guid", GetUserParams(user));

        }

        private User GetUser(IDataReader reader)
        {
            var id = reader.GetGuid(0);
            User user = null;
            using (var stream = reader.GetMemoryStream(1))
            {
                user = _jsonSerializer.DeserializeFromStream<User>(stream);
                user.Id = id;
            }
            return user;
        }

        /// <summary>
        /// Retrieve all users from the database
        /// </summary>
        /// <returns>IEnumerable{User}.</returns>
        public IEnumerable<User> RetrieveAllUsers()
        {
            return Reader("select guid,data,password from users", GetUser);
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

            await Commit("delete from users where guid=@guid", GetUserParams(user));
        }

        public async Task UpdateUserPassword(Guid id, string password, CancellationToken cancellationToken)
        {
            if (id == null)
            {
                throw new ArgumentNullException("user");
            }
            List<Params> parameters = new List<Params>() {
                new Params("@guid",DbType.Guid,id),
                new Params("@password", DbType.String,Crypto.GetSha1(password))
            };
            await Commit("update users set password=@password where guid=@guid", parameters);
        }

        public async Task<bool> AuthenticateUser(string login_name, string fqdn, string password)
        {
            password = password ?? String.Empty; 

            using (var connection = await CreateConnection(true))
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "select password from users where login_name=@login_name";
                    cmd.Parameters.Add(cmd, "@login_name", DbType.String).Value = login_name;

                    using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                    {
                        while (reader.Read())
                        {
                            var p = reader.GetString(0);
                            return String.Equals(Crypto.GetSha1(password), p, StringComparison.InvariantCultureIgnoreCase);
                        }
                    }
                }
            }
            return false;
        }

        public DirectoryEntry GetEntry(IDataReader reader)
        {
            var name = reader.GetString(1);
            var e = new DirectoryEntry()
            {
                Id = reader.GetGuid(0).ToString(),
                Name = name,
                FQDN = "Local",
                Type = EntryType.User,
                LoginName = name,
            };
            e.SetAttribute("pwd", reader.GetString(2));
            return e;
        }

        public Task<DirectoryEntry> RetrieveEntry(string uid, string fqdn, CancellationToken cancellationToken)
        {
            return Task.FromResult(Reader("select guid,login_name,password from users where guid=@guid",
                GetEntry,
                new List<Params>() {
                    new Params("@guid",DbType.Guid,Guid.Parse(uid))
            })
            .FirstOrDefault());
        }

        public async Task<IEnumerable<DirectoryEntry>> RetrieveAll(string fqdn)
        {
            return Reader("select guid,login_name,password from users",GetEntry);
        }

        public IEnumerable<string> GetDirectories()
        {
            return new string[] { "Local" };
        }
    }
}