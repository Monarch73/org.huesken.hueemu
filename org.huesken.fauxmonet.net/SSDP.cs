using System;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net.NetworkInformation;

namespace org.huesken.hueemu.net
{
	public static class SSDP
	{
        public class EntityDataGram
        {
            public string Message { get; set; }
            public EndPoint Sender { get; set; }
        }

		private static byte[] receiveBuffer = new byte[64000];
		private static Queue<EntityDataGram> textBuffer = new Queue<EntityDataGram>();
		private static Action<EntityDataGram,Socket> callBack;
        private static Thread threadNotify;
        private static Regex regexMatchMX = new Regex(@"MX: (\d)", RegexOptions.Multiline);
        private static Random rnd = new Random(DateTime.Now.Millisecond);
        private static string IpAdressToPublish;
        private static string hueId;
        private static string uuid;
        public static void Start(string ipAdress, PhysicalAddress mac)
        {
            hueId = mac.ToString().ToUpper().Substring(0, 6)+ "FFFE"+ mac.ToString().ToUpper().Substring(6, 6);
            uuid = "2f402f80-da50-11e1-9b23-" + mac.ToString().ToLower();
            // Creates a temporary EndPoint to pass to EndReceiveFrom.
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint tempRemoteEP = (EndPoint)sender;

            IpAdressToPublish = ipAdress;
            IPEndPoint LocalEndPoint = new IPEndPoint(IPAddress.Any, 1900);
            IPEndPoint MulticastEndPoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);

