//#define TEST

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using gmapi.General;
using gmapi.Custom;
using gmapi.TCP;
using System.IO;

namespace SEPSensor
{
    public class Sensor
    {
        /// <summary>
        /// Zielmachine (z.B. QBB25.bb.int)
        /// </summary>
        private String _TargetMachine;

        /// <summary>
        /// GMApi Einstellungen
        /// </summary>
        private SettingsController _settingsController;
        private LogfileController _logfileController;

        /// <summary>
        /// PRTG Error Codes als Enumaration
        /// </summary>
        public enum ERROR_CODE { OK, WARNING, SYSTEM_ERROR, PROTOCOL_ERROR, CONTENT_ERROR }
        private ERROR_CODE CURRENT_ERROR_STATE = ERROR_CODE.OK;

        /// <summary>
        /// Liste mit Job-Ergebnissen
        /// </summary>
        private List<JobResult> _jobResultList;

        /// <summary>
        /// Konstruktor
        /// </summary>
        /// <param name="TargetMachine">SEP Backup Server zum monitoren</param>
        public Sensor(String TargetMachine)
        {
            this._TargetMachine = TargetMachine;
            this._jobResultList = new List<JobResult>();
            this._settingsController = SettingsController.Instance; //Singleton!
            this._settingsController.RefreshDirectoryStructure();
            this._logfileController = new LogfileController(this._settingsController);
        }

        /// <summary>
        /// Auswertungs-Durchlauf
        /// </summary>
        internal void Run()
        {
            int jobErrors = 0;              //Fehler in den Job-Logs
            bool allSuccessful = true;      //Flag ob keine Fehler aufgetreten sind

            //Prüfe Connectivität zum Zielserver
            LocalNetworkController localNC = new LocalNetworkController(this._settingsController);
            if (localNC.PingAHost(_TargetMachine.Replace("\\", "")))
            {
                //Prüfe SEP State Log
                RunCheckStates();

                //Prüfe SEP Error Log
                int errorCount = RunCheckErrors();

                //PRTG XML Ausgabe generieren
                WritePRTGHeader();
                foreach (JobResult jr in this._jobResultList)
                {
                    if (!jr.Successful)
                    {
                        allSuccessful = false;
                        jobErrors++;
                    }
                    int successful = jr.Successful ? 1 : 0;
                    WriterPRTGChannel(jr.JobName, successful);
                }

                //Sofern Fehler gefunden wurden
                if (errorCount > 0 || !allSuccessful || jobErrors > 0)
                {
                    WritePRTGText("Job-Errors found: " + jobErrors.ToString() + " SEP-Errors found: " + errorCount.ToString());
                    CURRENT_ERROR_STATE = ERROR_CODE.WARNING;
                }

                WritePRTGFooter();
            }
#if !TEST
            //Beenden mit PRTG Exit-Code
            Environment.Exit((int)CURRENT_ERROR_STATE);
#else
            //Sofern Test, dann zeige ausgabe
            Console.ReadLine();
#endif
        }

        /// <summary>
        /// Zähle Fehler im Error-State Log
        /// </summary>
        /// <param name="needle">Suchbegriff</param>
        /// <param name="haystack">Text zum durchsuchen</param>
        /// <returns>Ganzzahl mit Vorkommen</returns>
        private int countOccurences(string needle, string haystack)
        {
                return (haystack.Length - haystack.Replace(needle,"").Length) / needle.Length;
        }

        /// <summary>
        /// Snippet zum erzeugen des PRTG XML Headers
        /// </summary>
        public void WritePRTGHeader()
        {
            Console.Write("<?xml version=`\"1.0`\" encoding=`\"UTF-8`\" ?><prtg>");
        }

        /// <summary>
        /// Snippet zum erzeugen des PRTG Footers
        /// </summary>
        public void WritePRTGFooter()
        {
            Console.Write("</prtg>");
        }


        /// <summary>
        /// Erzeuge PRTG Ergebnis-Eintrag
        /// </summary>
        /// <param name="Name">Channelname</param>
        /// <param name="Value">Ergebnis</param>
        public void WriterPRTGChannel(String Name, int Value)
        {
            string result = "";
            result += "<result>";
            result += "<channel>"+Name + "</channel>";
            result += "<value>" + Value + "</value>";
            result += "<ValueLookup>prtg.standardlookups.offon.stateonok</ValueLookup>";
            result += "</result>";
            Console.Write(result);
        }

        /// <summary>
        /// Kommentar zum PRTG Ergebnis
        /// </summary>
        /// <param name="text"></param>
        public void WritePRTGText(String text)
        {
            Console.Write("<text>" + text + "</text>");
        }

