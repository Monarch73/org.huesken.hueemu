using System;
using org.huesken.hueemu.net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.ServiceProcess;
using System.Linq;
using System.Net.NetworkInformation;
using System.Management;

namespace org.huesken.hueemu.Console
{
	class MainClass
	{
        public static string GetDefaultGatewayIp()
        {
            string result = null;
            //create a management scope object
            ManagementScope scope = new ManagementScope("\\\\.\\ROOT\\cimv2");
            //create object query
            ObjectQuery query = new ObjectQuery("SELECT * FROM Win32_IP4RouteTable");
            //create object searcher
            ManagementObjectSearcher searcher =
                                    new ManagementObjectSearcher(scope, query);

            //get a collection of WMI objects
            ManagementObjectCollection queryCollection = searcher.Get();

            //enumerate the collection.
            foreach (ManagementObject m in queryCollection)
            {
                // access properties of the WMI object
                if (m["Destination"].ToString().Trim() == "0.0.0.0")
                {
                    result = m["NextHop"].ToString().Trim();
                    break;
                }

            }

            return result;
        }

        public static void GetIpOfInternetInterface(string defaultGateway, out string ipAdress, out PhysicalAddress mac)
        {
            string myIp = null;
            PhysicalAddress myMac = null;

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                string myIP = null;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    System.Console.WriteLine(ni.Name);
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            myIP = ip.Address.ToString();
                            break; // foreach UnicastAddresses
                        }
                    }

                    IPInterfaceProperties adapterProperties = ni.GetIPProperties();
                    GatewayIPAddressInformationCollection addresses = adapterProperties.GatewayAddresses;
                    if (addresses.Count > 0)
                    {
                        foreach (GatewayIPAddressInformation address in addresses)
                        {
                            System.Console.WriteLine("  Gateway Address ......................... : {0}",
                                address.Address.ToString());

                            if (address.Address.ToString() == defaultGateway)
                            {
                                myIp = myIP;
                                myMac = ni.GetPhysicalAddress();
                                break;
                            }
                        }
                        System.Console.WriteLine();
                    }
                }
            }

            ipAdress = myIp;
            mac = myMac;
        }

		public static void Main(string[] args)
		{
            string defaultGwIp = GetDefaultGatewayIp();
            GetIpOfInternetInterface(defaultGwIp, out string myIP, out PhysicalAddress mac);

            Debug.WriteLine(string.Format("Starting HueEmulation for IP {0}:{1}", myIP, mac.ToString()));
            Debug.WriteLine(mac.ToString().ToUpper().Substring(0, 6) + "FFFE" + mac.ToString().ToUpper().Substring(6, 6));
            ServiceController[] services = ServiceController.GetServices();
            foreach (ServiceController service in services)
            {
                System.Console.WriteLine(service.ServiceName + "==" + service.Status);
            }

        

            if (services.Where(x => x.ServiceName == "SSDPSRV").First().Status == ServiceControllerStatus.Running)
            {
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine("Error: SSDP Service Running");
                System.Console.ForegroundColor = ConsoleColor.Yellow;
                Environment.Exit(0);
            }

            SSDP.Start(myIP, mac);

		}
	}
}
