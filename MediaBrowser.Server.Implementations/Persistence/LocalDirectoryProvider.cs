using MediaBrowser.Controller;
using MediaBrowser.Common.SQL;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Providers.Authentication;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Security;

namespace MediaBrowser.Server.Implementations.Persistence
{
    class LocalDirectoryProvider : BaseSqliteRepository, IDirectoriesProvider
    {
        private IDbConnection _connection;
        private readonly IServerApplicationPaths _appPaths;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;

        public LocalDirectoryProvider(ILogger logger, ILogManager logManager, IServerApplicationPaths appPaths, IDbConnector dbConnector, IJsonSerializer jsonSerializer) : base(logManager, dbConnector)
        {
            _appPaths = appPaths;
            _logger = logger;
            _jsonSerializer = jsonSerializer;
            DbFilePath = Path.Combine(appPaths.DataPath, "users.db");
            try
            {
                Initialize(dbConnector).Wait();
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
                return "LocalDirectory";
            }
        }

        /// <summary>
        /// Opens the connection to the database
        /// </summary>
        /// <returns>Task.</returns>
        private async Task Initialize(IDbConnector dbConnector)
        {
            using (var connection = await CreateConnection().ConfigureAwait(false))
            {
                string[] queries = {

                                "create table if not exists local_domain (id INTEGER PRIMARY KEY AUTOINCREMENT,"+
                                    "cn VARCHAR(50) NOT NULL UNIQUE, memberOf BLOB, type INTEGER, pwd CHAR(40))",
                                "create index if not exists idx_entry on local_domain(cn)",

                                "pragma shrink_memory"
                               };

                connection.RunQueries(queries, Logger);
            }

            var entries = await RetrieveAll("Local").ConfigureAwait(false);
           
            // There always has to be at least one user.
            if (entries.FirstOrDefault(e => e.Type == EntryType.User) == null)
            {
                var name = MakeValidUsername(Environment.UserName);
                await InsertEntry(new DirectoryEntry() {
                    Name = name,
                    Id = name,
                    FQDN = "Local"
                }, CancellationToken.None);
                
            }
        }

