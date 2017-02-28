
namespace MediaBrowser.Model.Net
{
    /// <summary>
    /// Implemented by components that can create a platform specific UDP socket implementation, and wrap it in the cross platform <see cref="IUdpSocket"/> interface.
    /// </summary>
    public interface ISocketFactory
	{

		/// <summary>
		/// Createa a new unicast socket using the specified local port number.
		/// </summary>
		/// <param name="localPort">The local port to bind to.</param>
		/// <returns>A <see cref="IUdpSocket"/> implementation.</returns>
		IUdpSocket CreateUdpSocket(int localPort);

        // <summary>
        /// Creates a new TCP socket and connects it to the specified remote address and port.
        /// </summary>
        /// <param name="remoteAddress">The IP Address to connect the socket to.</param>
        /// <param name="remotePort">An integer specifying the remote port to connect the socket to.</param>
        /// <returns>A <see cref="IUdpSocket"/> implementation.</returns>
        IUdpSocket CreateTcpSocket(IpAddressInfo remoteAddress, int remotePort);

        /// <summary>
        /// Createa a new unicast socket using the specified local port number.
        /// </summary>
        IUdpSocket CreateSsdpUdpSocket(IpAddressInfo localIp, int localPort);

        /// <summary>
        /// Createa a new multicast socket using the specified multicast IP address, multicast time to live and local port.
        /// </summary>
        /// <param name="ipAddress">The multicast IP address to bind to.</param>
        /// <param name="multicastTimeToLive">The multicast time to live value. Actually a maximum number of network hops for UDP packets.</param>
        /// <param name="localPort">The local port to bind to.</param>
        /// <returns>A <see cref="IUdpSocket"/> implementation.</returns>
        IUdpSocket CreateUdpMulticastSocket(string ipAddress, int multicastTimeToLive, int localPort);

        ISocket CreateSocket(IpAddressFamily family, SocketType socketType, ProtocolType protocolType, bool dualMode);
    }

    public enum SocketType
    {
        Stream
    }

    public enum ProtocolType
    {
        Tcp
    }
}
