
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using SupervisorioECA.Bridge.Communication;

namespace SupervisorioECA.Bridge.Api
{
    public sealed class ApiHttpServer : IDisposable
    {
        private readonly int port;
        private readonly string webRoot;
        private readonly ApiRequestHandler apiHandler;
        private readonly TrendExportController trendExportController;
        private readonly BridgeLogger logger;

        private HttpListener listener;
        private Thread listenerThread;
        private volatile bool running;

        private readonly Dictionary<string, string> allowedFiles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ApiHttpServer(
            int port,
            string webRoot,
            ApiRequestHandler apiHandler,
            TrendExportController trendExportController,
            BridgeLogger logger)
        {
            this.port = port;
            this.webRoot = Path.GetFullPath(webRoot);
            this.apiHandler = apiHandler;
            this.trendExportController = trendExportController;
            this.logger = logger;

            RegisterFiles();
        }

        private void RegisterFiles()
        {
            allowedFiles["/"] = "index.html";
            allowedFiles["/index.html"] = "index.html";

            allowedFiles["/css/style.css"] =
                Path.Combine("css", "style.css");

            allowedFiles["/js/app.js"] =
                Path.Combine("js", "app.js");

            allowedFiles["/js/alarms.js"] =
                Path.Combine("js", "alarms.js");

            allowedFiles["/js/trends.js"] =
                Path.Combine("js", "trends.js");

            allowedFiles["/js/events.js"] =
                Path.Combine("js", "events.js");

            allowedFiles["/data/tags.json"] =
                Path.Combine("..", "Data", "Config", "tags.json");
        }

        public void Start()
        {
            if (running) return;

            listener = new HttpListener();

            listener.Prefixes.Add(
                "http://127.0.0.1:" + port + "/");

            listener.Start();

            running = true;

            listenerThread = new Thread(ListenLoop);
            listenerThread.IsBackground = true;
            listenerThread.Name = "SupervisorioECA HTTP";

            listenerThread.Start();

            if (logger != null)
                logger.Info("Servidor HTTP iniciado em localhost:" + port);
        }

        private void ListenLoop()
        {
            while (running)
            {
                try
                {
                    HttpListenerContext context =
                        listener.GetContext();

                    ThreadPool.QueueUserWorkItem(
                        delegate(object state)
                        {
                            HandleRequest(
                                (HttpListenerContext)state);
                        },
                        context);
                }
                catch (HttpListenerException)
                {
                    if (running && logger != null)
                        logger.Warning("Falha ao receber requisição HTTP.");
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (running && logger != null)
                        logger.Error("Erro no servidor HTTP: " + ex.Message);
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                string method = context.Request.HttpMethod.ToUpperInvariant();
                string path = context.Request.Url.AbsolutePath;

                if (method != "GET" && method != "POST")
                {
                    WriteResponse(
                        context.Response,
                        405,
                        "Método não permitido.",
                        "text/plain");

                    return;
                }

                if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    if (path.Equals(
                        "/api/trends/export",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (method != "GET")
                        {
                            WriteResponse(
                                context.Response,
                                405,
                                "Método não permitido.",
                                "text/plain");

                            return;
                        }

                        trendExportController.Handle(context);
                        return;
                    }

                    if (apiHandler == null)
                    {
                        WriteResponse(
                            context.Response,
                            503,
                            "API indisponível.",
                            "text/plain");

                        return;
                    }

                    apiHandler.Handle(context);
                    return;
                }

                if (method != "GET")
                {
                    WriteResponse(
                        context.Response,
                        405,
                        "Método não permitido.",
                        "text/plain");

                    return;
                }

                ServeStaticFile(context, path);
            }
            catch (Exception ex)
            {
                if (logger != null)
                    logger.Error("Erro ao processar requisição: " + ex.Message);

                try
                {
                    WriteResponse(
                        context.Response,
                        500,
                        "Erro interno do servidor.",
                        "text/plain");
                }
                catch
                {
                }
            }
        }

        private void ServeStaticFile(
            HttpListenerContext context,
            string requestedPath)
        {
            string relativePath;

            if (!allowedFiles.TryGetValue(
                requestedPath,
                out relativePath))
            {
                WriteResponse(
                    context.Response,
                    404,
                    "Arquivo não encontrado.",
                    "text/plain");

                return;
            }

            string fullPath = Path.GetFullPath(
                Path.Combine(webRoot, relativePath));

            string rootPrefix = webRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(
                    fullPath,
                    webRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                WriteResponse(
                    context.Response,
                    403,
                    "Acesso negado.",
                    "text/plain");

                return;
            }

            if (!File.Exists(fullPath))
            {
                WriteResponse(
                    context.Response,
                    404,
                    "Arquivo não encontrado.",
                    "text/plain");

                return;
            }

            byte[] content = File.ReadAllBytes(fullPath);

            string contentType = GetContentType(fullPath);

            context.Response.StatusCode = 200;
            context.Response.ContentType = contentType;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = content.Length;
            context.Response.Headers["Cache-Control"] = "no-cache";

            context.Response.OutputStream.Write(
                content,
                0,
                content.Length);

            context.Response.OutputStream.Close();
        }

        private string GetContentType(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();

            switch (extension)
            {
                case ".html":
                    return "text/html";

                case ".css":
                    return "text/css";

                case ".js":
                    return "application/javascript";

                case ".json":
                    return "application/json";

                case ".csv":
                    return "text/csv";

                case ".png":
                    return "image/png";

                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";

                case ".ico":
                    return "image/x-icon";

                default:
                    return "application/octet-stream";
            }
        }

        private void WriteResponse(
            HttpListenerResponse response,
            int status,
            string content,
            string contentType)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);

            response.StatusCode = status;
            response.ContentType = contentType + "; charset=utf-8";
            response.ContentLength64 = bytes.Length;

            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        public void Stop()
        {
            running = false;

            if (listener != null)
            {
                try
                {
                    listener.Stop();
                    listener.Close();
                }
                catch
                {
                }

                listener = null;
            }

            if (listenerThread != null)
            {
                try
                {
                    listenerThread.Join(3000);
                }
                catch
                {
                }

                listenerThread = null;
            }

            if (logger != null)
                logger.Info("Servidor HTTP encerrado.");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
