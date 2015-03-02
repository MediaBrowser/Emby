using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Diagnostics;

namespace MediaBrowser.Server.Implementations.Diagnostics
{
    public class ProcessManager : IProcessManager
    {
        public void IdleProcess(Process process)
        {
            process.PriorityClass = ProcessPriorityClass.Idle;
        }

        public void UnidleProcess(Process process)
        {
            process.PriorityClass = ProcessPriorityClass.Normal;            
        }
    }
}
