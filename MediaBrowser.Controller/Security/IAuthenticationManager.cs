using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Security
{
    public interface IAuthenticationManager
    {
        event EventHandler<GenericEventArgs<AuthenticationRequest>> AuthenticationFailed;

        event EventHandler<GenericEventArgs<AuthenticationRequest>> AuthenticationSucceeded;

        Task<AuthenticationResult> Authenticate(AuthenticationRequest authRequest);
    }
}
