
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

        private object server;
        private object groups;
        private object group;
        private object items;

        private readonly Dictionary<string, object> opcItems =
            new Dictionary<string, object>();

        private bool connected;

        public bool IsConnected
        {
            get { return connected; }
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
                throw new ArgumentException("Servidor OPC nao informado.");

            if (definitions == null)
                throw new ArgumentNullException("definitions");

            this.serverName = serverName;
            this.machineName = machineName;
            this.definitions = definitions;
        }

        public void Connect()
        {
            if (connected)
                return;

            Type automationType =
                Type.GetTypeFromProgID(AutomationProgId);

            if (automationType == null)
            {
                throw new InvalidOperationException(
                    "OPC Automation nao encontrado. " +
                    "Verifique a instalacao dos componentes OPC."
                );
            }

            try
            {
                server = Activator.CreateInstance(automationType);

                Invoke(server, "Connect",
                    new object[] { serverName, machineName });

                groups = GetProperty(server, "OPCGroups");

                group = Invoke(groups, "Add",
                    new object[] { "SupervisorioECA" });

                SetProperty(group, "IsActive", true);
                SetProperty(group, "IsSubscribed", false);
                SetProperty(group, "UpdateRate", 1000);

                items = GetProperty(group, "OPCItems");

                opcItems.Clear();

                int clientHandle = 1;

                foreach (OpcTagDefinition definition in definitions)
                {
                    if (!definition.Enabled)
                        continue;

                    string itemId = BuildItemId(definition);

                    object opcItem = Invoke(items, "AddItem",
                        new object[] { itemId, clientHandle });

                    opcItems.Add(definition.TagId, opcItem);

                    clientHandle++;
                }

                connected = true;
                RaiseConnectionStateChanged();
            }
            catch
            {
                Disconnect();
                throw;
            }
        }

        public void Disconnect()
        {
            connected = false;

            opcItems.Clear();

            ReleaseComObject(items);
            ReleaseComObject(group);
            ReleaseComObject(groups);

            items = null;
            group = null;
            groups = null;

            if (server != null)
            {
                try
                {
                    Invoke(server, "Disconnect", new object[0]);
                }
                catch
                {
                    // A desconexao pode ocorrer com o servidor indisponivel.
                }
            }

            ReleaseComObject(server);
            server = null;

            RaiseConnectionStateChanged();
        }

        public IList<OpcValue> ReadAll()
        {
            IList<OpcValue> result = new List<OpcValue>();

            foreach (OpcTagDefinition definition in definitions)
            {
                if (!definition.Enabled)
                    continue;

                result.Add(Read(definition.TagId));
            }

            return result;
        }

        public OpcValue Read(string tagId)
        {
            if (!connected)
            {
                return OpcValue.CreateBad(
                    tagId,
                    "Bad",
                    "Cliente OPC desconectado."
                );
            }

            object opcItem;

            if (!opcItems.TryGetValue(tagId, out opcItem))
            {
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

                Invoke(opcItem, "Read", arguments);

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
                    rawQuality, CultureInfo.InvariantCulture
                );

                string qualityText = GetQualityName(quality);

                if (qualityText != "Good")
                {
                    return OpcValue.CreateBad(
                        tagId,
                        qualityText,
                        "Qualidade OPC nao permite utilizar a leitura."
                    );
                }

                double value = Convert.ToDouble(
                    rawValue, CultureInfo.InvariantCulture
                );

                DateTime timestamp = DateTime.Now;

                if (rawTimestamp != null)
                {
                    timestamp = Convert.ToDateTime(
                        rawTimestamp, CultureInfo.InvariantCulture
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
                    ex.Message
                );
            }
        }

        private string BuildItemId(OpcTagDefinition definition)
        {
            string itemId = definition.ItemId.Trim();

            if (itemId.StartsWith("["))
                return itemId;

            return "[" + definition.Topic + "]" + itemId;
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
            return target.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                null,
                target,
                arguments
            );
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
                    // Evita interromper o encerramento por falha COM.
                }
            }
        }

        private void RaiseConnectionStateChanged()
        {
            EventHandler handler = ConnectionStateChanged;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
