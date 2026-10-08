using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Threading;
using System.Collections.Concurrent;
using System.Text;

namespace ApkShellext2 {
    class apkShellextService : ServiceBase {
        public apkShellextService() {
            ServiceName = "ApkShellext Service";
            EventLog.Log = "Application";
            
            CanHandlePowerEvent = false;
            CanHandleSessionChangeEvent = false;
            CanPauseAndContinue = false;
            CanShutdown = false;
            CanStop = true;
        }

        static void Main() {
            ServiceBase.Run(new apkShellextService());
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
        }

        private WebServer ws;

        // This legacy QR download service is optional, not installed by the
        // normal Shell extension installer. It may only share files explicitly
        // staged in this directory, never an arbitrary LocalSystem-readable path.
        private static readonly string ShareRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ApkShellext2", "Share"));

        protected override void OnStart(string[] args) {
            base.OnStart(args);
            Directory.CreateDirectory(ShareRoot);
            // Valid HttpListener wildcard prefix, with a required trailing '/'.
            // Remote download requests need a random per-file token.
            ws = new WebServer(SendResponse, "http://+:42728/");
            ws.Run();
        }

        protected override void OnStop() {
            if (ws != null) ws.Stop();
            base.OnStop();
        }

        /// <summary>
        /// OnCustomCommand(): If you need to send a command to your
        ///   service without the need for Remoting or Sockets, use
        ///   this method to do custom methods.
        /// </summary>
        /// <param name="command">Arbitrary Integer between 128 & 256</param>
        protected override void OnCustomCommand(int command) {
            //  A custom command can be sent to a service by using this method:
            //#  int command = 128; //Some Arbitrary number between 128 & 256
            //#  ServiceController sc = new ServiceController("NameOfService");
            //#  sc.ExecuteCommand(command);

            base.OnCustomCommand(command);
        }

        private readonly ConcurrentDictionary<string, string> pathList =
            new ConcurrentDictionary<string, string>();

        public string SendResponse(HttpListenerRequest request) {
            string requestedPath = request.QueryString["path"];
            if (requestedPath != null) {
                // Only a process on this machine can register a shared file.
                // The endpoint never accepts a supplied download token.
                if (request.RemoteEndPoint == null ||
                    !IPAddress.IsLoopback(request.RemoteEndPoint.Address))
                    return "";
                string path;
                try { path = Path.GetFullPath(requestedPath); }
                catch { return ""; }
                string prefix = ShareRoot.TrimEnd(Path.DirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(path))
                    return "";
                string token = Guid.NewGuid().ToString("N");
                pathList[token] = path;
                return "token:" + token;
            }

            string id = request.Url == null ? "" : request.Url.AbsolutePath.Trim('/');
            string sharedFile;
            return id.Length == 32 && pathList.TryGetValue(id, out sharedFile)
                ? sharedFile : "";
        }
    }


    public class WebServer
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly Func<HttpListenerRequest, string> _responderMethod;
 
        public WebServer(string[] prefixes, Func<HttpListenerRequest, string> method)
        {
            if (!HttpListener.IsSupported)
                throw new NotSupportedException(
                    "Needs Windows XP SP2, Server 2003 or later.");
 
            if (prefixes == null || prefixes.Length == 0)
                throw new ArgumentException("prefixes");
 
            // A responder method is required
            if (method == null)
                throw new ArgumentException("method");
 
            foreach (string s in prefixes)
                _listener.Prefixes.Add(s);
 
            _responderMethod = method;
            _listener.Start();
        }
 
        public WebServer(Func<HttpListenerRequest, string> method, params string[] prefixes)
            : this(prefixes, method) { }
 
        public void Run()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Console.WriteLine("Webserver running...");
                try
                {
                    while (_listener.IsListening)
                    {
                        ThreadPool.QueueUserWorkItem((c) =>
                        {
                            var ctx = c as HttpListenerContext;
                            try
                            {
                                string rstr = _responderMethod(ctx.Request);
                                if (rstr.StartsWith("token:", StringComparison.Ordinal)) {
                                    byte[] token = Encoding.ASCII.GetBytes(rstr.Substring(6));
                                    ctx.Response.ContentType = "text/plain";
                                    ctx.Response.ContentLength64 = token.Length;
                                    ctx.Response.OutputStream.Write(token, 0, token.Length);
                                } else if (rstr != "") {
                                    string filename = Path.GetFileName(rstr);
                                    using (FileStream fs = new FileStream(rstr, FileMode.Open,
                                        FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                                        ctx.Response.ContentType = "application/octet-stream";
                                        ctx.Response.AddHeader("Content-Disposition",
                                            "attachment; filename=\"" + filename + "\"");
                                        ctx.Response.ContentLength64 = fs.Length;
                                        fs.CopyTo(ctx.Response.OutputStream);
                                    }
                                }                         
                            }
                            catch (Exception ex){
                                EventLog log = new EventLog();
                                log.WriteEntry(ex.Message);
                            } // suppress any exceptions
                            finally
                            {
                                // always close the stream
                                ctx.Response.OutputStream.Close();
                            }
                        }, _listener.GetContext());
                    }
                }
                catch { } // suppress any exceptions
            });
        }
 
        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}