        public async Task InsertEntry(DirectoryEntry entry, CancellationToken cancellationToken)
        {
            if (entry == null)
            {
                throw new ArgumentNullException("Directory Entry");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var serialized = _jsonSerializer.SerializeToBytes(entry.MemberOf);

            cancellationToken.ThrowIfCancellationRequested();
            var commit = new Query() {
                Text = "insert into local_domain (cn, type, memberOf, pwd) values (@cn, @type, @memberOf, @pwd)",
            };
            commit.AddValue("@cn", DbType.String, entry.Name);
            commit.AddValue("@type", DbType.Int16, entry.Type);
            commit.AddValue("@memberOf", DbType.Binary,serialized);
            commit.AddValue("@pwd", DbType.String, Crypto.GetSha1(String.Empty));

            await Commit(commit).ConfigureAwait(false);
        }


        public async Task<IEnumerable<DirectoryEntry>> RetrieveAll(string fqdn)
        {
            var query = new Query() { Text = "select cn, memberOf, pwd, type from local_domain" };
            return await Read(query, CreateDirectoryEntry);
        }

        public async Task DeleteEntry(string cn, string fqdn, CancellationToken cancellationToken)
        {
            ValidEntry(cn, fqdn);

            cancellationToken.ThrowIfCancellationRequested();

            var commit = new Query()
            {
                Text = "delete from local_domain where cn=@cn",
            };
            commit.AddValue("@cn", DbType.String, cn);
            await Commit(commit, "Failed to delete entry").ConfigureAwait(false);

        }


        public IEnumerable<string> GetDomains()
        {
            return new List<string>() { "Local" };
        }

        public async Task<bool> AuthenticateUser(string cn, string fqdn, string password)
        {
            var user = await RetrieveEntry(cn, fqdn, CancellationToken.None).ConfigureAwait(false);
            if (user != null)
            {
                password = String.IsNullOrEmpty(password) ? "DA39A3EE5E6B4B0D3255BFEF95601890AFD80709" : Crypto.GetSha1(password);
                var pass = (user.GetAttribute("password") ?? "DA39A3EE5E6B4B0D3255BFEF95601890AFD80709");
                return string.Equals(pass, password, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public async Task<DirectoryEntry> RetrieveEntry(string cn, string fqdn, CancellationToken cancellationToken)
        {
            ValidEntry(cn, fqdn);
            var query = new Query() { Text = "select cn, memberOf, pwd, type from local_domain where cn=@cn" };
            query.AddValue("@cn", DbType.String, cn);
            var result = await Read(query, CreateDirectoryEntry);
            return result.FirstOrDefault();
        }
        private DirectoryEntry CreateDirectoryEntry(IDataReader reader)
        {
            var entry = new DirectoryEntry()
            {
                Id = reader.GetString(0),
                Name = reader.GetString(0),
                FQDN = "Local",
                Type = (EntryType)reader.GetInt16(3),
                Attributes = new Dictionary<string, string>()
                            {
                               { "password", reader.GetString(2) }
                            }
            };
            using (var stream = reader.GetMemoryStream(1))
            {
                var memberOf = _jsonSerializer.DeserializeFromStream<List<string>>(stream);
                entry.MemberOf = memberOf;

            }
            return entry;
        }
        private void ValidEntry(string cn, string fqdn)
        {
            if (fqdn != "Local")
            {
                throw new ArgumentNullException("Dont manage the domain");
            }
            if (string.IsNullOrWhiteSpace(cn))
            {
                throw new ArgumentNullException("invalid entry common name");
            }
        }

        public async Task UpdateEntry(DirectoryEntry entry, CancellationToken cancellationToken, string newCn = null)
        {
            if (entry == null)
            {
                throw new ArgumentNullException("domain entry");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var serialized = _jsonSerializer.SerializeToBytes(entry.MemberOf);

            cancellationToken.ThrowIfCancellationRequested();

            var commit = new Query()
            {
                Text = "Update local_domain SET cn = @newCn, type = @type, memberOf = @memberOf where cn=@cn",
            };
            commit.AddValue("@cn", DbType.String, entry.Name);
            commit.AddValue("@type", DbType.Int16, entry.Type);
            commit.AddValue("@memberOf", DbType.Binary, serialized);
            commit.AddValue("@newCn", DbType.String, newCn ?? entry.Name);

            await Commit(commit, "Failed to Update User").ConfigureAwait(false);
    
        }

        public async Task UpdateUserPassword(string cn, string fqdn, string password)
        {
            ValidEntry(cn, fqdn);
            var commit = new Query()
            {
                Text = "update local_domain SET pwd=@pwd where cn=@cn",
            };
            commit.AddValue("@cn", DbType.String, cn);
            commit.AddValue("@pwd", DbType.String, Crypto.GetSha1(String.Empty));

            await Commit(commit, "failed to update password").ConfigureAwait(false);
   
        }

        public static bool IsValidUsername(string username)
        {
            // Usernames can contain letters (a-z), numbers (0-9), dashes (-), underscores (_), apostrophes ('), and periods (.)
            return username.All(IsValidUsernameCharacter);
        }

        private static  bool IsValidUsernameCharacter(char i)
        {
            return char.IsLetterOrDigit(i) || char.Equals(i, '-') || char.Equals(i, '_') || char.Equals(i, '\'') ||
                   char.Equals(i, '.');
        }

        public static string MakeValidUsername(string username)
        {
            if (IsValidUsername(username))
            {
                return username;
            }

            // Usernames can contain letters (a-z), numbers (0-9), dashes (-), underscores (_), apostrophes ('), and periods (.)
            var builder = new StringBuilder();

            foreach (var c in username)
            {
                if (IsValidUsernameCharacter(c))
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

    }
}
