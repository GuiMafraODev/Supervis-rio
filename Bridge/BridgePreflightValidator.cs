
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace SupervisorioECA.Bridge
{
    public class ValidationIssue
    {
        public string Severity { get; set; }
        public string Component { get; set; }
        public string Message { get; set; }

        public ValidationIssue(
            string severity,
            string component,
            string message)
        {
            Severity = severity;
            Component = component;
            Message = message;
        }
    }

    public class BridgePreflightValidator
    {
        private readonly string root;
        private readonly JavaScriptSerializer serializer;

        public BridgePreflightValidator(string projectRoot)
        {
            if (String.IsNullOrEmpty(projectRoot))
                throw new ArgumentException(
                    "Diretorio do projeto nao informado."
                );

            root = Path.GetFullPath(projectRoot);

            serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;
        }

        public IList<ValidationIssue> Validate()
        {
            List<ValidationIssue> issues =
                new List<ValidationIssue>();

            ValidateDirectories(issues);
            ValidateConfiguration(issues);
            ValidateTags(issues);

            return issues;
        }

        private void ValidateDirectories(
            List<ValidationIssue> issues)
        {
            string[] directories =
            {
                "Bridge",
                "Web",
                "Web\\css",
                "Web\\js",
                "Data",
                "Data\\Config",
                "Data\\Trends",
                "Data\\Events",
                "Data\\Backup"
            };

            foreach (string directory in directories)
            {
                string path = Path.Combine(root, directory);

                if (!Directory.Exists(path))
                {
                    Add(
                        issues,
                        "ERROR",
                        "Diretorios",
                        "Pasta ausente: " + directory
                    );
                }
            }

            CheckFile(
                issues,
                "Web\\index.html",
                "Interface"
            );

            CheckFile(
                issues,
                "Web\\css\\style.css",
                "Interface"
            );

            CheckFile(
                issues,
                "Web\\js\\app.js",
                "Interface"
            );
        }

        private void ValidateConfiguration(
            List<ValidationIssue> issues)
        {
            string path = Path.Combine(
                root,
                "Data\\Config\\appsettings.json"
            );

            if (!File.Exists(path))
            {
                Add(
                    issues,
                    "ERROR",
                    "Configuracao",
                    "appsettings.json nao encontrado."
                );

                return;
            }

            try
            {
                Dictionary<string, object> config =
                    serializer.Deserialize<
                        Dictionary<string, object>
                    >(File.ReadAllText(path));

                Dictionary<string, object> api =
                    GetSection(config, "api");

                Dictionary<string, object> security =
                    GetSection(config, "security");

                if (api == null || security == null)
                {
                    Add(
                        issues,
                        "ERROR",
                        "Configuracao",
                        "Secoes API ou security ausentes."
                    );

                    return;
                }

                string host = GetString(api, "host");
                bool remote = GetBoolean(
                    api,
                    "enableRemoteAccess"
                );

                bool writes = GetBoolean(
                    security,
                    "allowPlcWrites"
                );

                if (host != "127.0.0.1")
                {
                    Add(
                        issues,
                        "ERROR",
                        "Seguranca",
                        "A API deve utilizar somente 127.0.0.1."
                    );
                }

                if (remote || writes)
                {
                    Add(
                        issues,
                        "ERROR",
                        "Seguranca",
                        "Acesso remoto ou escrita em CLP esta habilitado."
                    );
                }

                if (host == "127.0.0.1" &&
                    !remote &&
                    !writes)
                {
                    Add(
                        issues,
                        "OK",
                        "Seguranca",
                        "API local e operacao somente leitura."
                    );
                }
            }
            catch (Exception ex)
            {
                Add(
                    issues,
                    "ERROR",
                    "Configuracao",
                    "Falha ao ler appsettings.json: " +
                    ex.Message
                );
            }
        }

        private void ValidateTags(
            List<ValidationIssue> issues)
        {
            string path = Path.Combine(
                root,
                "Data\\Config\\tags.json"
            );

            if (!File.Exists(path))
            {
                Add(
                    issues,
                    "ERROR",
                    "Tags",
                    "tags.json nao encontrado."
                );

                return;
            }

            try
            {
                Dictionary<string, object> config =
                    serializer.Deserialize<
                        Dictionary<string, object>
                    >(File.ReadAllText(path));

                object rawTags;

                if (!config.TryGetValue("tags", out rawTags))
                {
                    Add(
                        issues,
                        "ERROR",
                        "Tags",
                        "Lista tags nao encontrada."
                    );

                    return;
                }

                object[] tags = rawTags as object[];

                if (tags == null)
                {
                    Add(
                        issues,
                        "ERROR",
                        "Tags",
                        "Formato da lista de tags invalido."
                    );

                    return;
                }

                if (tags.Length != 16)
                {
                    Add(
                        issues,
                        "ERROR",
                        "Tags",
                        "Esperadas 16 variaveis; encontradas " +
                        tags.Length + "."
                    );
                }
                else
                {
                    Add(
                        issues,
                        "OK",
                        "Tags",
                        "As 16 variaveis do inventario estao presentes."
                    );
                }

                HashSet<string> identifiers =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase
                    );

                foreach (object rawTag in tags)
                {
                    Dictionary<string, object> tag =
                        rawTag as Dictionary<string, object>;

                    if (tag == null)
                    {
                        Add(
                            issues,
                            "ERROR",
                            "Tags",
                            "Registro de variavel invalido."
                        );

                        continue;
                    }

                    string id = GetString(tag, "id");
                    string topic = GetString(tag, "topic");
                    string itemId = GetString(tag, "itemId");

                    if (String.IsNullOrEmpty(id) ||
                        String.IsNullOrEmpty(topic) ||
                        String.IsNullOrEmpty(itemId))
                    {
                        Add(
                            issues,
                            "ERROR",
                            "Tags",
                            "Variavel sem identificador, topico ou Item ID."
                        );

                        continue;
                    }

                    if (!identifiers.Add(id))
                    {
                        Add(
                            issues,
                            "ERROR",
                            "Tags",
                            "Identificador duplicado: " + id
                        );
                    }
                }

                Add(
                    issues,
                    "WARNING",
                    "OPC",
                    "Topicos e Item IDs ainda exigem validacao integrada no RSLinx."
                );
            }
            catch (Exception ex)
            {
                Add(
                    issues,
                    "ERROR",
                    "Tags",
                    "Falha ao ler tags.json: " + ex.Message
                );
            }
        }

        private void CheckFile(
            List<ValidationIssue> issues,
            string relativePath,
            string component)
        {
            string path = Path.Combine(root, relativePath);

            if (!File.Exists(path))
            {
                Add(
                    issues,
                    "ERROR",
                    component,
                    "Arquivo ausente: " + relativePath
                );
            }
        }

        private Dictionary<string, object> GetSection(
            Dictionary<string, object> source,
            string name)
        {
            if (source == null)
                return null;

            object value;

            if (!source.TryGetValue(name, out value))
                return null;

            return value as Dictionary<string, object>;
        }

        private string GetString(
            Dictionary<string, object> source,
            string name)
        {
            if (source == null)
                return "";

            object value;

            if (!source.TryGetValue(name, out value) ||
                value == null)
                return "";

            return Convert.ToString(value);
        }

        private bool GetBoolean(
            Dictionary<string, object> source,
            string name)
        {
            if (source == null)
                return false;

            object value;

            if (!source.TryGetValue(name, out value) ||
                value == null)
                return false;

            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return false;
            }
        }

        private void Add(
            List<ValidationIssue> issues,
            string severity,
            string component,
            string message)
        {
            issues.Add(
                new ValidationIssue(
                    severity,
                    component,
                    message
                )
            );
        }
    }
}
