
using System;
using System.IO;
using System.Net;
using System.Text;

namespace SupervisorioECA.Bridge.Api
{
    public class ApiRequestHandler
    {
        private readonly ApiController controller;

        public ApiRequestHandler(ApiController controller)
        {
            if (controller == null)
                throw new ArgumentNullException("controller");

            this.controller = controller;
        }

        public bool Handle(HttpListenerContext context)
        {
            if (context == null)
                return false;

            string path = context.Request.Url.AbsolutePath;

            if (!path.StartsWith(
                "/api",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                string method = context.Request.HttpMethod;

                string body = ReadRequestBody(
                    context.Request
                );

                string response = controller.Handle(
                    method,
                    path,
                    context.Request.Url.Query,
                    body
                );

                SendResponse(
                    context.Response,
                    response,
                    200
                );

                return true;
            }
            catch (Exception ex)
            {
                string message =
                    "Falha ao processar requisicao da API.";

                string response =
                    "{\"success\":false," +
                    "\"message\":\"" + EscapeJson(message) + "\"," +
                    "\"data\":null}";

                try
                {
                    SendResponse(
                        context.Response,
                        response,
                        500
                    );
                }
                catch
                {
                    // A conexao pode ter sido encerrada pelo cliente.
                }

                return true;
            }
        }

        private string ReadRequestBody(
            HttpListenerRequest request)
        {
            if (request == null ||
                !request.HasEntityBody)
                return "";

            using (StreamReader reader =
                new StreamReader(
                    request.InputStream,
                    request.ContentEncoding))
            {
                return reader.ReadToEnd();
            }
        }

        private void SendResponse(
            HttpListenerResponse response,
            string content,
            int statusCode)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(
                content ?? ""
            );

            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;

            response.Headers["Cache-Control"] =
                "no-cache, no-store, must-revalidate";

            response.Headers["Pragma"] = "no-cache";

            using (Stream output = response.OutputStream)
            {
                output.Write(
                    buffer,
                    0,
                    buffer.Length
                );
            }
        }

        private string EscapeJson(string value)
        {
            if (String.IsNullOrEmpty(value))
                return "";

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
