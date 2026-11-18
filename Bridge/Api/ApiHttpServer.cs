
using System;
using System.IO;
using System.Net;
using System.Threading;
using SupervisorioECA.Bridge.Communication;

namespace SupervisorioECA.Bridge.Api
{
    public class ApiHttpServer : IDisposable
    {
        private readonly HttpListener listener;
        private readonly ApiRequestHandler apiHandler;
        private readonly TrendExportController exportController;
        private readonly string webRoot;
        private readonly BridgeLogger logger;

        private Thread worker;
        private volatile bool running;

        public ApiHttpServer(
            int port,
            string webRoot,
            ApiRequestHandler apiHandler,
            TrendExportController exportController,
            BridgeLogger logger)
        {
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException("port");

            if (String.IsNullOrEmpty(webRoot))
                throw new ArgumentException("Pasta Web nao informada.");

            if (apiHandler == null)
                throw new ArgumentNullException("apiHandler");

            this.webRoot = Path.GetFullPath(webRoot);
            this.apiHandler = apiHandler;
            this.exportController = exportController;
            this.logger = logger;

            listener = new HttpListener();
            listener.Prefixes.Add(
                "http://127.0.0.1:" + port + "/"
            );
        }

        public void Start()
        {
            if (running)
                return;

            listener.Start();
            running = true;

            worker = new Thread(ListenLoop);
            worker.IsBackground = true;
            worker.Name = "SupervisorioECA.HttpServer";
            worker.Start();

            Log("Servidor HTTP iniciado.");
        }

        private void ListenLoop()
        {
            while (running)
            {
                try
                {
                    HttpListenerContext context =
                        listener.GetContext();

                    ProcessRequest(context);
                }
                catch (HttpListenerException)
                {
                    if (!running)
                        break;

                    Log("Falha ao receber requisicao HTTP.");
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log("Erro HTTP: " + ex.Message);
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath;

            try
            {
                if (String.Equals(
                    path,
                    "/api/trends/export",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (exportController != null &&
                        exportController.Handle(context))
                        return;
                }

                if (path.StartsWith(
                    "/api/",
                    StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(
                    path,
                    "/api",
                    StringComparison.OrdinalIgnoreCase))
                {
                    apiHandler.Handle(context);
                    return;
                }

                ServeStaticFile(context, path);
            }
            catch (Exception ex)
            {
                Log("Erro ao processar requisicao: " + ex.Message);

                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch
                {
                }
            }
        }

        private void ServeStaticFile(
            HttpListenerContext context,
            string path)
        {
            if (!String.Equals(
                context.Request.HttpMethod,
                "GET",
                StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 405;
                context.Response.Close();
                return;
            }

            string relativePath;

            if (path == "/" || path == "/index.html")
                relativePath = "index.html";
            else if (path == "/css/style.css")
                relativePath = Path.Combine("css", "style.css");
            else if (path == "/js/app.js")
                relativePath = Path.Combine("js", "app.js");
            else if (path == "/Data/Config/tags.json")
                relativePath = null;
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            string filePath;

            if (relativePath == null)
            {
                filePath = Path.GetFullPath(
                    Path.Combine(
                        webRoot,
                        "..",
                        "Data",
                        "Config",
                        "tags.json"
                    )
                );
            }
            else
            {
                filePath = Path.GetFullPath(
                    Path.Combine(webRoot, relativePath)
                );
            }

            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            byte[] content = File.ReadAllBytes(filePath);

            context.Response.StatusCode = 200;
            context.Response.ContentType = GetContentType(filePath);
            context.Response.ContentLength64 = content.Length;
            context.Response.Headers["Cache-Control"] = "no-cache";

            using (Stream output = context.Response.OutputStream)
            {
                output.Write(content, 0, content.Length);
            }
        }

        private string GetContentType(string file)
        {
            switch (Path.GetExtension(file).ToLowerInvariant())
            {
                case ".html":
                    return "text/html; charset=utf-8";
                case ".css":
                    return "text/css; charset=utf-8";
                case ".js":
                    return "application/javascript; charset=utf-8";
                case ".json":
                    return "application/json; charset=utf-8";
                default:
                    return "application/octet-stream";
            }
        }

        private void Log(string message)
        {
            if (logger != null)
                logger.Info(message);
        }

        public void Stop()
        {
            running = false;

            try
            {
                if (listener.IsListening)
                    listener.Stop();
            }
            catch
            {
            }

            if (worker != null && worker.IsAlive)
                worker.Join(2000);

            worker = null;
        }

        public void Dispose()
        {
            Stop();
            listener.Close();
        }
    }
}
