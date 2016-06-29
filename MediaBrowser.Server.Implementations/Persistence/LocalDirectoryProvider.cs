using MediaBrowser.Controller;
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
            var commit = new CommitEntry() {
                Text = "insert into local_domain (cn, type, memberOf, pwd) values (@cn, @type, @memberOf, @pwd)",
            };
            commit.AddValue("@cn", DbType.String, entry.Name);
            commit.AddValue("@type", DbType.Int16, entry.Type);
            commit.AddValue("@memberOf", DbType.Binary,serialized);
            commit.AddValue("@pwd", DbType.String, GetSha1String(String.Empty));

            await Commit(commit).ConfigureAwait(false);
        }


        public Task<IEnumerable<DirectoryEntry>> RetrieveAll(string fqdn)
        {
            List<DirectoryEntry> local_domain = new List<DirectoryEntry>();

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "select cn, memberOf, pwd, type from local_domain";

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        local_domain.Add(CreateDirectoryEntry(reader));
                    }
                }
            }
            return Task.FromResult((IEnumerable<DirectoryEntry>)local_domain);
        }

        public async Task DeleteEntry(string cn, string fqdn, CancellationToken cancellationToken)
        {
            if (fqdn != "Local")
            {
                throw new ArgumentNullException("Dont have access to directory");
            }
            if (string.IsNullOrWhiteSpace(cn))
            {
                throw new ArgumentNullException("invalid entry common name");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var commit = new CommitEntry()
            {
                Text = "delete from local_domain where cn=@cn",
            };
            commit.AddValue("@cn", DbType.String, cn);
            await Commit(commit, "Failed to delete entry").ConfigureAwait(false);

        }


        public IEnumerable<string> GetDirectories()
        {
            return new List<string>() { "Local" };
        }

        public async Task<bool> AuthenticateUser(string cn, string fqdn, string password)
        {
            var user = await RetrieveEntry(cn, fqdn, CancellationToken.None).ConfigureAwait(false);
            if (user != null)
            {
                password = String.IsNullOrEmpty(password) ? "DA39A3EE5E6B4B0D3255BFEF95601890AFD80709" : GetSha1String(password);
                var pass = (user.GetAttribute("password") ?? "DA39A3EE5E6B4B0D3255BFEF95601890AFD80709");
                return string.Equals(pass, password, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public Task<DirectoryEntry> RetrieveEntry(string cn, string fqdn, CancellationToken cancellationToken)
        {
            DirectoryEntry entry = null;
            ValidEntry(cn, fqdn);
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "select cn, memberOf, pwd, type from local_domain where cn=@cn";
                cmd.Parameters.Add(cmd, "@cn", DbType.String).Value = cn;
                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        entry = CreateDirectoryEntry(reader);
                    }
                }
            }
            return Task.FromResult(entry);
        }
        private DirectoryEntry CreateDirectoryEntry(IDataReader reader)
        {
            var entry = new DirectoryEntry()
            {
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

        public async Task UpdateEntry(DirectoryEntry entry, CancellationToken cancellationToken, string cn = null)
        {
            if (entry == null)
            {
                throw new ArgumentNullException("domain entry");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var serialized = _jsonSerializer.SerializeToBytes(entry.MemberOf);

            cancellationToken.ThrowIfCancellationRequested();

            var commit = new CommitEntry()
            {
                Text = "Update local_domain SET cn = @newCn, type = @type, memberOf = @memberOf where cn=@cn",
            };
            commit.AddValue("@cn", DbType.String, cn ?? entry.Name);
            commit.AddValue("@type", DbType.Int16, entry.Type);
            commit.AddValue("@memberOf", DbType.Binary, serialized);
            commit.AddValue("@newCn", DbType.String, entry.Name);

            await Commit(commit, "Failed to Update User").ConfigureAwait(false);
    
        }

        public async Task UpdateUserPassword(string cn, string fqdn, string password, CancellationToken cancellationToken)
        {
            ValidEntry(cn, fqdn);
            var commit = new CommitEntry()
            {
                Text = "update local_domain SET pwd=@pwd where cn=@cn",
            };
            commit.AddValue("@cn", DbType.String, cn);
            commit.AddValue("@pwd", DbType.String, GetSha1String(String.Empty));

            await Commit(commit, "failed to update password").ConfigureAwait(false);
   
        }

        private static string GetSha1String(string str)
        {
            using (var provider = SHA1.Create())
            {
                var hash = provider.ComputeHash(Encoding.UTF8.GetBytes(str));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private async Task Commit(CommitEntry commit, string errorMsg = "Failed Commit:")
        {

            using (var connection = await CreateConnection().ConfigureAwait(false))
            {
                IDbTransaction transaction = null;

                try
                {
                    transaction = connection.BeginTransaction();

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = commit.Text;

                        commit.Values.ForEach(v => {
                            cmd.Parameters.Add(cmd, v.Id, v.Type).Value = v.Value;
                        });

                        cmd.Transaction = transaction;

                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch (OperationCanceledException)
                {
                    if (transaction != null)
                    {
                        transaction.Rollback();
                    }

                    throw;
                }
                catch (Exception e)
                {
                    Logger.ErrorException(errorMsg, e);

                    if (transaction != null)
                    {
                        transaction.Rollback();
                    }

                    throw;
                }
                finally
                {
                    if (transaction != null)
                    {
                        transaction.Dispose();
                    }
                }
            }
        }
    }
    public class CommitEntry
    {
        public string Text { get; set; }
        public List<CommitValue> Values{get; set;}

        public CommitEntry()
        {
            Values = new List<CommitValue>();
        }

        public void AddValue(string id, DbType type, Object value) {
            Values.Add(new CommitValue() { Id = id, Type = type, Value = value });
        }

    }
    public class CommitValue
    {
        public string Id { get; set; }
        public DbType Type { get; set; }
        public Object Value { get; set; }
    }
}
