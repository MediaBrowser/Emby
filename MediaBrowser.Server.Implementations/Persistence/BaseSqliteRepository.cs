using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.Persistence
{
    public abstract class BaseSqliteRepository : IDisposable
    {
        protected SemaphoreSlim WriteLock = new SemaphoreSlim(1, 1);
        protected readonly IDbConnector DbConnector;
        protected ILogger Logger;
        protected IDbConnection Connection;

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

        public async Task Commit(string command, CancellationToken cancellationToken, IEnumerable<Params> parameters = null, string errorMsg = "Commit Failed: ")
        {
            var conn = Connection ?? await CreateConnection().ConfigureAwait(false);
            await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                conn.Commit(command, parameters, errorMsg);
            }
            catch (Exception e)
            {
                Logger.ErrorException(errorMsg, e);
                throw;
            }
            finally
            {
                if (Connection == null) conn.Dispose();
                WriteLock.Release();
            }
        }

        public IEnumerable<T> Reader<T>(string command, Func<IDataReader, T> createFromEntry, IEnumerable<Params> parameters = null)
        {
            var conn = Connection ?? CreateConnection().Result;
            var result = conn.Read(command, createFromEntry, parameters);
            if (Connection == null) conn.Dispose();
            return result;
        }
    }

    public class Params
    {
        public string Name { get; set; }
        public DbType Type { get; set; }
        public object Value { get; set; }

        public Params(string name, DbType type, object value)
        {
            Name = name;
            Type = type;
            Value = value;
        }

        public void AddToCommand(IDbCommand cmd)
        {
            var param = cmd.CreateParameter();

            param.ParameterName = Name;
            param.DbType = Type;
            param.Value = Value;
            cmd.Parameters.Add(param);
        }
    }
}