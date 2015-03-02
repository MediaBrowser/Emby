using System.Diagnostics;

namespace MediaBrowser.Controller.Diagnostics
{
    /// <summary>
    /// Interface IProcessManager
    /// </summary>
    public interface IProcessManager
    {
        /// <summary>
        /// Idles the process.
        /// </summary>
        /// <param name="process">The process.</param>
        void IdleProcess(Process process);

        /// <summary>
        /// Unidles the process.
        /// </summary>
        /// <param name="process">The process.</param>
        void UnidleProcess(Process process);
    }
}
