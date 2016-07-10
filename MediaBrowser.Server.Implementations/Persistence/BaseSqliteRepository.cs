using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.Persistence
{
    public abstract class BaseSqliteRepository : IDisposable
    {
        protected SemaphoreSlim WriteLock = new SemaphoreSlim(1, 1);
        protected readonly IDbConnector DbConnector;
        protected ILogger Logger;

        protected string DbFilePath { get; set; }

        protected BaseSqliteRepository(ILogManager logManager, IDbConnector dbConnector)
        {
            DbConnector = dbConnector;
            Logger = logManager.GetLogger(GetType().Name);
        }

        protected virtual bool EnableConnectionPooling
        {
            get { return true; }
        }

        protected virtual async Task<IDbConnection> CreateConnection(bool isReadOnly = false)
        {
            var connection = await DbConnector.Connect(DbFilePath, false, true).ConfigureAwait(false);

            connection.RunQueries(new[]
            {
                "pragma temp_store = memory"

            }, Logger);

            return connection;
        }

        private bool _disposed;
        protected void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name + " has been disposed and cannot be accessed.");
            }
        }

        public void Dispose()
        {
            _disposed = true;
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected async Task Vacuum(IDbConnection connection)
        {
            CheckDisposed();

            await WriteLock.WaitAsync().ConfigureAwait(false);

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "vacuum";
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                Logger.ErrorException("Failed to vacuum:", e);

                throw;
            }
            finally
            {
                WriteLock.Release();
            }
        }

        private readonly object _disposeLock = new object();

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                try
                {
                    lock (_disposeLock)
                    {
                        WriteLock.Wait();

                        CloseConnection();
                    }
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error disposing database", ex);
                }
            }
        }

        protected virtual void CloseConnection()
        {

        }

        /// <summary>
        /// Save a user in the repo
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">user</exception>
        public async Task Commit(string command, IEnumerable<Params> parameters = null)
        {
            if (String.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException("sql command");
            }

            parameters = parameters ?? new List<Params>();

            using (var connection = await CreateConnection().ConfigureAwait(false))
            {
                IDbTransaction transaction = null;

                try
                {
                    transaction = connection.BeginTransaction();

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = command;
                        parameters.ToList().ForEach(p => cmd.Parameters.Add(cmd, p.Name, p.Type).Value = p.Value);
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
                    Logger.ErrorException("Failed to save user:", e);

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
        public IEnumerable<T> Reader<T>(string command, Func<IDataReader, T> createFromEntry, IEnumerable<Params> parameters = null)
        {
            var list = new List<T>();
            if (String.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException("sql command");
            }

            parameters = parameters ?? new List<Params>();
            using (var connection = CreateConnection(true).Result)
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = command;
                    parameters.ToList().ForEach(p => cmd.Parameters.Add(cmd, p.Name, p.Type).Value = p.Value);
                    using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                    {
                        while (reader.Read())
                        {
                            list.Add(createFromEntry(reader));
                        }
                    }
                }
            }
            return list;
        }
    }

    public class Params
    {
        public string Name { get; set; }
        public DbType Type { get; set; }
        public Object Value { get; set; }

        public Params(string name, DbType type, Object value)
        {
            Name = name;
            type = Type;
            Value = value;
        }
    }
}