        /// <summary>
        /// Überprüfe State Log von SEP
        /// </summary>
        private void RunCheckStates()
        {
            //Starte SEP CLI
            ProcessStartInfo psiCheckStates = new ProcessStartInfo();
            psiCheckStates.FileName = this._TargetMachine + "\\c$\\Program Files\\SEPsesam\\bin\\sesam\\sm_cmd.exe";
            psiCheckStates.Arguments = "show log state -B yesterday";   //Die Parameter für State Log
            psiCheckStates.UseShellExecute = false;
            psiCheckStates.RedirectStandardOutput = true;               //Verwende Stdout

            Process proc2 = new Process();
            proc2.StartInfo = psiCheckStates;
            proc2.Start();

            this._logfileController.WriteLogEntry("Running Process: " + psiCheckStates.FileName + " " + psiCheckStates.Arguments, false, this.GetType().ToString());

            //Lese Konsolenfenster aus
            string outputStates = proc2.StandardOutput.ReadToEnd();

            //Versuche Ausgabe zu cachen
            if (WriteCache("States", outputStates))
            {
                //Lese Cache Datei aus
                DirectorySearcher dirSearch = new DirectorySearcher();
                StreamReader reader = new StreamReader(dirSearch.getDirectoryPathByKey("cache", this._settingsController.getDirectoryIndex()) + "\\States.cache");
                while (reader.Peek() >= 0)
                {
                    string ln = reader.ReadLine();
                    
                    //Wenn 0 = Erfolgreich, X = Fehlerhaft (SEP Sesam Konvention)
                    if (ln.StartsWith("0") || ln.StartsWith("X"))
                    {
                        //Entferne unnötige Tabs im Text
                        string simpleSpaces = ln.Replace("\t", "");
                        string[] myLn = simpleSpaces.Split(' ');

                        //Wenn keine Fehlformatierte Ausgabe vorliegt...
                        if (myLn.Length > 1)
                        {
                            try
                            {
                                //Versuche JobResult anzulegen
                                this._jobResultList.Add(new JobResult()
                                {
                                    JobName = myLn[1].ToString(),
                                    Successful = (myLn[0] == "0") ? true : false
                                });

                                this._logfileController.WriteLogEntry("Found Job: " + myLn[0] + " with State: " + myLn[1], false, this.GetType().ToString());
                            }
                            catch (Exception ex)
                            {
                                //Breche mit Fehlermeldung ab
                                WritePRTGHeader();
                                WriterPRTGChannel("Backup Status", 0);
                                WritePRTGText("Code error in Sensor");
                                WritePRTGFooter();
                                this._logfileController.WriteLogEntry(ex, false, this.GetType().ToString());
                                CURRENT_ERROR_STATE = ERROR_CODE.SYSTEM_ERROR;
                            }

                        }
                    }
                }
                reader.Close();
            }
            else
            {
                WritePRTGHeader();
                WriterPRTGChannel("Backup Status", 0);
                WritePRTGText("Code error in Sensor");
                WritePRTGFooter();
                CURRENT_ERROR_STATE = ERROR_CODE.SYSTEM_ERROR;
            }
        }

        /// <summary>
        /// Überprüfe Error Log von SEP
        /// </summary>
        /// <returns></returns>
        private int RunCheckErrors()
        {
            //SEP Cli Starten
            ProcessStartInfo psiCheckError = new ProcessStartInfo();
            psiCheckError.FileName = this._TargetMachine + "\\c$\\Program Files\\SEPsesam\\bin\\sesam\\sm_cmd.exe";
            psiCheckError.Arguments = "show log error -B yesterday";

            psiCheckError.UseShellExecute = false;
            psiCheckError.RedirectStandardOutput = true;

            this._logfileController.WriteLogEntry("Running Process: " + psiCheckError.FileName + " " + psiCheckError.Arguments, false, this.GetType().ToString());

            Process proc = new Process();
            proc.StartInfo = psiCheckError;
            proc.Start();

            //Prüfe Konsolenfenster auf stdout
            string output = proc.StandardOutput.ReadToEnd();

            int countsError = 0;

            //Zähle die vorkommen im Text auf error und fehlerhaft
            if (output.ToLower().Contains("error") || output.ToLower().Contains("fehlerhaft"))
            {
                countsError = countOccurences("Error", output);
                countsError += countOccurences("fehlerhaft", output);
            }
            this._logfileController.WriteLogEntry("Errors found: " + countsError.ToString(), false, this.GetType().ToString());
            return countsError;
        }

        /// <summary>
        /// Erzeuge Cache Datei
        /// </summary>
        /// <param name="CacheName">Dateiname (ohne Endung)</param>
        /// <param name="CacheInput">Text zum cachen</param>
        /// <returns></returns>
        private bool WriteCache(String CacheName, String CacheInput)
        {
            try
            {
                DirectorySearcher dirSearch = new DirectorySearcher();
                StreamWriter CacheStates = File.CreateText(dirSearch.getDirectoryPathByKey("cache", 
                    this._settingsController.getDirectoryIndex()) + "\\" + CacheName + ".cache");
                CacheStates.Write(CacheInput);
                CacheStates.Close();
                return true;
            }
            catch (Exception ex)
            {
                this._logfileController.WriteLogEntry(ex, false, this.GetType().ToString());
                return false;
            }
        }
    }
}
