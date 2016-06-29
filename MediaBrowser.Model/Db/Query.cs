using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Model.Db
{
    public class Query
    {
        public string Text { get; set; }
        public List<QueryParams> Values { get; set; }

        public Query()
        {
            Values = new List<QueryParams>();
        }

        public void AddValue(string id, DbType type, Object value)
        {
            Values.Add(new QueryParams() { Id = id, Type = type, Value = value });
        }

    }
    public class QueryParams
    {
        public string Id { get; set; }
        public DbType Type { get; set; }
        public Object Value { get; set; }
    }

}
