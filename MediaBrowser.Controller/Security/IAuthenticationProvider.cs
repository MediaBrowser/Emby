using MediaBrowser.Controller.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Security
{
    public interface IAuthenticationProvider
    {
        Task<bool> Authenticate(AuthenticationRequest authRequest);
    }
}
