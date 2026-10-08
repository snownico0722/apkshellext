using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace ApkShellextIntegration {
    internal static class ServiceSmoke {
        private static int Main() {
            string staging = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ApkShellext2", "Share");
            string original = Path.Combine(Path.GetTempPath(),
                "ApkShellService_Outside_" + Guid.NewGuid().ToString("N") + ".apk");
            string shared = Path.Combine(staging,
                "ApkShellService_" + Guid.NewGuid().ToString("N") + ".apk");
            object server = null;
            Type serverType = null;
            try {
                string servicePath = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "ApkShellextService", "bin", "Release",
                    "apkShellextService.exe"));
                if (!File.Exists(servicePath))
                    throw new Exception("Optional service was not built: " + servicePath);

                Assembly assembly = Assembly.LoadFrom(servicePath);
                Type serviceType = assembly.GetType("ApkShellext2.apkShellextService", true);
                serverType = assembly.GetType("ApkShellext2.WebServer", true);
                object service = Activator.CreateInstance(serviceType, true);
                MethodInfo send = serviceType.GetMethod("SendResponse");

                Directory.CreateDirectory(staging);
                byte[] bytes = { 11, 22, 33, 44, 55 };
                File.WriteAllBytes(original, bytes);
                File.WriteAllBytes(shared, bytes);

                var portFinder = new TcpListener(IPAddress.Loopback, 0);
                portFinder.Start();
                int port = ((IPEndPoint)portFinder.LocalEndpoint).Port;
                portFinder.Stop();
                string prefix = "http://127.0.0.1:" + port + "/";
                var callback = new Func<HttpListenerRequest, string>(request =>
                    (string)send.Invoke(service, new object[] { request }));
                server = Activator.CreateInstance(serverType,
                    new object[] { new string[] { prefix }, callback });
                serverType.GetMethod("Run").Invoke(server, null);

                using (var client = new WebClient()) {
                    string prohibited = client.DownloadString(prefix + "?path=" +
                        Uri.EscapeDataString(original));
                    if (!string.IsNullOrEmpty(prohibited))
                        throw new Exception("Optional service accepted a file outside its staging directory.");

                    string token = client.DownloadString(prefix + "?path=" +
                        Uri.EscapeDataString(shared)).Trim();
                    if (token.Length != 32)
                        throw new Exception("Optional service did not generate a random 32-digit access token.");
                    string missing = client.DownloadString(prefix + "123456");
                    if (!string.IsNullOrEmpty(missing))
                        throw new Exception("Optional service accepted an old short predictable download token.");

                    byte[] downloaded = client.DownloadData(prefix + token);
                    if (downloaded.Length != bytes.Length)
                        throw new Exception("Unexpected optional service download length.");
                    for (int i = 0; i < bytes.Length; i++)
                        if (bytes[i] != downloaded[i])
                            throw new Exception("Optional service changed staged file bytes.");
                }
                Console.WriteLine("PASS: optional download service requires staging and random tokens");
                return 0;
            } catch (Exception ex) {
                Console.Error.WriteLine("FAIL: optional service: " + ex);
                return 1;
            } finally {
                if (server != null)
                    serverType.GetMethod("Stop").Invoke(server, null);
                if (File.Exists(original)) File.Delete(original);
                if (File.Exists(shared)) File.Delete(shared);
            }
        }
    }
}
