
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SupervisorioECA.Bridge.Opc
{
    public class RslinxOpcClient : IOpcClient
    {
        private const string AutomationProgId = "OPC.Automation.1";

        private readonly string serverName;
        private readonly string machineName;
        private readonly IList<OpcTagDefinition> definitions;

        private readonly object syncRoot = new object();

        private object server;
        private object groups;
        private object group;
        private object items;

        private readonly Dictionary<string, object> opcItems =
            new Dictionary<string, object>();

        private readonly Dictionary<string, string> failedItems =
            new Dictionary<string, string>();

        private bool connected;
        private bool disposed;

        public bool IsConnected
        {
            get
            {
                lock (syncRoot)
                {
                    return connected;
                }
            }
        }

        public string ServerName
        {
            get { return serverName; }
        }

        public event EventHandler ConnectionStateChanged;

        public RslinxOpcClient(
            string serverName,
            string machineName,
            IList<OpcTagDefinition> definitions)
        {
            if (String.IsNullOrEmpty(serverName))
                throw new ArgumentException(
                    "Servidor OPC nao informado."
                );

            if (definitions == null)
                throw new ArgumentNullException("definitions");

            this.serverName = serverName;
            this.machineName = machineName;
            this.definitions = definitions;
        }

        public void Connect()
        {
            bool stateChanged = false;

            lock (syncRoot)
            {
                if (disposed)
                    throw new ObjectDisposedException(
                        "RslinxOpcClient"
                    );

                if (connected)
                    return;

                try
                {
                    Type automationType =
                        Type.GetTypeFromProgID(AutomationProgId);

                    if (automationType == null)
                    {
                        throw new InvalidOperationException(
                            "OPC Automation nao encontrado. " +
                            "Verifique a instalacao dos componentes OPC."
                        );
                    }

                    server = Activator.CreateInstance(
                        automationType
                    );

                    Invoke(
                        server,
                        "Connect",
                        new object[] { serverName, machineName }
                    );

                    groups = GetProperty(
                        server,
                        "OPCGroups"
                    );

                    group = Invoke(
                        groups,
                        "Add",
                        new object[] { "SupervisorioECA" }
                    );

                    SetProperty(group, "IsActive", true);
                    SetProperty(group, "IsSubscribed", false);
                    SetProperty(group, "UpdateRate", 1000);

                    items = GetProperty(
                        group,
                        "OPCItems"
                    );

                    opcItems.Clear();
                    failedItems.Clear();

                    int clientHandle = 1;

                    foreach (OpcTagDefinition definition in definitions)
                    {
                        if (definition == null)
                            continue;

                        if (!definition.Enabled)
                            continue;

                        if (String.IsNullOrEmpty(definition.TagId))
                            continue;

                        try
                        {
                            string itemId = BuildItemId(definition);

                            object opcItem = Invoke(
                                items,
                                "AddItem",
                                new object[] { itemId, clientHandle }
                            );

                            if (opcItem == null)
                            {
                                throw new InvalidOperationException(
                                    "RSLinx nao retornou um item OPC valido."
                                );
                            }

                            opcItems[definition.TagId] = opcItem;
                        }
                        catch (Exception ex)
                        {
                            failedItems[definition.TagId] = ex.Message;
                        }

                        clientHandle++;
                    }

                    connected = true;
                    stateChanged = true;
                }
                catch
                {
                    CleanupConnection();
                    connected = false;
                    throw;
                }
            }

            if (stateChanged)
                RaiseConnectionStateChanged();
        }

        public void Disconnect()
        {
            bool stateChanged = false;

            lock (syncRoot)
            {
                stateChanged = connected;

                connected = false;

                CleanupConnection();
            }

            if (stateChanged)
                RaiseConnectionStateChanged();
        }

        public IList<OpcValue> ReadAll()
        {
            IList<OpcValue> result = new List<OpcValue>();

            foreach (OpcTagDefinition definition in definitions)
            {
                if (definition == null || !definition.Enabled)
                    continue;

                result.Add(Read(definition.TagId));
            }

            return result;
        }

        public OpcValue Read(string tagId)
        {
            lock (syncRoot)
            {
                if (!connected)
                {
                    return OpcValue.CreateBad(
                        tagId,
                        "Bad",
                        "Cliente OPC desconectado."
                    );
                }

                if (String.IsNullOrEmpty(tagId))
                {
                    return OpcValue.CreateBad(
                        tagId,
                        "Bad",
                        "Identificador da tag nao informado."
                    );
                }

                object opcItem;

                if (!opcItems.TryGetValue(tagId, out opcItem))
                {
                    string error;

                    if (failedItems.TryGetValue(tagId, out error))
                    {
                        return OpcValue.CreateBad(
                            tagId,
                            "Bad",
                            "Falha ao adicionar item OPC: " + error
                        );
                    }

                    return OpcValue.CreateBad(
                        tagId,
                        "Bad",
                        "Tag nao encontrada no grupo OPC."
                    );
                }

                try
                {
                    object[] arguments = new object[]
                    {
                        1,
                        null,
                        null,
                        null
                    };

                    Invoke(
                        opcItem,
                        "Read",
                        arguments
                    );

                    object rawValue = arguments[1];
                    object rawQuality = arguments[2];
                    object rawTimestamp = arguments[3];

                    if (rawValue == null || rawQuality == null)
                    {
                        return OpcValue.CreateBad(
                            tagId,
                            "Bad",
                            "Servidor OPC retornou dados incompletos."
                        );
                    }

                    int quality = Convert.ToInt32(
                        rawQuality,
                        CultureInfo.InvariantCulture
                    );

                    string qualityText = GetQualityName(quality);

                    if (qualityText != "Good")
                    {
                        return OpcValue.CreateBad(
                            tagId,
                            qualityText,
                            "Qualidade OPC nao permite utilizar a leitura."
                            true
                        );
                    }

                    double value = Convert.ToDouble(
                        rawValue,
                        CultureInfo.InvariantCulture
                    );

                    DateTime timestamp = DateTime.Now;

                    if (rawTimestamp != null)
                    {
                        timestamp = Convert.ToDateTime(
                            rawTimestamp,
                            CultureInfo.InvariantCulture
                        );
                    }

                    return OpcValue.CreateGood(
                        tagId,
                        value,
                        timestamp
                    );
                }
                catch (Exception ex)
                {
                    return OpcValue.CreateBad(
                        tagId,
                        "Bad",
                        "Erro na leitura OPC: " + ex.Message
                    );
                }
            }
        }

        private string BuildItemId(OpcTagDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");

            if (String.IsNullOrEmpty(definition.ItemId))
            {
                throw new InvalidOperationException(
                    "ItemId nao informado para a tag " +
                    definition.TagId
                );
            }

            string itemId = definition.ItemId.Trim();

            if (itemId.StartsWith("["))
                return itemId;

            if (String.IsNullOrEmpty(definition.Topic))
            {
                throw new InvalidOperationException(
                    "Topic nao informado para a tag " +
                    definition.TagId
                );
            }

            return "[" + definition.Topic.Trim() + "]" + itemId;
        }

        private static string GetQualityName(int quality)
        {
            int qualityBits = quality & 0xC0;

            if (qualityBits == 0xC0)
                return "Good";

            if (qualityBits == 0x40)
                return "Uncertain";

            return "Bad";
        }

        private static object GetProperty(
            object target,
            string propertyName)
        {
            if (target == null)
                throw new InvalidOperationException(
                    "Objeto COM nulo ao acessar " + propertyName
                );

            return target.GetType().InvokeMember(
                propertyName,
                BindingFlags.GetProperty,
                null,
                target,
                null
            );
        }

        private static void SetProperty(
            object target,
            string propertyName,
            object value)
        {
            if (target == null)
                throw new InvalidOperationException(
                    "Objeto COM nulo ao configurar " + propertyName
                );

            target.GetType().InvokeMember(
                propertyName,
                BindingFlags.SetProperty,
                null,
                target,
                new object[] { value }
            );
        }

        private static object Invoke(
            object target,
            string methodName,
            object[] arguments)
        {
            if (target == null)
                throw new InvalidOperationException(
                    "Objeto COM nulo ao executar " + methodName
                );

            return target.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                null,
                target,
                arguments
            );
        }

        private void CleanupConnection()
        {
            foreach (object opcItem in opcItems.Values)
                ReleaseComObject(opcItem);

            opcItems.Clear();
            failedItems.Clear();

            ReleaseComObject(items);
            items = null;

            ReleaseComObject(group);
            group = null;

            ReleaseComObject(groups);
            groups = null;

            if (server != null)
            {
                try
                {
                    Invoke(
                        server,
                        "Disconnect",
                        new object[0]
                    );
                }
                catch
                {
                    // O servidor pode estar indisponivel.
                }
            }

            ReleaseComObject(server);
            server = null;
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                try
                {
                    Marshal.FinalReleaseComObject(value);
                }
                catch
                {
                    // Evita interromper o encerramento.
                }
            }
        }

        private void RaiseConnectionStateChanged()
        {
            EventHandler handler = ConnectionStateChanged;

            if (handler == null)
                return;

            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss"
                    ) +
                    " - Erro no evento OPC: " + ex.Message
                );
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;

                disposed = true;
            }

            Disconnect();
        }
    }
}
