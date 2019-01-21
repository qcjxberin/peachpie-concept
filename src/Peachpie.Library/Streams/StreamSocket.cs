﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Pchp.Core;
using Pchp.Core.Utilities;

namespace Pchp.Library.Streams
{
    /// <summary>
	/// Gives access to various network-based stream properties.
	/// </summary>
	/// <threadsafety static="true"/>
	public static class StreamSocket
    {
        #region Enums

        /// <summary>
        /// Options used for <see cref="StreamSocket.Connect"/>.
        /// </summary>
        [PhpHidden]
        public enum SocketOptions
        {
            /// <summary>
            /// Default option.
            /// </summary>
            None = 0,

            /// <summary>
            /// Client socket opened with <c>stream_socket_client</c> should remain persistent
            /// between page loads.
            /// </summary>
            Persistent = 1,

            /// <summary>
            /// Open client socket asynchronously.
            /// </summary>
            Asynchronous = 2
        }

        public const int STREAM_CLIENT_CONNECT = (int)SocketOptions.None;
        public const int STREAM_CLIENT_PERSISTENT = (int)SocketOptions.Persistent;
        public const int STREAM_CLIENT_ASYNC_CONNECT = (int)SocketOptions.Asynchronous;

        [PhpHidden]
        public enum _AddressFamily
        {
            InterNetwork = AddressFamily.InterNetwork,
            InterNetworkV6 = AddressFamily.InterNetworkV6,
            Unix = AddressFamily.Unix
        }

        public const int STREAM_PF_INET = (int)_AddressFamily.InterNetwork;
        public const int STREAM_PF_INET6 = (int)_AddressFamily.InterNetworkV6;
        public const int STREAM_PF_UNIX = (int)_AddressFamily.Unix;

        [PhpHidden]
        public enum _SocketType
        {
            Unknown = SocketType.Unknown,
            Stream = SocketType.Stream,
            Dgram = SocketType.Dgram,
            Raw = SocketType.Raw,
            Rdm = SocketType.Rdm,
            Seqpacket = SocketType.Seqpacket,
        }

        public const int STREAM_SOCK_STREAM = (int)_SocketType.Stream;
        public const int STREAM_SOCK_DGRAM = (int)_SocketType.Dgram;
        public const int STREAM_SOCK_RAW = (int)_SocketType.Raw;
        public const int STREAM_SOCK_RDM = (int)_SocketType.Rdm;
        public const int STREAM_SOCK_SEQPACKET = (int)_SocketType.Seqpacket;

        [PhpHidden]
        public enum _ProtocolType
        {
            IP = ProtocolType.IP,
            Icmp = ProtocolType.Icmp,
            Tcp = ProtocolType.Tcp,
            Udp = ProtocolType.Udp,
            Raw = ProtocolType.Raw
        }

        public const int STREAM_IPPROTO_IP = (int)_ProtocolType.IP;
        public const int STREAM_IPPROTO_ICMP = (int)_ProtocolType.Icmp;
        public const int STREAM_IPPROTO_TCP = (int)_ProtocolType.Tcp;
        public const int STREAM_IPPROTO_UDP = (int)_ProtocolType.Udp;
        public const int STREAM_IPPROTO_RAW = (int)_ProtocolType.Raw;

        [PhpHidden]
        public enum SendReceiveOptions
        {
            None = 0,
            OutOfBand = 1,
            Peek = 2
        }

        public const int STREAM_OOB = (int)SendReceiveOptions.OutOfBand;
        public const int STREAM_PEEK = (int)SendReceiveOptions.Peek;

        #endregion

        #region TODO: stream_get_transports, stream_socket_get_name

        /// <summary>Retrieve list of registered socket transports</summary>
        public static PhpArray stream_get_transports()
        {
            PhpException.FunctionNotSupported("stream_get_transports");
            return null;
        }

        /// <summary>
        /// Retrieve the name of the local or remote sockets.
        /// </summary>
        public static string stream_socket_get_name(PhpResource handle, bool wantPeer)
        {
            PhpException.FunctionNotSupported("stream_socket_get_name");
            return null;
        }

        #endregion

        #region TODO: stream_socket_client

        //private static void SplitSocketAddressPort(ref string socket, out int port)
        //{
        //	port = 0;
        //	String[] arr = socket.Split(new[] {':'}, 2, StringSplitOptions.RemoveEmptyEntries);
        //	if (arr.Length == 2)
        //	{
        //		socket = arr[0];
        //		port = int.Parse(arr[1]);
        //	}
        //}
        /// <summary>
        /// Open client socket.
        /// </summary>
        public static PhpResource stream_socket_client(Context ctx, string remoteSocket)
        {
            int errno;
            string errstr;
            int port = 0;
            //SplitSocketAddressPort(ref remoteSocket, out port);
            return Connect(ctx, remoteSocket, port, out errno, out errstr, Double.NaN, SocketOptions.None, StreamContext.Default);
        }

