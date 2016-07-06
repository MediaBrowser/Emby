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
                await CreateEntry(name, "Local").ConfigureAwait(false);                
            }
        }
        
        public async Task<DirectoryEntry> CreateEntry(string cn, string fqdn, IEnumerable<string> memberOf = null, IDictionary<string, string> attributes = null, 
            EntryType type = EntryType.User, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cn == null || fqdn != "Local")
            {
                throw new ArgumentNullException("Directory Entry");
            }
            memberOf = memberOf ?? new string[0];

            cancellationToken.ThrowIfCancellationRequested();

            var serialized = _jsonSerializer.SerializeToBytes(memberOf);

            cancellationToken.ThrowIfCancellationRequested();

            var q = new Query()
            {
                Cmd = "insert into local_domain (cn, type, memberOf, pwd) values (@cn, @type, @memberOf, @pwd)",
            };
            q.AddValue("@cn", cn);
            q.AddValue("@type", (int)type);
            q.AddValue("@memberOf", serialized);
            q.AddValue("@pwd", Crypto.GetSha1(String.Empty));

            await Commit(q).ConfigureAwait(false);

            return await RetrieveEntry(cn, fqdn, cancellationToken);

        }

        public async Task<IEnumerable<DirectoryEntry>> RetrieveAll(string fqdn)
        {
            var query = new Query() { Cmd = "select cn, memberOf, pwd, type from local_domain" };
            return await Read(query, CreateDirectoryEntry);
        }

        public async Task DeleteEntry(string uid, string fqdn, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidEntry(uid, fqdn);

            cancellationToken.ThrowIfCancellationRequested();

            var q = new Query()
            {
                Cmd = "delete from local_domain where cn=@cn",
                ErrorMsg = "Failled to Delete Entry"
            };
            q.AddValue("@cn",uid);
            await Commit(q).ConfigureAwait(false);
        }


        public IEnumerable<string> GetDomains()
        {
            return new List<string>() { "Local" };
        }

        public async Task<bool> Authenticate(string uid, string fqdn, string password, CancellationToken cancellationToken = default(CancellationToken))
        {
            var user = await RetrieveEntry(uid, fqdn, CancellationToken.None).ConfigureAwait(false);
            if (user != null)
            {
                password = String.IsNullOrEmpty(password) ? "DA39A3EE5E6B4B0D3255BFEF95601890AFD80709" : Crypto.GetSha1(password);
                var pass = (user.GetAttribute("password") ?? "DA39A3EE5E6B4B0D3255BFEF95601890AFD80709");
                return string.Equals(pass, password, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public async Task<DirectoryEntry> RetrieveEntry(string uid, string fqdn, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidEntry(uid, fqdn);
            var query = new Query() { Cmd = "select cn, memberOf, pwd, type from local_domain where cn=@cn" };
            query.AddValue("@cn", uid);
            var result = await Read(query, CreateDirectoryEntry);
            return result.FirstOrDefault();
        }
        private DirectoryEntry CreateDirectoryEntry(IDataReader reader)
        {
            Logger.Info("Type");
            Logger.Info("Construct");
            var entry = new DirectoryEntry()
            {
                UID = reader["cn"] as string,
                CN = reader["cn"] as string,
                FQDN = "Local",
                Type = (EntryType)(reader.GetInt32(3)),
                Attributes = new Dictionary<string, string>()
                {
                    { "password", reader["pwd"] as string }
                }
            };
            using (var stream = reader.GetMemoryStream(1))
            {
                var memberOf = _jsonSerializer.DeserializeFromStream<List<string>>(stream);
                entry.MemberOf = memberOf;
            }
            return entry;
        }
        private void ValidEntry(string uid, string fqdn)
        {
            if (fqdn != "Local")
            {
                throw new ArgumentNullException("Dont manage the domain");
            }
            if (string.IsNullOrWhiteSpace(uid))
            {
                throw new ArgumentNullException("invalid entry uid");
            }
        }

        public async Task UpdateEntry(string uid, DirectoryEntry entry, CancellationToken cancellationToken = default(CancellationToken))
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
                Cmd = "Update local_domain SET cn = @cn, type = @type, memberOf = @memberOf where cn=@uid",
                ErrorMsg = "Failed to Update directory entry"
            };

            commit.AddValue("@uid", uid);
            commit.AddValue("@type", entry.Type);
            commit.AddValue("@memberOf", serialized);
            commit.AddValue("@cn", entry.CN);

            await Commit(commit).ConfigureAwait(false);
    
        }

        public async Task UpdatePassword(string uid, string fqdn, string password, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidEntry(uid, fqdn);
            var commit = new Query()
            {
                Cmd = "update local_domain SET pwd=@pwd where cn=@uid",
                ErrorMsg = "failed to update password"
            };
            commit.AddValue("@uid", uid);
            commit.AddValue("@pwd", Crypto.GetSha1(password));

            await Commit(commit).ConfigureAwait(false);
   
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
