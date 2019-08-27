using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;

namespace VideoDownloader
{
    class DownloadManager
    {
        private bool initialized = false;
        private ProgressBar progressBar1;
        private Label label2;
        private int count = 1;
        private int max = 0;
        private Dictionary<WebClient, string> downloading = new Dictionary<WebClient, string>();
        private Dictionary<string, bool> Linklist = new Dictionary<string, bool>();
        private string prefix = "";
        private string _Path = "";
        private bool stopall = false;

        public event EventHandler DownloadCompleted;


        public void initialize(ProgressBar pb, Label lb)
        {
            progressBar1 = pb;
            label2 = lb;
            initialized = true;
        }

        public void StopDownload()
        {
            stopall = true;
            while (downloading.Count > 0)
                Application.DoEvents();
        }

        public void manageDownload(Dictionary<string, bool> LinkL, string pref, string pth)
        {
            stopall = false;
            if (!initialized)
                return;
            if (LinkL.Count == 0)
            {
                DownloadCompleted(null, null);
                return;
            }


            _Path = pth;
            prefix = pref;
            Linklist = LinkL;
            max = Linklist.Count(item => item.Value == true);

            progressBar1.Invoke(new Action(() =>
            {
                progressBar1.Maximum = max;
            }));

            System.Timers.Timer t = new System.Timers.Timer();
            t.Interval = 100;
            t.Elapsed += T_Elapsed;
            t.Start();
        }

        private void T_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (downloading.Count < 8)
            {
                string link = Linklist.Where(item => item.Value == true).First().Key;
                Linklist[link] = false;

                WebClient wbc = new WebClient();
                string path = "";
                try
                {
                    Log.addLogEntry("Starting download: " + link.Split(new string[] { "-chunk-" }, StringSplitOptions.None)[1]);
                    path = Path.Combine(_Path, link.Split(new string[] { "-chunk-" }, StringSplitOptions.None)[1]);
                    wbc.DownloadFileAsync(new Uri(prefix + link), path);
                    wbc.DownloadFileCompleted += Wbc_DownloadFileCompleted;

                    downloading.Add(wbc, path);
                }
                catch
                {
                    wbc.CancelAsync();
                    Thread.Sleep(10);
                    File.Delete(path);
                    Log.addLogEntry("Failed to download Part " + path.Split('\\').Last().Split('.')[0]);
                }
            }

            if (stopall)
            {
                foreach (KeyValuePair<WebClient, string> item in downloading)
                {
                    item.Key.CancelAsync();
                    Thread.Sleep(10);
                    File.Delete(item.Value);
                }

                downloading = new Dictionary<WebClient, string>();
            }
        }

        private void Wbc_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (stopall)
            {
                return;
            }

            progressBar1.Invoke(new Action(() =>
            {
                progressBar1.Value++;
                label2.Text = count++ + " / " + max;
            }));

            downloading.Remove(sender as WebClient);

            if (downloading.Count == 0)
            {
                DownloadCompleted(Linklist, null);
                Log.addLogEntry("Downloading Finished");
            }
        }
    }
}
