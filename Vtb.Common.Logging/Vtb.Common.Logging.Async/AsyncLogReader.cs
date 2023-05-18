namespace Vtb.Common.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    
    public class AsyncLogReader : ILogReader, IDisposable
    {
        public AsyncLogReader(AsyncLoggerConfigurationSection config)
        {
            BufferSize = config.BufferSize;
            Interval = config.Interval;
            FolderPath = (string.IsNullOrEmpty(config.LogPath) || string.IsNullOrWhiteSpace(config.LogPath)) ?
              Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\Logs\" : Path.GetFullPath(config.LogPath);

            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            m_files = new ConcurrentDictionary<string, Tuple<AsyncLogFile, CircularBuffer<byte>>>();
        }

        public void Dispose()
        {
            foreach (var fileName in m_files.Keys)
            {
                if (m_files.TryRemove(fileName, out var file))
                    file.Item1.Dispose();
            }
        }

        public string FolderPath { get; set; }

        public string FileInfo(string fileName)
        {
            return m_files.TryGetValue(fileName, out var file) ?
                file.Item1.FileName : null;
        }

        public string NextLog(string fileName)
        {
            return CreateLog(fileName, fn => AsyncLogFile.GetNextFileName(fn, FolderPath)).Item1.FileName;
        }

        private Tuple<AsyncLogFile, CircularBuffer<byte>> CreateLog(string fileName, Func<string, string> fileNameGetter)
        {
            var systemFileName = fileNameGetter(fileName);
            return m_files.AddOrUpdate(fileName,
                fn => Tuple.Create(new AsyncLogFile(systemFileName), new CircularBuffer<byte>(BufferSize << 1)),
                (fn, fl) =>
                {
                    fl.Item1.Dispose();
                    return Tuple.Create(new AsyncLogFile(systemFileName), new CircularBuffer<byte>(BufferSize << 1));
                });
        }

        public void Subscribe(string fileName, Func<string, bool> newLine)
        {
            Subscribe(fileName, bytes => newLine(Encoding.UTF8.GetString(bytes)));
        }

        public void Subscribe(string fileName, Func<byte[], bool> newLine)
        {
            var fileNameParts = fileName.Split('.');

            if (fileNameParts.Length == 1)
            {
                CreateLog(fileName, fn => AsyncLogFile.GetCurrentFileName(fn, FolderPath));
            }
            else
            {
                CreateLog(fileNameParts[0], fn => Path.Combine(FolderPath, fileName));
                fileName = fileNameParts[0];
            }

            new Thread(() => AsyncMonitoring(fileName, newLine)).Start();
        }

        public int BufferSize { get; set; }
        public int Interval { get; set; }

        private unsafe void AsyncMonitoring(string fileName,  Func<byte[], bool> newLine)
        {
            var bytes = stackalloc byte[BufferSize];
            var monitor = new object();
            lock (monitor)
            {
                while (true)
                {
                    if (!m_files.TryGetValue(fileName, out var file))
                        return; 

                    file.Item1.Read(bytes, BufferSize, (err, cnt) => ReadBytes(file.Item2, bytes, monitor, err, cnt));
                    if (Monitor.Wait(monitor, Interval))
                    {
                        foreach (var line in GetLine(file.Item2))
                        {
                            if (line.Length > 2 && !newLine(line))
                                return;
                        }
                    }

                    if (!newLine(empty))
                        return;
                }
            }
        }

        private unsafe void ReadBytes(CircularBuffer<byte> buffer, byte* byteBuffer, object monitor, uint error, uint count)
        {
            if (count > 0)
            {
                lock (monitor)
                {
                    for (int i = 0; i < count; i++)
                        buffer.Enqueue(byteBuffer[i]);

                    Monitor.Pulse(monitor);
                }
            }
        }

        private IEnumerable<string> GetString(IEnumerable<byte[]> lines)
        {
            foreach (var line in lines)
            {
                yield return Encoding.UTF8.GetString(line);
            }            
        }

        private static byte[] lineEnd = new byte[] { 0xd, 0xa };
        private static byte[] empty = new byte[0];
        private IEnumerable<byte[]> GetLine(CircularBuffer<byte> buffer)
        {
            while (true)
            {
                var endLine = buffer.IndexOf(lineEnd);
                if (endLine > 0)
                {
                    var result = new byte[endLine + 2];
                    for (int i = 0, N = endLine + 2; i < N; i++)
                        result[i] = buffer[i];
                    buffer.Remove(endLine + 2);

                    yield return result;
                }
                else
                    break;
            }
        }

        private ConcurrentDictionary<string, Tuple<AsyncLogFile, CircularBuffer<byte>>> m_files;
    }
}
