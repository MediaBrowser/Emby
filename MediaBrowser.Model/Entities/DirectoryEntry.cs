﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Model.Entities
{
    public enum EntryType { User, Group, Asset, Other };
    public class DirectoryEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string LoginName { get; set; }
        public EntryType Type { get; set; }
        public string FQDN { get; set; }
        public string AccountName { get { return FQDN + "/" + LoginName; } }
        public IDictionary<string, string> Attributes { get; set; }
        public IEnumerable<string> MemberOf { get; set; }

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
        public void SetAttribute(string name, string value)
        {
            Attributes[name] = value;
        }
    }
}
