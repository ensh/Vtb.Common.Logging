namespace Vtb.Common.Logging
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Permissions;
    using System.Threading;

    [SuppressUnmanagedCodeSecurity]
    internal unsafe static class NativeMethods
    {
        private const string KERNEL32 = "kernel32.dll";

        [Flags]
        internal enum EFileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            WriteThrough = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }

        [DllImport(KERNEL32, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false), SecurityCritical]
        internal static extern IntPtr CreateFile(
            string lpFileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
            [MarshalAs(UnmanagedType.U4)] EFileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport(KERNEL32, SetLastError = true), SecurityCritical]
        internal static extern int WriteFile(IntPtr hFile, byte* lpBuffer, int nNumberOfBytesToWrite, IntPtr lpNumberOfBytesWritten, NativeOverlapped* lpOverlapped);

        [DllImport(KERNEL32, SetLastError = true), SecurityCritical]
        internal static extern int ReadFile(IntPtr hFile, byte* lpBuffer, int nNumberOfBytesToRead, IntPtr lpNumberOfBytesRead, NativeOverlapped* lpOverlapped);

        [DllImport(KERNEL32, SetLastError = true, EntryPoint = "SetFilePointer"), SecurityCritical]
        private static extern int SetFilePointerWin32(IntPtr hFile, int lo, int* hi, int origin);

        [DllImport(KERNEL32, SetLastError = true), SecurityCritical]
        internal static extern int FlushFileBuffers(IntPtr hFile);

        [DllImport(KERNEL32, SetLastError = true), SecurityCritical]
        internal static extern int CancelIo(IntPtr hFile);

        [DllImport(KERNEL32, SetLastError = true), SecurityCritical]
        internal static extern int LockFile(IntPtr hFile, int dwFileOffsetLow, int dwFileOffsetHigh, int nNumberOfBytesToLockLow, int nNumberOfBytesToLockHigh);

        [DllImport(KERNEL32, SetLastError = true), SecurityCritical]
        internal static extern int UnlockFile(IntPtr hFile, int dwFileOffsetLow, int dwFileOffsetHigh, int nNumberOfBytesToLockLow, int nNumberOfBytesToLockHigh);

        [DllImport(KERNEL32, SetLastError = true), SecurityCritical]
        internal static extern int CloseHandle(IntPtr hFile);

        [DllImport(KERNEL32, SetLastError = true), SecurityCritical]
        internal static extern uint GetLastError();

        [DllImport(KERNEL32, SetLastError = true), SecurityCritical]
        internal static extern int GetOverlappedResult(IntPtr hFile, NativeOverlapped* lpOverlapped, ulong* lpNumberOfBytesTransferred, int bWait);

        [SecuritySafeCritical]
        internal static long SetFilePointer(IntPtr hFile, long offset, SeekOrigin origin, out int hr)
        {
            hr = 0;
            int lo = (int)offset;
            int hi = (int)(offset >> 32);
            lo = SetFilePointerWin32(hFile, lo, &hi, (int)origin);
            if (lo == -1 && ((hr = Marshal.GetLastWin32Error()) != 0))
                return -1;
            return (long)(((ulong)((uint)hi)) << 32) | ((uint)lo);
        }
    }

    public sealed unsafe class AsyncLogFile : IDisposable
    {
        public string FileName { get; private set; }
        public AsyncLogFile(string fileName, FileMode fileMode = FileMode.OpenOrCreate, bool createImmediate = true)
        {
            //new FileIOPermission(FileIOPermissionAccess.Write | FileIOPermissionAccess.Read, new [] { fileName }).Demand();
            new FileIOPermission(FileIOPermissionAccess.AllAccess, new[] { fileName }).Demand();
            if (createImmediate)
                CreateFile(fileName, fileMode);
        }

        public long Offset { get { return m_writeOffset; } }

        [SecuritySafeCritical]
        public void CreateFile(string fileName, FileMode fileMode = FileMode.OpenOrCreate)
        {
            m_fileHandle = NativeMethods.CreateFile(FileName = fileName, FileAccess.Write | FileAccess.Read,
                FileShare.ReadWrite, IntPtr.Zero, fileMode,
                NativeMethods.EFileAttributes.Overlapped, IntPtr.Zero);
            m_writeOffset = (int)NativeMethods.SetFilePointer(m_fileHandle, 0, SeekOrigin.End, out var hr);
            m_readOffset = 0;

            ThreadPool.BindHandle(m_fileHandle);
        }

        ~AsyncLogFile()
        {
            Dispose(false);
        }

        private static int OverlappedSize = Marshal.SizeOf(typeof(NativeOverlapped));

        private volatile int lastPosition;
        private const int DefaultFileFlushSize = 4096;

        [SecurityCritical]
        public void WriteComplete(uint errorCode, uint numBytes, NativeOverlapped* pOverlapped)
        {
            if (m_fileHandle != IntPtr.Zero)
            {
                int temp = lastPosition;
                if (pOverlapped->OffsetLow - temp > DefaultFileFlushSize)
                {
                    lastPosition = pOverlapped->OffsetLow;
                    NativeMethods.FlushFileBuffers(m_fileHandle);
                }
            }

            Overlapped.Free(pOverlapped);
        }

        [SecurityCritical]
        public void ReadComplete(uint errorCode, uint numBytes, NativeOverlapped* pOverlapped)
        {
            Overlapped.Free(pOverlapped);
        }

        [SecurityCritical]
        public void AsyncReadComplete(uint errorCode, uint numBytes, NativeOverlapped* pOverlapped, Action<uint, uint> read)
        {
            read(errorCode, numBytes);
            m_readOffset += (int)numBytes;
            Overlapped.Free(pOverlapped);
        }

        [SecuritySafeCritical]
        public void Reserve(byte[] buffer, Func<int, bool> checkOffset)
        {
            int temp = 0, offset;
            do
            {
                temp = m_writeOffset;
                offset = temp + buffer.Length;
            } while (Interlocked.CompareExchange(ref m_writeOffset, offset, temp) != temp);

            checkOffset(m_writeOffset);
        }

        [SecuritySafeCritical]
        public void WriteSync(byte[] buffer, Func<int, bool> checkOffset, bool withLock = true)
        {
            int temp = 0, offset;
            do
            {
                temp = m_writeOffset;
                offset = temp + buffer.Length;
            } while (Interlocked.CompareExchange(ref m_writeOffset, offset, temp) != temp);

            if (checkOffset(m_writeOffset))
            {
                Overlapped ovl = new Overlapped(temp, 0, IntPtr.Zero, null);
                NativeOverlapped* pOverlapped = ovl.Pack(WriteComplete, buffer);

                try
                {
                    if (withLock)
                    {
                        while (!(withLock = 0 != NativeMethods.LockFile(m_fileHandle, offset, 0, buffer.Length, 0)))
                            Thread.Sleep(0);
                    }

                    fixed (byte* bytes = buffer)
                    {
                        if (0 == NativeMethods.WriteFile(m_fileHandle, bytes, buffer.Length, IntPtr.Zero, pOverlapped))
                        {
                            var dwResult = NativeMethods.GetLastError();
                            if (ERROR_IO_PENDING == dwResult)
                            {
                                var transferred = stackalloc ulong[1];
                                NativeMethods.GetOverlappedResult(m_fileHandle, pOverlapped, transferred, 1);
                            }
                        }
                    }
                }
                finally
                {
                    if (withLock)
                        NativeMethods.UnlockFile(m_fileHandle, offset, 0, buffer.Length, 0);
                }
            }
        }

        [SecuritySafeCritical]
        public void Write(byte[] buffer, Func<int, bool> checkOffset)
        {
            int temp = 0, offset;
            do
            {
                temp = m_writeOffset;
                offset = temp + buffer.Length;
            } while (Interlocked.CompareExchange(ref m_writeOffset, offset, temp) != temp);

            if (checkOffset(m_writeOffset))
            {
                Overlapped ovl = new Overlapped(temp, 0, IntPtr.Zero, null);
                NativeOverlapped* pOverlapped = ovl.Pack(WriteComplete, buffer);

                fixed (byte* bytes = buffer)
                {
                    NativeMethods.WriteFile(m_fileHandle, bytes, buffer.Length, IntPtr.Zero, pOverlapped);
                }
            }
        }

        [SecuritySafeCritical]
        public void Write(byte[] buffer, int offset)
        {
            Overlapped ovl = new Overlapped(offset, 0, IntPtr.Zero, null);
            NativeOverlapped* pOverlapped = ovl.Pack(WriteComplete, buffer);

            fixed (byte* bytes = buffer)
            {
                NativeMethods.WriteFile(m_fileHandle, bytes, buffer.Length, IntPtr.Zero, pOverlapped);
            }
        }

        [SecuritySafeCritical]
        public void WriteSync(byte[] buffer, int offset, bool withLock = true)
        {
            Overlapped ovl = new Overlapped(offset, 0, IntPtr.Zero, null);
            NativeOverlapped* pOverlapped = ovl.Pack(WriteComplete, buffer);

            try
            {
                if (withLock)
                {
                    while (!(withLock = 0 != NativeMethods.LockFile(m_fileHandle, offset, 0, buffer.Length, 0)))
                        Thread.Sleep(0);
                }

                fixed (byte* bytes = buffer)
                {
                    if (0 == NativeMethods.WriteFile(m_fileHandle, bytes, buffer.Length, IntPtr.Zero, pOverlapped))
                    {
                        var dwResult = NativeMethods.GetLastError();
                        if (ERROR_IO_PENDING == dwResult)
                        {
                            var transferred = stackalloc ulong[1];
                            NativeMethods.GetOverlappedResult(m_fileHandle, pOverlapped, transferred, 1);
                        }
                    }
                }
            }
            finally
            {
                if (withLock)
                    NativeMethods.UnlockFile(m_fileHandle, offset, 0, buffer.Length, 0);
            }
        }

        private const int ERROR_HANDLE_EOF = 38;
        private const int ERROR_INVALID_PARAMETER = 87;
        private const int ERROR_IO_PENDING = 997;

        [SecuritySafeCritical]
        public int ReadSync(byte[] buffer, int offset, bool withLock = true)
        {
            Overlapped ovl = new Overlapped(offset, 0, IntPtr.Zero, null);
            NativeOverlapped* pOverlapped = ovl.Pack(ReadComplete, buffer);

            try
            {
                if (withLock)
                {
                    while (!(withLock = 0 != NativeMethods.LockFile(m_fileHandle, offset, 0, buffer.Length, 0)))
                        Thread.Sleep(0);
                }

                fixed (byte* bytes = buffer)
                {
                    if (0 == NativeMethods.ReadFile(m_fileHandle, bytes, buffer.Length, IntPtr.Zero, pOverlapped))
                    {
                        var dwResult = NativeMethods.GetLastError();
                        if (ERROR_IO_PENDING == dwResult)
                        {
                            var transferred = stackalloc ulong[1];
                            NativeMethods.GetOverlappedResult(m_fileHandle, pOverlapped, transferred, 1);
                            return (int)transferred[0];
                        }
                    }

                    return 0;
                }
            }
            finally
            {
                if (withLock)
                    NativeMethods.UnlockFile(m_fileHandle, offset, 0, buffer.Length, 0);
            }
        }

        [SecuritySafeCritical]
        public int ReadSync(byte[] buffer, bool withLock = true)
        {
            var transferred = stackalloc ulong[1];
            var ovl = new Overlapped(m_readOffset, 0, IntPtr.Zero, null);
            var pOverlapped = ovl.Pack(ReadComplete, buffer);

            try
            {
                if (withLock)
                {
                    while (!(withLock = 0 != NativeMethods.LockFile(m_fileHandle, m_readOffset, 0, buffer.Length, 0)))
                        Thread.Sleep(0);
                }

                fixed (byte* bytes = buffer)
                {
                    if (0 == NativeMethods.ReadFile(m_fileHandle, bytes, buffer.Length, IntPtr.Zero, pOverlapped))
                    {
                        var dwResult = NativeMethods.GetLastError();
                        if (ERROR_IO_PENDING == dwResult)
                        {
                            NativeMethods.GetOverlappedResult(m_fileHandle, pOverlapped, transferred, 1);
                            return (int)transferred[0];
                        }
                    }
                    return 0;
                }
            }
            finally
            {
                var oldOffset = m_readOffset;
                m_readOffset += (int)transferred[0];

                if (withLock)
                {
                    NativeMethods.UnlockFile(m_fileHandle, oldOffset, 0, buffer.Length, 0);
                }
            }
        }

        [SecuritySafeCritical]
        public void Read(byte[] buffer, Action<uint, uint> read)
        {
            fixed (byte* bytes = buffer)
            {
                Read(bytes, buffer.Length, read);
            }
        }

        [SecuritySafeCritical]
        public void Read(byte* bytes, int length, Action<uint, uint> read)
        {
            Overlapped ovl = new Overlapped(m_readOffset, 0, IntPtr.Zero, null);
            NativeOverlapped* pOverlapped = ovl.Pack((a1, a2, a3) => AsyncReadComplete(a1, a2, a3, read));

            NativeMethods.ReadFile(m_fileHandle, bytes, length, IntPtr.Zero, pOverlapped);
        }

        [SecuritySafeCritical]
        public void Write(byte[] buffer)
        {
            Write(buffer, _ => true);
        }

        [SecuritySafeCritical]
        public void Flush()
        {
            NativeMethods.FlushFileBuffers(m_fileHandle);
        }

        public static int GetCurrentPart(string fileName, string folderPath)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var maxPart = Directory.GetFiles(folderPath, fileName + ".???.log").Max();
            if (maxPart != null)
            {
                maxPart = Path.GetFileNameWithoutExtension(maxPart);
                maxPart = maxPart.Substring(maxPart.Length -3);
                return int.TryParse(maxPart, out var part) ? part : 0;
            }

            return 0;
        }

        public static string GetCurrentFileName(string fileName, string folderPath)
        {
            return GenerateFilePath(fileName, folderPath, GetCurrentPart(fileName, folderPath));
        }

        public static string GetNextFileName(string fileName, string folderPath)
        {
            return GenerateFilePath(fileName, folderPath, 1 + GetCurrentPart(fileName, folderPath));
        }

        private static string GenerateFilePath(string fileName, string folderPath, int part)
        {
            string result;
            // проверка на абсолютный путь
            if (Path.IsPathRooted(fileName))
            {
                result = fileName;
            }
            else
            {
                result = Path.GetFullPath(Path.Combine(folderPath, fileName));
            }
            // именование продолжений файла после достижения лимита размера
            result += string.Concat(".", part.ToString("000"), ".log");
            return result;
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [SecuritySafeCritical]
        private void Dispose(bool disposing)
        {
            if (m_fileHandle != IntPtr.Zero)
            {
                if (disposing)
                {
                    NativeMethods.FlushFileBuffers(m_fileHandle);
                    NativeMethods.CloseHandle(m_fileHandle);
                }

                m_fileHandle = IntPtr.Zero;
            }
        }
        #endregion

        private IntPtr m_fileHandle;
        private volatile int m_writeOffset;
        private volatile int m_readOffset;
    }
}
