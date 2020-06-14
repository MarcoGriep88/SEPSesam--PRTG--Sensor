//#define TEST

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SEPSensor
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                #region Error-Message-NoServer
                string result = "";
                result += "<?xml version=`\"1.0`\" encoding=`\"UTF-8`\" ?>";
                result += "<prtg>";
                result += "<result>";
                result += "<channel>Backup Status</channel>";
                result += "<value>0</value>";
                result += "<ValueLookup>prtg.standardlookups.offon.stateonok</ValueLookup>";
                result += "</result>";
                result += "<text>No Server Information. Check Sensor Paramters</text>";
                result += "</prtg>";

                Console.Write(result);

#if !TEST
                Environment.Exit(4);
#else 
                Console.ReadLine();
#endif
                #endregion
            }
            else
            {

                if (args[0].Length > 0)
                {
                    Sensor sensor = new Sensor("\\\\" + args[0]);
                    sensor.Run();
                }
                else
                {
                    #region Error-Message-NoServer-Adress
                    string result = "";
                    result += "<?xml version=`\"1.0`\" encoding=`\"UTF-8`\" ?>";
                    result += "<prtg>";

                    result += "<result>";
                    result += "<channel>Backup Status</channel>";
                    result += "<value>0</value>";
                    result += "<ValueLookup>prtg.standardlookups.offon.stateonok</ValueLookup>";
                    result += "</result>";
                    result += "<text>No Server Information. Check Sensor Paramters</text>";
                    result += "</prtg>";

                    Console.Write(result);

#if !TEST
                    Environment.Exit(4);
#else 
                Console.ReadLine();
#endif
                    #endregion
                }
            }
        }
    }
}
