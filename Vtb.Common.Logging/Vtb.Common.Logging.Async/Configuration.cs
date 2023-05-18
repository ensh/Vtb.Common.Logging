using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vtb.Common.Logging
{
    public class AsyncLoggerConfigurationSection : ConfigurationSection
    {
        public const string SectionName = "asyncLog";
        public AsyncLoggerConfigurationSection() { }

        public const string CD = "code";
        [ConfigurationProperty(CD, DefaultValue = null, IsRequired = false)]
        public string Code
        {
            get => (string)this[CD];
            set => this[CD] = value;
        }

        public const string LP = "dir";
        [ConfigurationProperty(LP, DefaultValue = null, IsRequired = false)]
        public string LogPath
        {
            get => (string)this[LP];
            set => this[LP] = value;
        }

        public const string LL = "level";
        [ConfigurationProperty(LL, DefaultValue = EventsLoggingLevels.All, IsRequired = false)]
        public EventsLoggingLevels LoggingLevel
        {
            get => (EventsLoggingLevels)this[LL];
            set => this[LL] = value;
        }

        public const string SPM = "spaceMin";
        [ConfigurationProperty(SPM, DefaultValue = 1073741824U, IsRequired = false)]
        public uint LogDiscSpaceLeftMinimum
        {
            get => (uint)this[SPM];
            set => this[SPM] = value;
        }

        public const string SL = "logSize";
        [ConfigurationProperty(SL, DefaultValue = 1073741824U, IsRequired = false)]
        public uint LogSizeLimit
        {
            get => (uint)this[SL]; 
            set => this[SL] = value;
        }

        public const string BS = "bufferSize";
        [ConfigurationProperty(BS, DefaultValue = 4096*4, IsRequired = false)]
        public int BufferSize
        {
            get => (int)this[BS];
            set => this[BS] = value;
        }

        public const string IT = "interval";
        [ConfigurationProperty(IT, DefaultValue = 1000, IsRequired = false)]
        public int Interval
        {
            get => (int)this[IT];
            set => this[IT] = value;
        }
    }

    public class FileSettings : ConfigurationElement
    {
        public FileSettings()
        {
        }

        public static readonly TimeSpan defaultRDT = new TimeSpan(0, 0, 0);

        public const string RDT = "resetDayTime";
        [ConfigurationProperty(RDT, IsRequired = false)]
        public TimeSpan ResetDayTyme
        {
            get => TimeSpan.TryParse((string)this[RDT], out var result) ? result : defaultRDT;
            set => this[RDT] = value;
        }

        public const string RET = "resetEveryTime";
        [ConfigurationProperty(RET, IsRequired = false)]
        public TimeSpan ResetEveryTyme
        {
            get => TimeSpan.TryParse((string)this[RET], out var result) ? result : defaultRDT;
            set => this[RET] = value;
        }
    }
}
