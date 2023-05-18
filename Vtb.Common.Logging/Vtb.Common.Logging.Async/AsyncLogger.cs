namespace Vtb.Common.Logging
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Reflection;
    using System.Text;

    public class AsyncLogger : ILogger, IDisposable
    {
        /// <summary>
        /// Оставшееся место на диске в данный момент
        /// </summary>
        private long m_currentFreeDiscSpaceLeft;

        /// <summary>
        /// Настройка с минимальным допустимым остатком места на диске
        /// </summary>
        private readonly long m_minimunFreeDiscSpaceLeft;

        private DateTime _lastLowDiscSpaceAlert = DateTime.MaxValue;
        private DateTime _lastOverHeadQueueAlert = DateTime.MinValue;

        /// <summary>
        /// уровень логирования (All,Nothing,ErrorsOnly)
        /// </summary>
        public EventsLoggingLevels LoggingLevel { get; set; }

        public string FolderPath { get; set; }

        public uint SizeLimit { get; set; }
        
        public void CutAllStreams()
        {
            foreach (var fileName in m_files.Keys)
            {
                if (m_files.TryRemove(fileName, out var file))
                    file.Dispose();
            }
        }

        private ConcurrentDictionary<string, AsyncLogFile> m_files;

        private bool InternalAdd(string eventText, Exception innerException, string fileName)
        {
            return InternalAdd(eventText, innerException, fileName, DateTime.Now);
        }

        private bool InternalAdd(string eventText, Exception innerException, string fileName, DateTime moment)
        {
            // отметаем запись в файлы если надо
            if (LoggingLevel == EventsLoggingLevels.Nothing ||
                (LoggingLevel == EventsLoggingLevels.ErrorsOnly && !(eventText != "critical")))
            {
                return false;
            }

            var sb = new StringBuilder(eventText.Length);
            
            sb.Append(string.Concat(
                moment.Year.ToString(), ".", moment.Month.ToString("00"), ".", moment.Day.ToString("00"), " ",
                moment.Hour.ToString("00"), ":", moment.Minute.ToString("00"), ":", moment.Second.ToString("00"), ".",
                moment.Millisecond.ToString("000"), "\t"));

            if (innerException == null)
                sb.AppendLine(eventText);
            else
            {
                sb.Append(eventText);

                while (innerException != null)
                {
                    sb.Append(" ");
                    sb.AppendLine(innerException.ToString());
                    innerException = innerException.InnerException;
                }
            }

            return InternalAdd(sb.ToString(), fileName);
        }

        private bool InternalAdd(string eventText, string fileName)
        {
            var file = m_files.GetOrAdd(fileName, 
                fn => 
                {
                    var systemFileName = AsyncLogFile.GetNextFileName(fn, FolderPath);
                    return new AsyncLogFile(systemFileName);
                });

            var bytes = Encoding.UTF8.GetBytes(eventText);
            file.Write(bytes, (offset) => CheckOffset(offset, bytes, fileName));

            return true;
        }

        public bool CheckOffset(long offset, byte[] bytes, string fileName)
        {
            if (offset < SizeLimit)
                return true;

            CreateLog(fileName, fn => AsyncLogFile.GetNextFileName(fn, FolderPath))
                .Write(bytes);
            return false;
        }

        private AsyncLogFile CreateLog(string fileName, Func<string, string> fileNameGetter, FileMode fileMode = FileMode.OpenOrCreate)
        {
            var systemFileName = fileNameGetter(fileName);
            return m_files.AddOrUpdate(fileName,
                fn => new AsyncLogFile(systemFileName, fileMode),
                (fn, fl) =>
                {
                    fl.Dispose();
                    return new AsyncLogFile(systemFileName, fileMode);
                });
        }

        public string FileInfo(string fileName)
        {
            return m_files.TryGetValue(fileName, out var file) ?
                file.FileName : null;
        }

        public void CloseLog(string fileName)
        {
            if (m_files.TryRemove(fileName, out var file))
                file.Dispose();
        }

        public bool AddLine(string eventText, string fileName = "out")
        {
            return InternalAdd(eventText, fileName);
        }

        public bool AddInfo(string eventText, string fileName = "info")
        {
            return InternalAdd(eventText, null, fileName);
        }

        public bool AddError(string eventText, Exception innerException, string fileName = "error")
        {
            return InternalAdd(eventText, innerException, fileName);
        }

        public void Dispose()
        {
            CutAllStreams();
        }

        public AsyncLogger(AsyncLoggerConfigurationSection config)
        {
            LoggingLevel = config.LoggingLevel;
            FolderPath = (string.IsNullOrEmpty(config.LogPath) || string.IsNullOrWhiteSpace(config.LogPath)) ?
                          Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\Logs\" : Path.GetFullPath(config.LogPath);

            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            // если в конфиге указано сколько надо оставить на диске места то приводим,
            // а если нет то остается значение по умолчанию
            m_currentFreeDiscSpaceLeft = m_minimunFreeDiscSpaceLeft = config.LogDiscSpaceLeftMinimum;
            //var prms = new FreeSpaceCheckerThreadParams("DiscFreeSpaceLeftChecker_Thread")
            //{
            //	DriveLetter = _folderPath[0]
            //};
            //(new ThreadBase(CheckForDiscSpace, prms)).Start();
            //Task.Factory.StartNew(() => CheckForDiscSpace(_folderPath[0]));
            SizeLimit = config.LogSizeLimit;

            m_files = new ConcurrentDictionary<string, AsyncLogFile>();
        }
    }
}
