using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Collections;
using System.Net.NetworkInformation;

namespace org.huesken.hueemu.net
{
    public static class HTTP
    {
        private static HttpListener listener;
        private static Thread listenerThread;
        private static string ipAdressToPublish;
        private delegate void OnHandler(HttpListenerContext context, MatchCollection matches);
        private static IList<KeyValuePair<Regex, OnHandler>> urlRegex;

        private static string udn;
        private static PhysicalAddress mac;

        public static void Start(string ipAdressToPublish, PhysicalAddress mac)
        {
            HTTP.mac = mac;
            udn = "uuid:2f402f80-da50-11e1-9b23-" + mac.ToString().ToLower();

            urlRegex = new List<KeyValuePair<Regex, OnHandler>>()
            {
                new KeyValuePair<Regex, OnHandler>(new Regex("/description.xml"), (x,y)=> OnDescription(x,y) ),
                new KeyValuePair<Regex, OnHandler>(new Regex("/api/nouser/config"), (x,y)=> OnApiNouserConfig(x,y)),
                //                                           "/api/s1tqdEw8T50c5e5CPzm9FKh3VAeBcbMOiM5CbWFQ/lights"
                new KeyValuePair<Regex, OnHandler>(new Regex("/api/[a-z0-9A-Z]{3,}/lights"), (x,y) => OnApiLights(x,y))
            };

            HTTP.ipAdressToPublish = ipAdressToPublish;
            if (!HttpListener.IsSupported)
            {
                throw new NotSupportedException("HttpListener not suppored. Windows XP SP2 required.");
            }

            listenerThread = new Thread(new ThreadStart(ListenerThread));
            listenerThread.Start();
        }

        private static void OnApiLights(HttpListenerContext context, MatchCollection matches)
        {
            var json = org.huesken.fauxmonet.net.Properties.Resources.lights;
            var bytes = System.Text.Encoding.Default.GetBytes(json);

            var response = context.Response;
            response.ContentType = "text/json";
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        private static void OnApiNouserConfig(HttpListenerContext x, MatchCollection y)
        {
        }

        public static void OnDescription(HttpListenerContext context, MatchCollection matches)
        {
            var xmlResponse = org.huesken.fauxmonet.net.Properties.Resources.description;
            xmlResponse = xmlResponse.Replace("##URLBASE##", ipAdressToPublish);
            xmlResponse = xmlResponse.Replace("##MAC##", mac.ToString().ToLower());
            xmlResponse = xmlResponse.Replace("##UDN##", udn);
            var bytes = System.Text.Encoding.Default.GetBytes(xmlResponse);
            var response = context.Response;
            response.ContentType = "text/xml";
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        public static void ListenerThread()
        {
            int retryCounter = 0;
            while (true)
            {
                if (retryCounter++==3)
                {
                    throw new InvalidOperationException("HttpListener failed.");
                }
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add("http://+:80/");
                    listener.Start();
                    break;
                }
                catch (HttpListenerException)
                {
                    NetAclChecker.AddAddress("http://+:80/");
                }
            }
             while (true)
            {
                var context = listener.GetContext();
                var request = context.Request;
                Debug.WriteLine("Incoming Webrequest from " + request.RemoteEndPoint.ToString() + ": " + request.RawUrl);
                bool contextHandled = false;
                foreach (var i in urlRegex)
                {
                    var matches = i.Key.Matches(request.RawUrl);
                    if (matches.Count>0)
                    {
                        contextHandled = true;
                        i.Value(context, matches);
                        break;
                    }
                }

                if (contextHandled==false)
                {
                    // 404 the hell out of here
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
                else
                {
                    context.Response.Close();
                }
            }
        }
    }


}
