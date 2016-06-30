using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Model.Entities
{
    public enum EntryType { User, Group, Asset, Other };
    public class DirectoryEntry
    {
        public string UID { get; set; }
        public string CN { get; set; }
        public EntryType Type { get; set; }
        public string FQDN { get; set; }
        public string DN { get { return FQDN + "/" + UID; } }
        public IDictionary<string, string> Attributes { get; set; }
        public IEnumerable<string> MemberOf { get; set; }

        public DirectoryEntry(string cn, string fqdn, EntryType type = EntryType.User, string uid = null)
        {
            UID = uid ?? cn;
            CN = cn;
            Type = type;
            MemberOf = new List<string>();
            Attributes = new Dictionary<string, string>();
        }

        public DirectoryEntry()
        {
            MemberOf = new List<string>();
            Attributes = new Dictionary<string, string>();
        }

        public string GetAttribute(string name, string def = null)
        {
            var val = def ?? String.Empty;
            Attributes.TryGetValue(name, out val);
            return val;
        }
        
    }
}