        /// <summary>
        /// Open client socket.
        /// </summary>
        public static PhpResource stream_socket_client(Context ctx, string remoteSocket, out int errno)
        {
            string errstr;
            int port = 0;
            //SplitSocketAddressPort(ref remoteSocket, out port);
            return Connect(ctx, remoteSocket, port, out errno, out errstr, Double.NaN, SocketOptions.None, StreamContext.Default);
        }

        /// <summary>
        /// Open client socket.
        /// </summary>
        public static PhpResource stream_socket_client(Context ctx, string remoteSocket, out int errno, out string errstr, double timeout = double.NaN, SocketOptions flags = SocketOptions.None)
        {
            int port = 0;
            //SplitSocketAddressPort(ref remoteSocket, out port);
            return Connect(ctx, remoteSocket, port, out errno, out errstr, timeout, flags, StreamContext.Default);
        }

        /// <summary>
        /// Open client socket.
        /// </summary>
        public static PhpResource stream_socket_client(Context ctx, string remoteSocket, out int errno, out string errstr, double timeout, SocketOptions flags, PhpResource context)
        {
            StreamContext sc = StreamContext.GetValid(context);
            if (sc == null)
            {
                errno = -1;
                errstr = null;
                return null;
            }

            int port = 0;
            //SplitSocketAddressPort(ref remoteSocket, out port);
            return Connect(ctx, remoteSocket, port, out errno, out errstr, timeout, flags, sc);
        }

        #endregion

        #region TODO: stream_socket_server

        /// <summary>
        /// Open client socket.
        /// </summary>
        public static PhpResource stream_socket_server(Context ctx, string localSocket)
        {
            int errno;
            string errstr;
            int port = 0;
            //SplitSocketAddressPort(ref localSocket, out port);
            return Connect(ctx, localSocket, port, out errno, out errstr, Double.NaN, SocketOptions.None, StreamContext.Default);
        }

        /// <summary>
        /// Open client socket.
        /// </summary>
        public static PhpResource stream_socket_server(Context ctx, string localSocket, out int errno)
        {
            string errstr;
            int port = 0;
            //SplitSocketAddressPort(ref localSocket, out port);
            return Connect(ctx, localSocket, port, out errno, out errstr, Double.NaN, SocketOptions.None, StreamContext.Default);
        }

        /// <summary>
        /// Open client socket.
        /// </summary>
        public static PhpResource stream_socket_server(Context ctx, string localSocket, out int errno, out string errstr, double timeout = double.NaN, SocketOptions flags = SocketOptions.None)
        {
            int port = 0;
            //SplitSocketAddressPort(ref localSocket, out port);
            return Connect(ctx, localSocket, port, out errno, out errstr, timeout, flags, StreamContext.Default);
        }

        /// <summary>
        /// Open client socket.
        /// </summary>
        public static PhpResource stream_socket_server(Context ctx, string localSocket, out int errno, out string errstr, double timeout, SocketOptions flags, PhpResource context)
        {
            StreamContext sc = StreamContext.GetValid(context);
            if (sc == null)
            {
                errno = -1;
                errstr = null;
                return null;
            }

            int port = 0;
            //SplitSocketAddressPort(ref localSocket, out port);
            return Connect(ctx, localSocket, port, out errno, out errstr, timeout, flags, sc);
        }

        #endregion

        #region TODO: stream_socket_accept

        /// <summary>
        /// Accepts a connection on a server socket.
        /// </summary>
        public static bool stream_socket_accept(Context ctx, PhpResource serverSocket)
        {
            string peerName;
            return stream_socket_accept(serverSocket, ctx.Configuration.Core.DefaultSocketTimeout, out peerName);
        }

        /// <summary>
        /// Accepts a connection on a server socket.
        /// </summary>
        public static bool stream_socket_accept(PhpResource serverSocket, int timeout)
        {
            string peerName;
            return stream_socket_accept(serverSocket, timeout, out peerName);
        }

        /// <summary>
        /// Accepts a connection on a server socket.
        /// </summary>
        public static bool stream_socket_accept(PhpResource serverSocket, int timeout, out string peerName)
        {
            peerName = "";

            SocketStream stream = SocketStream.GetValid(serverSocket);
            if (stream == null) return false;

            PhpException.FunctionNotSupported("stream_socket_accept");
            return false;
        }

        #endregion

        #region TODO: stream_socket_recvfrom

