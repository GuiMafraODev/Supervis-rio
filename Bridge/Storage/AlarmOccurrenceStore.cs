
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using SupervisorioECA.Bridge.Alarms;

namespace SupervisorioECA.Bridge.Storage
{
    public sealed class AlarmOccurrenceStore
    {
        private readonly string filePath;
        private readonly string backupPath;
        private readonly string temporaryPath;

        private readonly object syncRoot = new object();
        private readonly JavaScriptSerializer serializer;

        public AlarmOccurrenceStore(string directory)
        {
            if (String.IsNullOrEmpty(directory))
                throw new ArgumentException("Diretorio invalido.");

            Directory.CreateDirectory(directory);

            filePath = Path.Combine(
                directory,
                "alarm_occurrences.jsonl");

            backupPath = filePath + ".bak";
            temporaryPath = filePath + ".tmp";

            serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;
        }

        public void Save(AlarmOccurrence occurrence)
        {
            if (occurrence == null)
                return;

            if (occurrence.Id <= 0)
                throw new ArgumentException(
                    "A ocorrencia precisa possuir um ID valido.");

            string json = serializer.Serialize(occurrence);

            lock (syncRoot)
            {
                using (FileStream stream = new FileStream(
                    filePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read))
                {
                    using (StreamWriter writer =
                        new StreamWriter(stream))
                    {
                        writer.WriteLine(json);
                        writer.Flush();
                        stream.Flush();
                    }
                }
            }
        }

        public IList<AlarmOccurrence> LoadAll()
        {
            lock (syncRoot)
            {
                // Recupera o backup somente quando
                // o arquivo principal nao existe.
                if (!File.Exists(filePath) &&
                    File.Exists(backupPath))
                {
                    File.Copy(backupPath, filePath);
                }

                return LoadFile(filePath);
            }
        }

        private IList<AlarmOccurrence> LoadFile(
            string path)
        {
            List<AlarmOccurrence> result =
                new List<AlarmOccurrence>();

            if (!File.Exists(path))
                return result;

            Dictionary<long, AlarmOccurrence> latest =
                new Dictionary<long, AlarmOccurrence>();

            using (StreamReader reader =
                new StreamReader(path))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (String.IsNullOrEmpty(line))
                        continue;

                    try
                    {
                        AlarmOccurrence occurrence =
                            serializer.Deserialize<AlarmOccurrence>(
                                line);

                        if (occurrence == null ||
                            occurrence.Id <= 0)
                            continue;

                        // Mantem o ultimo estado gravado
                        // para cada ID de ocorrencia.
                        latest[occurrence.Id] = occurrence;
                    }
                    catch
                    {
                        // Ignora somente a linha invalida.
                    }
                }
            }

            foreach (KeyValuePair<long, AlarmOccurrence> item
                in latest)
            {
                result.Add(item.Value);
            }

            result.Sort(
                delegate(
                    AlarmOccurrence a,
                    AlarmOccurrence b)
                {
                    return a.TriggeredAt.CompareTo(
                        b.TriggeredAt);
                });

            return result;
        }

        public void SaveAll(
            IList<AlarmOccurrence> occurrences)
        {
            if (occurrences == null)
                return;

            lock (syncRoot)
            {
                // Cria o novo arquivo antes de alterar
                // o arquivo principal.
                using (FileStream stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    using (StreamWriter writer =
                        new StreamWriter(stream))
                    {
                        int i;

                        for (i = 0; i < occurrences.Count; i++)
                        {
                            AlarmOccurrence occurrence =
                                occurrences[i];

                            if (occurrence == null ||
                                occurrence.Id <= 0)
                                continue;

                            writer.WriteLine(
                                serializer.Serialize(occurrence));
                        }

                        writer.Flush();
                        stream.Flush();
                    }
                }

                // Preserva o arquivo anterior.
                if (File.Exists(filePath))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);

                    File.Move(filePath, backupPath);
                }

                try
                {
                    File.Move(temporaryPath, filePath);
                }
                catch
                {
                    // Restaura o arquivo anterior
                    // se a substituicao falhar.
                    if (!File.Exists(filePath) &&
                        File.Exists(backupPath))
                    {
                        File.Move(backupPath, filePath);
                    }

                    throw;
                }
            }
        }
    }
}
