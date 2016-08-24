using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.Persistence
{
    public class SqliteUserDataRepository : BaseSqliteRepository, IUserDataRepository
    {

        public SqliteUserDataRepository(ILogManager logManager, IApplicationPaths appPaths, IDbConnector connector) : base(logManager, connector)
        {
            DbFilePath = Path.Combine(appPaths.DataPath, "userdata_v2.db");
        }

        protected override bool EnableConnectionPooling
        {
            get { return false; }
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

        protected override async Task<IDbConnection> CreateConnection(bool isReadOnly = false)
        {
            var connection = await DbConnector.Connect(DbFilePath, false, false, 10000).ConfigureAwait(false);

            connection.RunQueries(new[]
            {
                "pragma temp_store = memory"

            }, Logger);

            return connection;
        }

        /// <summary>
        /// Opens the connection to the database
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Initialize(IDbConnection connection, SemaphoreSlim writeLock)
        {
            WriteLock.Dispose();
            WriteLock = writeLock;
            Connection = connection;

            string[] queries = {

                                "create table if not exists UserDataDb.userdata (key nvarchar, userId GUID, rating float null, played bit, playCount int, isFavorite bit, playbackPositionTicks bigint, lastPlayedDate datetime null)",

                                "drop index if exists UserDataDb.idx_userdata",
                                "drop index if exists UserDataDb.idx_userdata1",
                                "drop index if exists UserDataDb.idx_userdata2",
                                "drop index if exists UserDataDb.userdataindex1",

                                "create unique index if not exists UserDataDb.userdataindex on userdata (key, userId)",
                                "create index if not exists UserDataDb.userdataindex2 on userdata (key, userId, played)",
                                "create index if not exists UserDataDb.userdataindex3 on userdata (key, userId, playbackPositionTicks)",
                                "create index if not exists UserDataDb.userdataindex4 on userdata (key, userId, isFavorite)",

                                //pragmas
                                "pragma temp_store = memory",

                                "pragma shrink_memory"
                               };

            Connection.RunQueries(queries, Logger);

            Connection.AddColumn(Logger, "userdata", "AudioStreamIndex", "int");
            Connection.AddColumn(Logger, "userdata", "SubtitleStreamIndex", "int");
        }

        /// <summary>
        /// Saves the user data.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="key">The key.</param>
        /// <param name="userData">The user data.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">userData
        /// or
        /// cancellationToken
        /// or
        /// userId
        /// or
        /// userDataId</exception>
        public Task SaveUserData(Guid userId, string key, UserItemData userData, CancellationToken cancellationToken)
        {
            if (userData == null)
            {
                throw new ArgumentNullException("userData");
            }
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("key");
            }

            return PersistUserData(userId, key, userData, cancellationToken);
        }

        public Task SaveAllUserData(Guid userId, IEnumerable<UserItemData> userData, CancellationToken cancellationToken)
        {
            if (userData == null)
            {
                throw new ArgumentNullException("userData");
            }
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            return PersistAllUserData(userId, userData, cancellationToken);
        }

        private List<Params> GetParams(Guid userId, string key, UserItemData userData)
        {

            return new List<Params>() {
                new Params("@key", DbType.String, key),
                new Params("@userId",DbType.Guid,userId),
                new Params("@rating",DbType.Double,userData.Rating),
                new Params("@played",DbType.Boolean,userData.Played),
                new Params("@playCount",DbType.Int32,userData.PlayCount),
                new Params("@isFavorite",DbType.Boolean,userData.IsFavorite),
                new Params("@playbackPositionTicks",DbType.Int64,userData.PlaybackPositionTicks),
                new Params("@lastPlayedDate",DbType.DateTime,userData.LastPlayedDate),
                new Params("@AudioStreamIndex",DbType.Int32,userData.AudioStreamIndex),
                new Params("@SubtitleStreamIndex",DbType.Int32,userData.SubtitleStreamIndex)
            };
        }

        /// <summary>
        /// Persists the user data.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="key">The key.</param>
        /// <param name="userData">The user data.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task PersistUserData(Guid userId, string key, UserItemData userData, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Commit("replace into userdata (key, userId, rating,played,playCount,isFavorite,playbackPositionTicks,lastPlayedDate,AudioStreamIndex,SubtitleStreamIndex) values (@key, @userId, @rating,@played,@playCount,@isFavorite,@playbackPositionTicks,@lastPlayedDate,@AudioStreamIndex,@SubtitleStreamIndex)",
                cancellationToken, GetParams(userId, key, userData), "Failed to save user data: ");

            WriteLock.Release();
        }

        /// <summary>
        /// Persist all user data for the specified user
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userData"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task PersistAllUserData(Guid userId, IEnumerable<UserItemData> userData, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var userItemData in userData)
            {
                await PersistUserData(userId, userItemData.Key, userItemData, cancellationToken);
            }

        }

        /// <summary>
        /// Gets the user data.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="key">The key.</param>
        /// <returns>Task{UserItemData}.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// userId
        /// or
        /// key
        /// </exception>
        public UserItemData GetUserData(Guid userId, string key)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("key");
            }

            var paramList = new List<Params>() {
                new Params("@userId",DbType.Guid,userId),
                new Params("@key", DbType.String, key)
            };
            return Reader("select key,userid,rating,played,playCount,isFavorite,playbackPositionTicks,lastPlayedDate,AudioStreamIndex,SubtitleStreamIndex from userdata where key = @key and userId=@userId",
                ReadRow, paramList).FirstOrDefault();
        }

        public UserItemData GetUserData(Guid userId, List<string> keys)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }
            if (keys == null)
            {
                throw new ArgumentNullException("keys");
            }

            var builder = new StringBuilder();
            var index = 0;
            var userdataKeys = new List<string>();
            var paramList = new List<Params>();
            paramList.Add(new Params("@userId", DbType.Guid, userId));
            foreach (var key in keys)
            {
                var paramName = "@Key" + index;
                userdataKeys.Add("Key =" + paramName);
                paramList.Add(new Params(paramName, DbType.String, key));
                builder.Append(" WHEN Key=" + paramName + " THEN " + index);
                index++;
                break;
            }
            var keyText = string.Join(" OR ", userdataKeys.ToArray());
            var commandText = "select key,userid,rating,played,playCount,isFavorite,playbackPositionTicks,lastPlayedDate,AudioStreamIndex,SubtitleStreamIndex from userdata where userId=@userId AND (" + keyText + ") ";
            commandText += " ORDER BY (Case " + builder + " Else " + keys.Count.ToString(CultureInfo.InvariantCulture) + " End )";
            commandText += " LIMIT 1";
            return Reader(commandText, ReadRow, paramList).FirstOrDefault();
        }

        /// <summary>
        /// Return all user-data associated with the given user
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public IEnumerable<UserItemData> GetAllUserData(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentNullException("userId");
            }

            var paramList = new List<Params>() {
                new Params("@userId",DbType.Guid,userId)
            };
            return Reader("select key, userid, rating, played, playCount, isFavorite, playbackPositionTicks, lastPlayedDate, AudioStreamIndex, SubtitleStreamIndex from userdata where userId = @userId",
                ReadRow, paramList);
        }

        /// <summary>
        /// Read a row from the specified reader into the provided userData object
        /// </summary>
        /// <param name="reader"></param>
        private UserItemData ReadRow(IDataReader reader)
        {
            var userData = new UserItemData();

            userData.Key = reader.GetString(0);
            userData.UserId = reader.GetGuid(1);

            if (!reader.IsDBNull(2))
            {
                userData.Rating = reader.GetDouble(2);
            }

            userData.Played = reader.GetBoolean(3);
            userData.PlayCount = reader.GetInt32(4);
            userData.IsFavorite = reader.GetBoolean(5);
            userData.PlaybackPositionTicks = reader.GetInt64(6);

            if (!reader.IsDBNull(7))
            {
                userData.LastPlayedDate = reader.GetDateTime(7).ToUniversalTime();
            }

            if (!reader.IsDBNull(8))
            {
                userData.AudioStreamIndex = reader.GetInt32(8);
            }

            if (!reader.IsDBNull(9))
            {
                userData.SubtitleStreamIndex = reader.GetInt32(9);
            }

            return userData;
        }

        protected override void Dispose(bool dispose)
        {
            // handled by library database
        }

        protected override void CloseConnection()
        {
            // handled by library database
        }
    }
}