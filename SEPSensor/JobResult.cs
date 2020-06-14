using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SEPSensor
{
    public class JobResult
    {
        /// <summary>
        /// Jobname von SEP Sicherung
        /// </summary>
        public String JobName { get; set; }

        /// <summary>
        /// Erfolgreich Ja/Nein
        /// </summary>
        public bool Successful { get; set; }
    }
}
