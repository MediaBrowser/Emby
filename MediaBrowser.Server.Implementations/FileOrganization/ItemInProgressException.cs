using MediaBrowser.Controller.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.FileOrganization
{
    class ItemInProgressException : Exception
    {
        string _message;

        public ItemInProgressException(ILocalizationManager localizationManager)
        {
            _message = localizationManager.GetLocalizedString("ItemInProgress");
        }

        public override string Message
        {
            get
            {
                return _message;
            }
        }
    }
}
