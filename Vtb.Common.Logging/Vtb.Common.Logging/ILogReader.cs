namespace Vtb.Common.Logging
{
    using System;
    public interface ILogReader
    {
        /// <summary>
        /// Каталог сохранения логов.
        /// </summary>
        string FolderPath { get; set; }
        string FileInfo(string fileName);
        string NextLog(string fileName);
        void Subscribe(string fileName, Func<string, bool> newLine);
        void Subscribe(string fileName, Func<byte[], bool> newLine);

    }
}
