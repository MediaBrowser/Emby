using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;

namespace MediaBrowser.Controller.Authentication
{
    public interface IAuthenticationProvider
    {
        string Name { get; }
        Task Authenticate(string username, string password, User resolvedUser);
        Task<bool> HasPassword(User user);
        Task ChangePassword(User user, string newPassword);
    }
}
