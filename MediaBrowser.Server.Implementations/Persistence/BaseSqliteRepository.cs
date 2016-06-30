using MediaBrowser.Common.SQL;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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

        protected async Task AddColumns(IDictionary<string,string> columns, string table) {
            using (var connection = await CreateConnection(true).ConfigureAwait(false)) {

                DataTable schema = null;
                List<string> queries = new List<string>();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM "+table;

                    using (var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly))
                    {
                        schema = reader.GetSchemaTable();

                        foreach (DataRow myField in schema.Rows)
                        {
                            columns.Remove(myField["ColumnName"].ToString());
                        }
                    }
                }
                               
                columns.ToList().ForEach(p => {
                        queries.Add(string.Format("ALTER TABLE {0} ADD COLUMN {1} {2}", table, p.Key, p.Value));
                });
                connection.RunQueries(queries.ToArray(), Logger);
            }
        }

        protected async Task<IEnumerable<T>> Read<T>(Query query, Func<IDataReader,T> func)
        {
            var result = new List<T>();
            using (var connection = CreateConnection(true).Result)
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = query.Text;

                    query.Values.ForEach(v => {
                        cmd.Parameters.Add(cmd, v.Id, v.Type).Value = v.Value;
                    });

                    using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                    {
                        while (reader.Read())
                        {
                            result.Add(func(reader));
                        }
                    }
                }
            }

            return result;
        }

        protected async Task Commit(Query query, string errorMsg = "Failed Commit:")
        {

            using (var connection = await CreateConnection().ConfigureAwait(false))
            {
                IDbTransaction transaction = null;

                try
                {
                    transaction = connection.BeginTransaction();

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = query.Text;

                       query.Values.ForEach(v => {
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

}