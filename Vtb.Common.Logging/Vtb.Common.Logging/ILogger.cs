namespace Vtb.Common.Logging
{
    using System;
    using System.IO;

    public enum EventsLoggingLevels
    {
        All,
        Nothing,
        ErrorsOnly
    }

    public interface ILogger
    {
        /// <summary>
        /// Каталог сохранения логов.
        /// </summary>
        string FolderPath { get; set; }

        /// <summary>
        /// уровень логирования (All,Nothing,ErrorsOnly)
        /// </summary>
        EventsLoggingLevels LoggingLevel { get; set; }

        /// <summary>
        /// Максимальный размер файла лога
        /// </summary>
        uint SizeLimit { get; set; }

        /// <summary>
        /// Закрыть все открытые файлы и начать новые части.
        /// </summary>
        void CutAllStreams();
        void CloseLog(string fileName);

        string FileInfo(string fileName);

        bool AddLine(string eventText, string fileName = "out");
        bool AddInfo(string eventText, string fileName = "info");
        bool AddError(string eventText, Exception innerException, string fileName = "error");
    }
}