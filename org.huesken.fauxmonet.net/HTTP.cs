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

namespace org.huesken.hueemu.net
{
    public static class HTTP
    {
        private static HttpListener listener;
        private static Thread listenerThread;
        private static string ipAdressToPublish;
        private delegate void OnHandler(HttpListenerContext context, MatchCollection matches);
        private static IList<KeyValuePair<Regex, OnHandler>> urlRegex;
        public static void Start(string ipAdressToPublish)
        {
            urlRegex = new List<KeyValuePair<Regex, OnHandler>>()
            {
                new KeyValuePair<Regex, OnHandler>(new Regex("/description.xml"), (x,y)=> OnDescription(x,y) )

            };

            HTTP.ipAdressToPublish = ipAdressToPublish;
            if (!HttpListener.IsSupported)
            {
                throw new NotSupportedException("HttpListener not suppored. Windows XP SP2 required.");
            }

            listenerThread = new Thread(new ThreadStart(ListenerThread));
            listenerThread.Start();
        }

        public static void OnDescription(HttpListenerContext context, MatchCollection matches)
        {

        }

        public static void ListenerThread()
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://+:80/");
            listener.Start();
            while (true)
            {
                var context = listener.GetContext();
                var request = context.Request;
                Debug.WriteLine("Incoming Webrequest: " + request.RawUrl);
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
            }
        }
    }


}