        public static string stream_socket_recvfrom(PhpResource socket, int length, SendReceiveOptions flags = SendReceiveOptions.None)
        {
            string address;
            return stream_socket_recvfrom(socket, length, flags, out address);
        }

        public static string stream_socket_recvfrom(PhpResource socket, int length, SendReceiveOptions flags,
          out string address)
        {
            address = null;

            SocketStream stream = SocketStream.GetValid(socket);
            if (stream == null) return null;

            PhpException.FunctionNotSupported("stream_socket_recvfrom");
            return null;
        }

        #endregion

        #region TODO: stream_socket_sendto

        public static int stream_socket_sendto(PhpResource socket, string data, SendReceiveOptions flags = SendReceiveOptions.None, string address = null)
        {
            SocketStream stream = SocketStream.GetValid(socket);
            if (stream == null) return -1;

            PhpException.FunctionNotSupported("stream_socket_sendto");
            return -1;
        }

        #endregion

        #region TODO: stream_socket_pair

        //public static PhpArray stream_socket_pair(ProtocolFamily protocolFamily, SocketType type, ProtocolType protocol)
        //{
        //    PhpException.FunctionNotSupported();
        //    return null;
        //}

        #endregion

        #region Connect

        /// <summary>
        /// Opens a new SocketStream
        /// </summary>
        internal static SocketStream Connect(Context ctx, string remoteSocket, int port, out int errno, out string errstr, double timeout, SocketOptions flags, StreamContext/*!*/ context)
        {
            errno = 0;
            errstr = null;

            if (remoteSocket == null)
            {
                PhpException.ArgumentNull("remoteSocket");
                return null;
            }

            // TODO: extract schema (tcp://, udp://) and port from remoteSocket
            // Uri uri = Uri.TryCreate(remoteSocket);
            ProtocolType protocol = ProtocolType.Tcp;
            if (remoteSocket.Contains("://"))
            {
                String[] separator = { "://" };
                String[] socketParts = remoteSocket.Split(separator, 2, StringSplitOptions.None);
                switch (socketParts[0])
                {
                    case "udp":
                        protocol = ProtocolType.Udp;
                        break;
                    case "tcp":
                    default:
                        protocol = ProtocolType.Tcp;
                        break;
                }
                remoteSocket = socketParts[1];
            }

            if (remoteSocket.Contains(":"))
            {
                Char[] separator = { ':' };
                String[] socketParts = remoteSocket.Split(separator, 2, StringSplitOptions.None);
                remoteSocket = socketParts[0];

                int result = 0;
                if (socketParts[1] != "" && int.TryParse(socketParts[1], out result) &&
                    (0 < result && result < 65536))
                {
                    port = result;
                }
            }

            if (double.IsNaN(timeout))
            {
                timeout = ctx.Configuration.Core.DefaultSocketTimeout;
            }

            // TODO:
            if (flags != SocketOptions.None && flags != SocketOptions.Asynchronous)
            {
                PhpException.ArgumentValueNotSupported("flags", (int)flags);
            }

            try
            {
                // workitem 299181; for remoteSocket as IPv4 address it results in IPv6 address
                //IPAddress address = System.Net.Dns.GetHostEntry(remoteSocket).AddressList[0];

                IPAddress address;
                if (!IPAddress.TryParse(remoteSocket, out address)) // if remoteSocket is not a valid IP address then lookup the DNS
                {
                    var addresses = System.Net.Dns.GetHostAddressesAsync(remoteSocket).Result;
                    if (addresses != null && addresses.Length != 0)
                    {
                        address = addresses[0];
                    }
                    else
                    {
                        throw new ArgumentException(nameof(remoteSocket));
                    }
                }

                var socket = new Socket(address.AddressFamily, SocketType.Stream, protocol);
                if (!socket.ConnectAsync(address, port).Wait((int)(timeout * 1000)))
                {
                    Debug.Assert(!socket.Connected);
                    PhpException.Throw(PhpError.Warning, string.Format(Resources.LibResources.socket_open_timeout, FileSystemUtils.StripPassword(remoteSocket)));
                    return null;
                }

                // socket.Connect(new IPEndPoint(address, port));
                return new SocketStream(ctx, socket, remoteSocket, context, (flags & SocketOptions.Asynchronous) == SocketOptions.Asynchronous);
            }
            catch (SocketException e)
            {
                errno = (int)e.SocketErrorCode;
                errstr = e.Message;
            }
            catch (System.Exception e)
            {
                errno = -1;
                errstr = e.Message;
            }

            PhpException.Throw(PhpError.Warning, string.Format(Resources.LibResources.socket_open_error, FileSystemUtils.StripPassword(remoteSocket), errstr));
            return null;
        }

        #endregion
    }
}
