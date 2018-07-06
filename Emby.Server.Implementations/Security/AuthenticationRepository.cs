using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Emby.Server.Implementations.Data;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Security;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using SQLitePCL.pretty;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Controller.Configuration;

namespace Emby.Server.Implementations.Security
{
    public class AuthenticationRepository : BaseSqliteRepository, IAuthenticationRepository
    {
        private readonly IServerConfigurationManager _config;
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");

        public AuthenticationRepository(ILogger logger, IServerConfigurationManager config)
            : base(logger)
        {
            _config = config;
            DbFilePath = Path.Combine(config.ApplicationPaths.DataPath, "authentication.db");
        }

        public void Initialize()
        {
            using (var connection = CreateConnection())
            {
                RunDefaultInitialization(connection);

                string[] queries = {

                               "create table if not exists Tokens (Id INTEGER PRIMARY KEY, AccessToken TEXT NOT NULL, DeviceId TEXT NOT NULL, AppName TEXT NOT NULL, AppVersion TEXT NOT NULL, DeviceName TEXT NOT NULL, UserId TEXT, UserName TEXT, IsActive BIT NOT NULL, DateCreated DATETIME NOT NULL, DateLastActivity DATETIME NOT NULL)",
                                "drop index if exists idx_AccessTokens",
                                "drop index if exists Tokens1",
                                "drop index if exists Tokens2",
                                "create index if not exists Tokens3 on Tokens (AccessToken, DateLastActivity)",
                                "create index if not exists Tokens4 on Tokens (DeviceId, DateLastActivity)"
                               };

                connection.RunQueries(queries);

                TryMigrate(connection);
            }
        }

        private void TryMigrate(ManagedConnection connection)
        {
            try
            {
                if (TableExists(connection, "AccessTokens"))
                {
                    connection.RunInTransaction(db =>
                    {
                        var existingColumnNames = GetColumnNames(db, "AccessTokens");

                        AddColumn(db, "AccessTokens", "UserName", "TEXT", existingColumnNames);
                        AddColumn(db, "AccessTokens", "DateLastActivity", "DATETIME", existingColumnNames);
                        AddColumn(db, "AccessTokens", "AppVersion", "TEXT", existingColumnNames);

                    }, TransactionMode);

                    connection.RunQueries(new[]
                    {
                        "update accesstokens set DateLastActivity=DateCreated where DateLastActivity is null",
                        "INSERT INTO Tokens (AccessToken, DeviceId, AppName, AppVersion, DeviceName, UserId, UserName, IsActive, DateCreated, DateLastActivity) SELECT AccessToken, DeviceId, AppName, AppVersion, DeviceName, UserId, UserName, IsActive, DateCreated, DateLastActivity FROM AccessTokens where deviceid not null and devicename not null and appname not null and isactive=1",
                        "drop table if exists AccessTokens"
                    });
                }
                else if (!_config.Configuration.AuthDbUpgraded)
                {
                    connection.RunQueries(new[]
                    {
                        "delete from tokens where isactive=0"
                    });
                }

                if (!_config.Configuration.AuthDbUpgraded)
                {
                    _config.Configuration.AuthDbUpgraded = true;
                    _config.SaveConfiguration();
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error migrating authentication database", ex);
            }
        }

        public void Create(AuthenticationInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(db =>
                    {
                        using (var statement = db.PrepareStatement("insert into Tokens (AccessToken, DeviceId, AppName, AppVersion, DeviceName, UserId, UserName, IsActive, DateCreated, DateLastActivity) values (@AccessToken, @DeviceId, @AppName, @AppVersion, @DeviceName, @UserId, @UserName, @IsActive, @DateCreated, @DateLastActivity)"))
                        {
                            statement.TryBind("@AccessToken", info.AccessToken);

                            statement.TryBind("@DeviceId", info.DeviceId);
                            statement.TryBind("@AppName", info.AppName);
                            statement.TryBind("@AppVersion", info.AppVersion);
                            statement.TryBind("@DeviceName", info.DeviceName);
                            statement.TryBind("@UserId", (info.UserId.Equals(Guid.Empty) ? null : info.UserId.ToString("N")));
                            statement.TryBind("@UserName", info.UserName);
                            statement.TryBind("@IsActive", true);
                            statement.TryBind("@DateCreated", info.DateCreated.ToDateTimeParamValue());
                            statement.TryBind("@DateLastActivity", info.DateLastActivity.ToDateTimeParamValue());

                            statement.MoveNext();
                        }

                    }, TransactionMode);
                }
            }
        }

