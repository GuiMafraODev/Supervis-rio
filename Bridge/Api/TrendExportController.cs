
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using SupervisorioECA.Bridge.Storage;

namespace SupervisorioECA.Bridge.Api
{
    public class TrendExportController
    {
        private readonly TrendExportService exportService;
        private readonly string exportFolder;

        public TrendExportController(
            TrendExportService exportService,
            string exportFolder)
        {
            if (exportService == null)
                throw new ArgumentNullException("exportService");

            if (String.IsNullOrEmpty(exportFolder))
                throw new ArgumentException(
                    "Pasta de exportacao nao informada."
                );

            this.exportService = exportService;
            this.exportFolder = exportFolder;
        }

        public bool Handle(HttpListenerContext context)
        {
            if (context == null)
                return false;

            string path = context.Request.Url.AbsolutePath;

            if (!String.Equals(
                path,
                "/api/trends/export",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!String.Equals(
                context.Request.HttpMethod,
                "GET",
                StringComparison.OrdinalIgnoreCase))
            {
                SendError(
                    context.Response,
                    405,
                    "Metodo nao permitido."
                );

                return true;
            }

            try
            {
                string tagId = GetQueryValue(
                    context.Request.Url.Query,
                    "tagId"
                );

                string startText = GetQueryValue(
                    context.Request.Url.Query,
                    "start"
                );

                string endText = GetQueryValue(
                    context.Request.Url.Query,
                    "end"
                );

                DateTime start;
                DateTime end;

                if (!DateTime.TryParse(
                    startText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out start))
                {
                    throw new ArgumentException(
                        "Data inicial invalida."
                    );
                }

                if (!DateTime.TryParse(
                    endText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out end))
                {
                    throw new ArgumentException(
                        "Data final invalida."
                    );
                }

                string filePath = exportService.ExportCsv(
                    tagId,
                    start,
                    end,
                    exportFolder
                );

                byte[] content = File.ReadAllBytes(filePath);

                context.Response.StatusCode = 200;
                context.Response.ContentType =
                    "text/csv; charset=utf-8";

                context.Response.ContentEncoding =
                    Encoding.UTF8;

                context.Response.ContentLength64 =
                    content.Length;

                context.Response.Headers["Content-Disposition"] =
                    "attachment; filename=\"" +
                    Path.GetFileName(filePath) +
                    "\"";

                context.Response.Headers["Cache-Control"] =
                    "no-cache, no-store";

                using (Stream output =
                    context.Response.OutputStream)
                {
                    output.Write(
                        content,
                        0,
                        content.Length
                    );
                }
            }
            catch (ArgumentException ex)
            {
                SendError(
                    context.Response,
                    400,
                    ex.Message
                );
            }
            catch (Exception)
            {
                SendError(
                    context.Response,
                    500,
                    "Nao foi possivel exportar o historico."
                );
            }

            return true;
        }

        private string GetQueryValue(
            string query,
            string key)
        {
            if (String.IsNullOrEmpty(query))
                return "";

            string text = query.TrimStart('?');
            string[] parts = text.Split('&');

            foreach (string part in parts)
            {
                string[] pair = part.Split(
                    new char[] { '=' },
                    2
                );

                if (pair.Length == 2 &&
                    String.Equals(
                        Uri.UnescapeDataString(pair[0]),
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(
                        pair[1].Replace("+", " ")
                    );
                }
            }

            return "";
        }

        private void SendError(
            HttpListenerResponse response,
            int statusCode,
            string message)
        {
            string json =
                "{\"success\":false,\"message\":\"" +
                EscapeJson(message) +
                "\"}";

            byte[] content = Encoding.UTF8.GetBytes(json);

            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = content.Length;

            using (Stream output = response.OutputStream)
            {
                output.Write(
                    content,
                    0,
                    content.Length
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
                .Replace("\n", "\\n");
        }
    }
}
