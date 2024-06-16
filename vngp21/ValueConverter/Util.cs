using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using log4net.Repository;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace vietnamgiapha
{
    public class Util
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        public static string RemoveSpecialChar(string text)
        {
            //string mystring = "abcdef@_#124";
            //return Regex.Replace(mystring, "[^\\w\\.]", "");
            return text.Replace("\"", "").Replace("'", "");
            //return Regex.Replace(text, @"^[A-Za-z\u00C1\u00C9\u00CD\u00D3\u00DA\u00E1\u00E9\u00ED\u00F3\u00FA][A-Za-z\u00C1\u00C9\u00CD\u00D3\u00DA\u00E1\u00E9\u00ED\u00F3\u00FA0-9@#%&\'\-\s\.\,*]*$", "");
        }

        public static string GetFirstWord(string text)
        {
            var candidate = text.Trim();
            if (!candidate.Any(Char.IsWhiteSpace))
                return text;

            return candidate.Split(' ').FirstOrDefault();
        }
        public static Level LoggerLevel(string logLevel)
        {

            Level level = null;

            if (logLevel != null)
            {
                level = LogManager.GetRepository().LevelMap[logLevel];
            }


            if (level == null)
            {
                level = Level.Info;
            }

            ILoggerRepository[] repositories = LogManager.GetAllRepositories();

            //Configure all loggers to be at the same level.
            foreach (ILoggerRepository repository in repositories)
            {
                repository.Threshold = level;
                Hierarchy hier = (Hierarchy)repository;
                ILogger[] loggers = hier.GetCurrentLoggers();
                foreach (ILogger logger in loggers)
                {
                    ((Logger)logger).Level = level;
                }
            }

            //Configure the root logger.
            Hierarchy h = (Hierarchy)LogManager.GetRepository();
            Logger rootLogger = h.Root;
            rootLogger.Level = level;

            return level;
        }

        public static void LoggerLevel33(Level level)
        {
            log.Info("LoggerLevel " + level.Value);
            foreach (var r in LogManager.GetAllRepositories())
            {
                log.Info("Hierarchy " + ((log4net.Repository.Hierarchy.Hierarchy)r).Name +" "+ ((log4net.Repository.Hierarchy.Hierarchy)r).Threshold.Name + " change to "+ level.Value);
                ((log4net.Repository.Hierarchy.Hierarchy)r).Root.Level = level;
                ((log4net.Repository.Hierarchy.Hierarchy)r).RaiseConfigurationChanged(EventArgs.Empty);
            }
        }
        public static void SetupLogger()
        {
            string xml =
  @"<log4net>
  
  <root>
    <level value='INFO' />
    <appender-ref ref='console' />
  </root>
  
  <appender name='console' type='log4net.Appender.ConsoleAppender'>
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date %level %logger - %message%newline' />
    </layout>
  </appender>
  
  <appender name='m0' type='log4net.Appender.RollingFileAppender'>
    <file value='logs/GoldenBet_Main.log' />
    <encoding value='utf-8' />
    <appendToFile value='true' />
    <rollingStyle value='Size' />
    <maxSizeRollBackups value='5' />
    <maximumFileSize value='10MB' />
    <staticLogFileName value='true' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date [%thread] %level %logger - %message%newline' />
    </layout>
  </appender>
  <appender name='b1' type='log4net.Appender.RollingFileAppender'>
    <file value='logs/GoldenBet_Bookie1.log' />
    <encoding value='utf-8' />
    <appendToFile value='true' />
    <rollingStyle value='Size' />
    <maxSizeRollBackups value='5' />
    <maximumFileSize value='10MB' />
    <staticLogFileName value='true' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date [%thread] %level %logger - %message%newline' />
    </layout>
  </appender>
  <appender name='b2' type='log4net.Appender.RollingFileAppender'>
    <file value='logs/GoldenBet_Bookie2.log' />
    <encoding value='utf-8' />
    <appendToFile value='true' />
    <rollingStyle value='Size' />
    <maxSizeRollBackups value='5' />
    <maximumFileSize value='10MB' />
    <staticLogFileName value='true' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date [%thread] %level %logger - %message%newline' />
    </layout>
  </appender>
  <appender name='t1' type='log4net.Appender.RollingFileAppender'>
    <file value='logs/GoldenBet_Ticket.log' />
    <encoding value='utf-8' />
    <appendToFile value='true' />
    <rollingStyle value='Size' />
    <maxSizeRollBackups value='5' />
    <maximumFileSize value='10MB' />
    <staticLogFileName value='true' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date [%thread] %level %logger - %message%newline' />
    </layout>
  </appender>
  <appender name='t2' type='log4net.Appender.RollingFileAppender'>
    <file value='logs/GoldenBet_BetPlace.log' />
    <encoding value='utf-8' />
    <appendToFile value='true' />
    <rollingStyle value='Size' />
    <maxSizeRollBackups value='5' />
    <maximumFileSize value='10MB' />
    <staticLogFileName value='true' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date [%thread] %level %logger - %message%newline' />
    </layout>
  </appender>
  <appender name='t3' type='log4net.Appender.RollingFileAppender'>
    <file value='logs/GoldenBet_BetResult.log' />
    <encoding value='utf-8' />
    <appendToFile value='true' />
    <rollingStyle value='Size' />
    <maxSizeRollBackups value='5' />
    <maximumFileSize value='10MB' />
    <staticLogFileName value='true' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date [%thread] %level %logger - %message%newline' />
    </layout>
  </appender>

  <appender name='t4' type='log4net.Appender.RollingFileAppender'>
    <file value='logs/GoldenBet_contra.log' />
    <encoding value='utf-8' />
    <appendToFile value='true' />
    <rollingStyle value='Size' />
    <maxSizeRollBackups value='5' />
    <maximumFileSize value='10MB' />
    <staticLogFileName value='true' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date [%thread] %level %logger - %message%newline' />
    </layout>
  </appender>

  <appender name='t5' type='log4net.Appender.RollingFileAppender'>
    <file value='logs/GoldenBet_account.log' />
    <encoding value='utf-8' />
    <appendToFile value='true' />
    <rollingStyle value='Size' />
    <maxSizeRollBackups value='5' />
    <maximumFileSize value='10MB' />
    <staticLogFileName value='true' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date [%thread] %level %logger - %message%newline' />
    </layout>
  </appender>

<appender name='t6' type='log4net.Appender.RollingFileAppender'>
    <file value='logs/GoldenBet_config.log' />
    <encoding value='utf-8' />
    <appendToFile value='true' />
    <rollingStyle value='Size' />
    <maxSizeRollBackups value='5' />
    <maximumFileSize value='10MB' />
    <staticLogFileName value='true' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date [%thread] %level %logger - %message%newline' />
    </layout>
  </appender>

<appender name='t7' type='log4net.Appender.RollingFileAppender'>
    <file value='logs/GoldenBet_betcontrol.log' />
    <encoding value='utf-8' />
    <appendToFile value='true' />
    <rollingStyle value='Size' />
    <maxSizeRollBackups value='5' />
    <maximumFileSize value='5MB' />
    <staticLogFileName value='true' />
    <layout type='log4net.Layout.PatternLayout'>
      <conversionPattern value='%date [%thread] %level %logger - %message%newline' />
    </layout>
  </appender>
  <logger name='b2'>
    <level value='INFO' />
    <appender-ref ref='b2' />
    <appender-ref ref='m0' />
  </logger>

  <logger name='b1'>
    <level value='INFO' />
    <appender-ref ref='b1' />
    <appender-ref ref='m0' />
  </logger>

  <logger name='t1'>
    <level value='INFO' />
    <appender-ref ref='t1' />
    <appender-ref ref='m0' />
  </logger>

  <logger name='t2'>
    <level value='INFO' />
    <appender-ref ref='t2' />
    <appender-ref ref='t1' />
    <appender-ref ref='m0' />
  </logger>

  <logger name='t3'>
    <level value='INFO' />
    <appender-ref ref='t3' />
    <appender-ref ref='t2' />
    <appender-ref ref='t1' />    
    <appender-ref ref='m0' />
  </logger>

  <logger name='t4'>
    <level value='INFO' />
    <appender-ref ref='t4' />
    <appender-ref ref='m0' />
  </logger>

  <logger name='t5'>
    <level value='INFO' />
    <appender-ref ref='t5' />
    <appender-ref ref='m0' />
  </logger>

<logger name='t6'>
    <level value='INFO' />
    <appender-ref ref='t6' />
    <appender-ref ref='m0' />
  </logger>

<logger name='t7'>
    <level value='INFO' />
    <appender-ref ref='t7' />
  </logger>

  <logger name='m0'>
    <level value='INFO' />
    <appender-ref ref='m0' />
  </logger>
</log4net>";

            //
            // Use XmlDocument to load the xml string then pass the DocumentElement to
            // XmlConfigurator.Configure.
            //
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            log4net.Config.XmlConfigurator.Configure(doc.DocumentElement);
        }

        
        public static void SetupLogger2()
        {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
            hierarchy.Root.RemoveAllAppenders();                            // Clear all previously added repositories.
            hierarchy.Root.Level = Level.Info;

            //Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

            PatternLayout patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "%date [%thread] %level %logger - %message%newline";
            patternLayout.ActivateOptions();

            
            RollingFileAppender roller_m0 = new RollingFileAppender();
            roller_m0.Name = "m0";
            roller_m0.AppendToFile = true;
            roller_m0.Encoding = Encoding.UTF8;
            roller_m0.File = @"logs/GoldenBet_Main.log";
            roller_m0.Layout = patternLayout;
            roller_m0.MaxSizeRollBackups = 10;
            roller_m0.MaximumFileSize = "25MB";
            roller_m0.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller_m0.StaticLogFileName = true;
            roller_m0.ActivateOptions();

            RollingFileAppender roller_b1 = new RollingFileAppender();
            roller_b1.Name = "b1";
            roller_b1.AppendToFile = true;
            roller_b1.Encoding = Encoding.UTF8;
            roller_b1.File = @"logs/GoldenBet_Bookie1.log";
            roller_b1.Layout = patternLayout;
            roller_b1.MaxSizeRollBackups = 10;
            roller_b1.MaximumFileSize = "25MB";
            roller_b1.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller_b1.StaticLogFileName = true;
            roller_b1.ActivateOptions();

            RollingFileAppender roller_b2 = new RollingFileAppender();
            roller_b2.Name = "b2";
            roller_b2.AppendToFile = true;
            roller_b2.Encoding = Encoding.UTF8;
            roller_b2.File = @"logs/GoldenBet_Bookie2.log";
            roller_b2.Layout = patternLayout;
            roller_b2.MaxSizeRollBackups = 10;
            roller_b2.MaximumFileSize = "25MB";
            roller_b2.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller_b2.StaticLogFileName = true;
            roller_b2.ActivateOptions();

            RollingFileAppender roller_t1 = new RollingFileAppender();
            roller_t1.Name = "t1";
            roller_t1.AppendToFile = true;
            roller_t1.Encoding = Encoding.UTF8;
            roller_t1.File = @"logs/GoldenBet_Ticket.log";
            roller_t1.Layout = patternLayout;
            roller_t1.MaxSizeRollBackups = 10;
            roller_t1.MaximumFileSize = "25MB";
            roller_t1.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller_t1.StaticLogFileName = true;
            roller_t1.ActivateOptions();

            RollingFileAppender roller_t2 = new RollingFileAppender();
            roller_t2.Name = "t2";
            roller_t2.AppendToFile = true;
            roller_t2.Encoding = Encoding.UTF8;
            roller_t2.File = @"logs/GoldenBet_BetPlace.log";
            roller_t2.Layout = patternLayout;
            roller_t2.MaxSizeRollBackups = 10;
            roller_t2.MaximumFileSize = "25MB";
            roller_t2.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller_t2.StaticLogFileName = true;
            roller_t2.ActivateOptions();


            RollingFileAppender roller_t3 = new RollingFileAppender();
            roller_t3.Name = "t3";
            roller_t3.AppendToFile = true;
            roller_t3.Encoding = Encoding.UTF8;
            roller_t3.File = @"logs/GoldenBet_BetResult.log";
            roller_t3.Layout = patternLayout;
            roller_t3.MaxSizeRollBackups = 10;
            roller_t3.MaximumFileSize = "25MB";
            roller_t3.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller_t3.StaticLogFileName = true;
            roller_t3.ActivateOptions();

            RollingFileAppender roller_t4 = new RollingFileAppender();
            roller_t4.Name = "t4";
            roller_t4.AppendToFile = true;
            roller_t4.Encoding = Encoding.UTF8;
            roller_t4.File = @"logs/GoldenBet_Contra.log";
            roller_t4.Layout = patternLayout;
            roller_t4.MaxSizeRollBackups = 10;
            roller_t4.MaximumFileSize = "25MB";
            roller_t4.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller_t4.StaticLogFileName = true;
            roller_t4.ActivateOptions();

            Logger loggerm0 = hierarchy.LoggerFactory.CreateLogger(
                LogManager.GetRepository(), "m0");
            loggerm0.AddAppender(roller_m0);
            loggerm0.Level = Level.Info;


            Logger loggerb1 = hierarchy.LoggerFactory.CreateLogger(
                LogManager.GetRepository(), "b1");
            loggerb1.AddAppender(roller_m0);
            loggerb1.AddAppender(roller_b1);
            loggerb1.Level = Level.Info;

            Logger loggerb2 = hierarchy.LoggerFactory.CreateLogger(
                LogManager.GetRepository(), "b2");
            loggerb2.AddAppender(roller_m0);
            loggerb2.AddAppender(roller_b2);
            loggerb2.Level = Level.Info;

            Logger loggert1 = hierarchy.LoggerFactory.CreateLogger(
                LogManager.GetRepository(), "t1");
            loggert1.AddAppender(roller_m0);
            loggert1.AddAppender(roller_t1);
            loggert1.Level = Level.Info;

            Logger loggert2 = hierarchy.LoggerFactory.CreateLogger(
                LogManager.GetRepository(), "t2");
            loggert2.AddAppender(roller_m0);
            loggert2.AddAppender(roller_t2);
            loggert2.Level = Level.Info;

            Logger loggert3 = hierarchy.LoggerFactory.CreateLogger(
                LogManager.GetRepository(), "t3");
            loggert3.AddAppender(roller_m0);
            loggert3.AddAppender(roller_t3);
            loggert3.Level = Level.Info;


            Logger loggert4 = hierarchy.LoggerFactory.CreateLogger(
                LogManager.GetRepository(), "t4");
            loggert4.Level = Level.Info;
            loggert4.AddAppender(roller_m0);
            loggert4.AddAppender(roller_t4);
            

            //hierarchy.Root.AddAppender(RFA);
            hierarchy.Root.Level = Level.Info;
            hierarchy.Configured = true;

            BasicConfigurator.Configure(hierarchy);
        }
        
        public static long ToUnixTimeMilliseconds()
        {
            return ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
        }
        public static long getCurrentTimeStame()
        {
            long epochTime = (DateTime.UtcNow.Ticks - 621355968000000000) / 10000;
            return epochTime;
        }
        public static string ToString(Dictionary<string, string> dic)
        {
            if (dic == null)
            {
                return "NULL";
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, string> kvp in dic)
                {
                    string key = kvp.Key;
                    string value = kvp.Value;
                    sb.Append(key + "=" + value + "\n");
                }
                return sb.ToString();
            }
        }
        public static string ReadFile(string filename)
        {
            return File.ReadAllText(filename, System.Text.Encoding.UTF8);
        }



        public static string GetUUID_u()
        {
            Guid g = Guid.NewGuid();
            return g.ToString().Substring(g.ToString().Length - 4);
        }

        

        public static int GetLong(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                throw new ArgumentNullException(s, "String Cannot Be Null Or Empty");
            }

            if (string.IsNullOrEmpty(t))
            {
                throw new ArgumentNullException(t, "String Cannot Be Null Or Empty");
            }

            int n = s.Length; // length of s
            int m = t.Length; // length of t

            if (n == 0)
            {
                return m;
            }

            if (m == 0)
            {
                return n;
            }

            int[] p = new int[n + 1]; //'previous' cost array, horizontally
            int[] d = new int[n + 1]; // cost array, horizontally

            // indexes into strings s and t
            int i; // iterates through s
            int j; // iterates through t

            for (i = 0; i <= n; i++)
            {
                p[i] = i;
            }

            for (j = 1; j <= m; j++)
            {
                char tJ = t[j - 1]; // jth character of t
                d[0] = j;

                for (i = 1; i <= n; i++)
                {
                    int cost = s[i - 1] == tJ ? 0 : 1; // cost
                                                       // minimum of cell to the left+1, to the top+1, diagonally left and up +cost                
                    d[i] = Math.Min(Math.Min(d[i - 1] + 1, p[i] + 1), p[i - 1] + cost);
                }

                // copy current distance counts to 'previous row' distance counts
                int[] dPlaceholder = p; //placeholder to assist in swapping p and d
                p = d;
                d = dPlaceholder;
            }

            // our last action in the above loop was to switch d and p, so p now 
            // actually has the most recent cost counts
            return p[n];
        }

        public static string GenerateIP(string ipSubnet, long countIp)
        {

            //103.252.0.0/22         
            // Get_octec3_range = 4;
            Random rnd = new Random();
            List<String> listIp = new List<string>();
            string[] ip_octec = ipSubnet.Split('.');
            int Get_octec3_range = 4;
            for (int i = 0; i < Get_octec3_range; i++)
            {

                int new_octec3 = Convert.ToInt32(ip_octec[2]) + i;
                if(new_octec3 > 255)
                {
                    new_octec3 = Convert.ToInt32(ip_octec[2]) + i - 5;
                }
                for (int j = 0; j < countIp; j++)
                {
                    int ip4 = rnd.Next(3 + j, 250);
                    string newIp = ip_octec[0] + "." + ip_octec[1] + "." + new_octec3 + "." + ip4;
                    listIp.Add(newIp);
                }
            }
            foreach (string ip in listIp)
            {
                //Console.WriteLine(ip);
            }

            int randomIndex = rnd.Next(0, listIp.Count - 2);
            return listIp[randomIndex];
        }
        public static string Base64Encode(string plainText)
        {
            if (plainText.Length == 0)
            {
                return plainText;
            }
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public static string Base64Decode(string base64EncodedData)
        {
            if (base64EncodedData.Length == 0)
            {
                return base64EncodedData;
            }
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
}
