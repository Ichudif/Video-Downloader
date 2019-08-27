using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoDownloader
{
    public static class Log
    {
        public static ListBox LogWindow;
        private static bool initialized = false;

        public static void Initialize(ListBox lw)
        {
            LogWindow = lw;
            initialized = true;
        }
        
        public static bool addLogEntry(string message)
        {
            if (initialized)
            {
                try
                {
                    LogWindow.Invoke(new Action(() => LogWindow.Items.Add("[" + DateTime.Now.ToString("hh:mm:ss") + "] " + message)));
                    LogWindow.Invoke(new Action(() => LogWindow.TopIndex = LogWindow.Items.Count - 1));
                }
                catch { }
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