            Socket UdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            UdpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            UdpSocket.Bind(LocalEndPoint);
            UdpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(MulticastEndPoint.Address, IPAddress.Any));
            UdpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
            UdpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);

            //UdpSocket.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, OnReceive, UdpSocket);
            UdpSocket.BeginReceiveFrom(receiveBuffer, 0, receiveBuffer.Length,SocketFlags.None,ref tempRemoteEP, OnReceive, UdpSocket);


            threadNotify = new Thread(new ThreadStart(() => NotifyLoop()));
            threadNotify.Start();
            SetOnReceive(OnUdpReceive);
        }

        public static void NotifyLoop()
        {
            // SSDP_ADDR = '239.255.255.250'
            // SSDP_PORT = 1900
            // MSEARCH_Interval = 2
            // multicast_group_s = (SSDP_ADDR, SSDP_PORT)
            while (true)
            {
                // message = 'NOTIFY * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nCACHE-CONTROL: max-age=100\r\nLOCATION: http://' + getIpAddress() + ':80/description.xml\r\nSERVER: Linux/3.14.0 UPnP/1.0 IpBridge/1.20.0\r\nNTS: ssdp:alive\r\nhue-bridgeid: ' + (mac[:6] + 'FFFE' + mac[6:]).upper() + '\r\n'
                // custom_message = { 0: { "nt": "upnp:rootdevice", "usn": "uuid:2f402f80-da50-11e1-9b23-" + mac + "::upnp:rootdevice"}, 1: { "nt": "uuid:2f402f80-da50-11e1-9b23-" + mac, "usn": "uuid:2f402f80-da50-11e1-9b23-" + mac}, 2: { "nt": "urn:schemas-upnp-org:device:basic:1", "usn": "uuid:2f402f80-da50-11e1-9b23-" + mac} }
                // sent = sock.sendto(message + "NT: " + custom_message[x]["nt"] + "\r\nUSN: " + custom_message[x]["usn"] + "\r\n\r\n", multicast_group_s)
                // sent = sock.sendto(message + "NT: " + custom_message[x]["nt"] + "\r\nUSN: " + custom_message[x]["usn"] + "\r\n\r\n", multicast_group_s)

                Thread.Sleep(1000 * 60);
            }
        }

		private static void OnReceive(IAsyncResult result)
		{
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint tempRemoteEP = (EndPoint)sender;

            Socket udpSocket = (Socket)result.AsyncState;
			int read = udpSocket.EndReceiveFrom(result, ref tempRemoteEP);
			if (read > 0)
			{
                
				textBuffer.Enqueue(new EntityDataGram() { Message = Encoding.UTF8.GetString(receiveBuffer, 0, read), Sender = tempRemoteEP });
				udpSocket.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, OnReceive, udpSocket);

			}
			else
			{
				udpSocket.Close();
			}

			if (callBack != null)
			{
				callBack(textBuffer.Dequeue(), udpSocket);
			}

		}

		public static void SetOnReceive(Action<EntityDataGram,Socket> callbackOnReceive)
		{
			callBack = callbackOnReceive;
		}

        public static void OnUdpReceive(EntityDataGram content, Socket udpSocket)
        {
            byte[] ssdp_response = System.Text.Encoding.Default.GetBytes(
                "HTTP/1.1 200 OK\r\nHOST: 239.255.255.250:1900\r\nEXT:\r\nCACHE-CONTROL: max-age=100\r\nLOCATION: http://" + IpAdressToPublish + ":80/description.xml\r\nSERVER: Linux/3.14.0 UPnP/1.0 IpBridge/1.20.0\r\nhue-bridgeid: " + hueId + "\r\n" +
                "ST: upnp:rootdevice\r\n" +
                "USN: uuid:" + uuid + "::upnp:rootdevice\r\n\r\n"
                );
            //	Response_message = 'HTTP/1.1 200 OK\r\nHOST: 239.255.255.250:1900\r\nEXT:\r\nCACHE-CONTROL: max-age=100\r\nLOCATION: http://' + getIpAddress() + ':80/description.xml\r\nSERVER: Linux/3.14.0 UPnP/1.0 IpBridge/1.20.0\r\nhue-bridgeid: ' + (mac[:6] + 'FFFE' + mac[6:]).upper() + '\r\n'
            //    custom_response_message = {0: {"st": "upnp:rootdevice", "usn": "uuid:2f402f80-da50-11e1-9b23-" + mac + "::upnp:rootdevice"}, 1: {"st": "uuid:2f402f80-da50-11e1-9b23-" + mac, "usn": "uuid:2f402f80-da50-11e1-9b23-" + mac}, 2: {"st": "urn:schemas-upnp-org:device:basic:1", "usn": "uuid:2f402f80-da50-11e1-9b23-" + mac}}
            //    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            //    sock.bind(server_address)
            //
            //    group = socket.inet_aton(multicast_group_c)
            //    mreq = struct.pack('4sL', group, socket.INADDR_ANY)
            //    sock.setsockopt(socket.IPPROTO_IP, socket.IP_ADD_MEMBERSHIP, mreq)
            //
            //    print("starting ssdp...")
            //
            //    while run_service:
            //              data, address = sock.recvfrom(1024)
            //              if data[0:19]== 'M-SEARCH * HTTP/1.1':
            //                   if data.find("ssdp:discover") != -1:
            //                       sleep(random.randrange(0, 3))
            //                       print("Sending M-Search response to " + address[0])
            //                       for x in xrange(3):
            //                          sock.sendto(Response_message + "ST: " + custom_response_message[x]["st"] + "\r\nUSN: " + custom_response_message[x]["usn"] + "\r\n\r\n", address)

            if (content.Message.Contains("M-SEARCH"))
            {
                if (content.Message.Contains("ssdp:discover"))
                {
                    Debug.WriteLine(content);
                    var result = regexMatchMX.Matches(content.Message);
                    var mx = result.Cast<Match>().FirstOrDefault()?.Groups?.Cast<Group>()?.ElementAtOrDefault(1)?.Value;
                    int imx = mx != null ? int.Parse(mx) * 1000 : 250;
                    new Thread(new ThreadStart(() => {
                        Thread.Sleep(rnd.Next(imx));
                        udpSocket.Send(ssdp_response);
                    })).Start();
                }
            }
        }
    }
}