        public void Update(AuthenticationInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException("entry");
            }

            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(db =>
                    {
                        using (var statement = db.PrepareStatement("Update Tokens set AccessToken=@AccessToken, DeviceId=@DeviceId, AppName=@AppName, AppVersion=@AppVersion, DeviceName=@DeviceName, UserId=@UserId, UserName=@UserName, DateCreated=@DateCreated, DateLastActivity=@DateLastActivity where Id=@Id"))
                        {
                            statement.TryBind("@Id", info.Id);

                            statement.TryBind("@AccessToken", info.AccessToken);

                            statement.TryBind("@DeviceId", info.DeviceId);
                            statement.TryBind("@AppName", info.AppName);
                            statement.TryBind("@AppVersion", info.AppVersion);
                            statement.TryBind("@DeviceName", info.DeviceName);
                            statement.TryBind("@UserId", (info.UserId.Equals(Guid.Empty) ? null : info.UserId.ToString("N")));
                            statement.TryBind("@UserName", info.UserName);
                            statement.TryBind("@DateCreated", info.DateCreated.ToDateTimeParamValue());
                            statement.TryBind("@DateLastActivity", info.DateLastActivity.ToDateTimeParamValue());

                            statement.MoveNext();
                        }
                    }, TransactionMode);
                }
            }
        }

        public void Delete(AuthenticationInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException("entry");
            }

            using (WriteLock.Write())
            {
                using (var connection = CreateConnection())
                {
                    connection.RunInTransaction(db =>
                    {
                        using (var statement = db.PrepareStatement("Delete from Tokens where Id=@Id"))
                        {
                            statement.TryBind("@Id", info.Id);

                            statement.MoveNext();
                        }
                    }, TransactionMode);
                }
            }
        }

        private const string BaseSelectText = "select Id, AccessToken, DeviceId, AppName, AppVersion, DeviceName, UserId, UserName, DateCreated, DateLastActivity from Tokens";

        private void BindAuthenticationQueryParams(AuthenticationInfoQuery query, IStatement statement)
        {
            if (!string.IsNullOrEmpty(query.AccessToken))
            {
                statement.TryBind("@AccessToken", query.AccessToken);
            }

            if (!query.UserId.Equals(Guid.Empty))
            {
                statement.TryBind("@UserId", query.UserId.ToString("N"));
            }

            if (!string.IsNullOrEmpty(query.DeviceId))
            {
                statement.TryBind("@DeviceId", query.DeviceId);
            }
        }

        public QueryResult<AuthenticationInfo> Get(AuthenticationInfoQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var commandText = BaseSelectText;

            var whereClauses = new List<string>();

            if (!string.IsNullOrEmpty(query.AccessToken))
            {
                whereClauses.Add("AccessToken=@AccessToken");
            }

            if (!string.IsNullOrEmpty(query.DeviceId))
            {
                whereClauses.Add("DeviceId=@DeviceId");
            }

            if (!query.UserId.Equals(Guid.Empty))
            {
                whereClauses.Add("UserId=@UserId");
            }

            if (query.HasUser.HasValue)
            {
                if (query.HasUser.Value)
                {
                    whereClauses.Add("UserId not null");
                }
                else
                {
                    whereClauses.Add("UserId is null");
                }
            }

            var whereTextWithoutPaging = whereClauses.Count == 0 ?
              string.Empty :
              " where " + string.Join(" AND ", whereClauses.ToArray(whereClauses.Count));

            commandText += whereTextWithoutPaging;

            commandText += " ORDER BY DateLastActivity desc";

            if (query.Limit.HasValue || query.StartIndex.HasValue)
            {
                var offset = query.StartIndex ?? 0;

                if (query.Limit.HasValue || offset > 0)
                {
                    commandText += " LIMIT " + (query.Limit ?? int.MaxValue).ToString(CultureInfo.InvariantCulture);
                }

                if (offset > 0)
                {
                    commandText += " OFFSET " + offset.ToString(CultureInfo.InvariantCulture);
                }
            }

            var list = new List<AuthenticationInfo>();

            using (WriteLock.Read())
            {
                using (var connection = CreateConnection(true))
                {
                    return connection.RunInTransaction(db =>
                    {
                        var result = new QueryResult<AuthenticationInfo>();

                        var statementTexts = new List<string>();
                        statementTexts.Add(commandText);
                        statementTexts.Add("select count (Id) from Tokens" + whereTextWithoutPaging);

                        var statements = PrepareAllSafe(db, statementTexts)
                            .ToList();

                        using (var statement = statements[0])
                        {
                            BindAuthenticationQueryParams(query, statement);

                            foreach (var row in statement.ExecuteQuery())
                            {
                                list.Add(Get(row));
                            }

                            using (var totalCountStatement = statements[1])
                            {
                                BindAuthenticationQueryParams(query, totalCountStatement);

                                result.TotalRecordCount = totalCountStatement.ExecuteQuery()
                                    .SelectScalarInt()
                                    .First();
                            }
                        }

                        result.Items = list.ToArray(list.Count);
                        return result;

                    }, ReadTransactionMode);
                }
            }
        }

        public AuthenticationInfo Get(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            using (WriteLock.Read())
            {
                using (var connection = CreateConnection(true))
                {
                    var commandText = BaseSelectText + " where Id=@Id";

                    using (var statement = connection.PrepareStatement(commandText))
                    {
                        statement.BindParameters["@Id"].Bind(id.ToGuidBlob());

                        foreach (var row in statement.ExecuteQuery())
                        {
                            return Get(row);
                        }
                        return null;
                    }
                }
            }
        }

        private AuthenticationInfo Get(IReadOnlyList<IResultSetValue> reader)
        {
            var info = new AuthenticationInfo
            {
                Id = reader[0].ToInt64(),
                AccessToken = reader[1].ToString()
            };

            if (reader[2].SQLiteType != SQLiteType.Null)
            {
                info.DeviceId = reader[2].ToString();
            }

            if (reader[3].SQLiteType != SQLiteType.Null)
            {
                info.AppName = reader[3].ToString();
            }

            if (reader[4].SQLiteType != SQLiteType.Null)
            {
                info.AppVersion = reader[4].ToString();
            }

            if (reader[5].SQLiteType != SQLiteType.Null)
            {
                info.DeviceName = reader[5].ToString();
            }

            if (reader[6].SQLiteType != SQLiteType.Null)
            {
                info.UserId = new Guid(reader[6].ToString());
            }

            if (reader[7].SQLiteType != SQLiteType.Null)
            {
                info.UserName = reader[7].ToString();
            }

            info.DateCreated = reader[8].ReadDateTime();

            if (reader[9].SQLiteType != SQLiteType.Null)
            {
                info.DateLastActivity = reader[9].ReadDateTime();
            }
            else
            {
                info.DateLastActivity = info.DateCreated;
            }

            return info;
        }
    }
}
