using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.IO
{
    class FileRefreshItem
    {
        private ConcurrentDictionary<string, string> _filePaths = new ConcurrentDictionary<string, string>();
        private string _folder;

        public FileRefreshItem(string folder)
        {
            _folder = folder;
        }

        public DateTime DueDate { get; set; }

        public string Folder { get { return _folder; } }

        public ConcurrentDictionary<string, string> FilePaths { get { return _filePaths; } }
    }
}
