using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace org.huesken.fauxmonet.wemodevice
{
    public class WeMoDevice
    {
        private int port;
        private string usn;
        private string nls;
        private Socket socket;

        public WeMoDevice(int port, string USN, string nls)
        {
            this.port = port;
            this.usn = USN;
            this.nls = nls;
            
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.socket.Bind(new IPEndPoint(IPAddress.Any, this.port));
            this.socket.Listen(10);
            this.socket.BeginAccept(AcceptCallBack, this);
        }

        private static void AcceptCallBack(IAsyncResult ar)
        {
            WeMoDevice wemo = (WeMoDevice)ar.AsyncState;
            byte[] buffer;
            int bytesRead;
            var socket = wemo.socket.EndAccept(out buffer, out bytesRead, ar);
            if (bytesRead>0)
            {
                var request = System.Text.Encoding.UTF8.GetString(buffer);
                Console.WriteLine(request);
                socket.Close();
            }
            else
            {
                Console.WriteLine("No initinal data");
            }
        }

        public void GetUdpResponse(out byte[] buffer)
        {
            string format = "HTTP/1.1 200 OK\r\n"+
"CACHE-CONTROL: max-age=86400\r\n"+
"DATE: Wed, 19 Mar 2014 10:24:58 GMT\r\n"+
"EXT:\r\n"+
"LOCATION: http://{0}:{1}/setup.xml\r\n"+
"OPT: \"http://schemas.upnp.org/upnp/1/0/\"; ns=01\r\n"+
"01-NLS: {2}\r\n"+
"SERVER: Unspecified, UPnP/1.0, Unspecified\r\n"+
"X-User-Agent: redsonic\r\n"+
"ST: {3}\r\n"+
"USN: uuid:{4}::{5}\r\n\r\n";

            string.Format(format, this.ip, this.port, this.uuid, this.)
        }
    }
}
