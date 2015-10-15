using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.IO
{
    class FileRefreshEventArgs : EventArgs
    {
        private FileRefreshItem _item;

        public bool Cancel { get; set; }

        public FileRefreshItem Item { get { return _item; }}

        public FileRefreshEventArgs(FileRefreshItem item)
        {
            _item = item;
        }
    }
}
