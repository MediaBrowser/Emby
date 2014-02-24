using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.PlayTo.Extensions
{
    public static class SocketExtensions
    {
        public static Task<int> ReceiveAsync(this Socket socket, byte[] buffer, int offset, int size)
        {
            var tcs = new TaskCompletionSource<int>(socket);
            IPEndPoint remoteip = new IPEndPoint(IPAddress.Any, 0);
            EndPoint endpoint = (EndPoint)remoteip;
            socket.BeginReceiveFrom(buffer, offset, size, SocketFlags.None, ref endpoint, iar =>
            {                
                var result = (TaskCompletionSource<int>)iar.AsyncState;
                var iarSocket = (Socket)result.Task.AsyncState;
                
                try
                {
                    result.TrySetResult(iarSocket.EndReceive(iar));
                }
                catch (Exception exc)
                {
                    result.TrySetException(exc);
                }
            }, tcs);             
            return tcs.Task;
        }
    }
}
