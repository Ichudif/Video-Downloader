using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Diagnostics;

namespace VideoDownloader
{
    public partial class Form1 : Form
    {
        string result = "";
        static Form MyForm;
        private Stopwatch sw = new Stopwatch();
        private DownloadManager dm = new DownloadManager();
        public string DownloadFolder = "";

        public Form1()
        {
            InitializeComponent();
            MyForm = this;

            Log.Initialize(listBox1);

            DownloadFolder = Path.Combine(Application.StartupPath, "Download");

            //Do all needed Folders exist?
            if (!Directory.Exists(DownloadFolder))
            {
                Directory.CreateDirectory(DownloadFolder);     //Create Download Folders
            }

            dm.initialize(progressBar1, label2);

            LinkGetter.listBox1 = listBox1;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(AcceptAllCertifications);
            System.Net.ServicePointManager.CheckCertificateRevocationList = false;

            LinkGetter.gecko = webBrowser1;
            webBrowser1.Navigate("https://hdfilme.cc");
            if (webBrowser1.DocumentText.Contains("Checking your Browser"))
            {
                System.Timers.Timer browsercheck = new System.Timers.Timer();
                browsercheck.Interval = 50;
                browsercheck.Elapsed += Browsercheck_Elapsed;
                browsercheck.Start();
                Log.addLogEntry("Cloudflare check");
            }
        }

        private static bool AcceptAllCertifications(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certification, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private void Browsercheck_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (webBrowser1.DocumentText.Contains("Checking your Browser"))
            {
                button1.Enabled = false;
            }
            else if (webBrowser1.DocumentText.Length < 100)
            {
                Log.addLogEntry("Cloudflare check not passed ... exiting");
                Application.Exit();
            }
            else
            {
                button1.Enabled = true;
                Log.addLogEntry("Cloudflare check passed ... continuing");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            sw.Start();
            Log.addLogEntry("Starting");
            LinkGetter.videolink = textBox1.Text;
            textBox1.Enabled = false;
            button1.Enabled = false;

            //check if there is still an old download, which has been cancelled
            if (File.Exists(Path.Combine(DownloadFolder, "DownloadInfo.Info")))
            {
                //there is one
                if (File.ReadAllLines(Path.Combine(DownloadFolder, "DownloadInfo.Info"))[0].Split(':')[2] == textBox1.Text.Split(':')[1])
                {
                    new Thread(() =>
                    {
                        Merge(true, null);
                    }).Start();
                    return;
                }
            }

            Log.addLogEntry("Clearing Workspace");
            Directory.GetFiles(DownloadFolder).ToList().Where(item => !item.Contains("ffmpeg.exe")).ToList().ForEach(item => File.Delete(item));
            new Thread(StartDownload).Start();
        }

        private void StartDownload()
        {
            //LinkGetter.Name = DateTime.Now.ToString();
            Task<string> getlink = LinkGetter.GetLinkAsync(textBox1.Text);
            getlink.Wait();
            result = getlink.Result;
            if (result == null)
            {
                Thread.Sleep(1500);
                result = getlink.Result;
            }

            if (result == null)
            {
                MessageBox.Show("Could not load Webpage");
                return;
            }

            List<VideoResolution> Resolutions = new List<VideoResolution>();
            WebClient wc = new WebClient();
            wc.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:68.0) Gecko/20100101 Firefox/68.0");
            string file = wc.DownloadString(result);

            string[] lines = file.Split('\n');

            for (int i = 1; i < lines.Length - 1; i += 2)
            {
                Resolutions.Add(new VideoResolution(lines[i].Split(new string[] { "RESOLUTION=" }, StringSplitOptions.None)[1], lines[i + 1]));
            }

            Resolutions = Resolutions.OrderByDescending(item => int.Parse(item.Resolution.Split('x')[0])).ThenBy(item => int.Parse(item.Resolution.Split('x')[1])).ToList();

            Log.addLogEntry("Downloading in Resolution " + Resolutions.First().Resolution);

            string prefix = result.Split(new string[] { ".org" }, StringSplitOptions.None)[0] + ".org";
            string complete = prefix + Resolutions.First().Link;

            string partlist = wc.DownloadString(complete);
            string[] linessplitted = partlist.Split('\n');

            Dictionary<string, bool> Linklist = new Dictionary<string, bool>();

            for (int i = 0; i < linessplitted.Length; i++)
            {
                if (!linessplitted[i].StartsWith("#"))
                {
                    string singleline = linessplitted[i].Split('&')[0];
                    if (singleline != "" && !Linklist.Any(item => item.Key.Split(new string[] { "-chunk-" }, StringSplitOptions.None)[1].Split('.')[0] == singleline.Split(new string[] { "-chunk-" }, StringSplitOptions.None)[1].Split('.')[0]))
                    {
                        Linklist.Add(singleline, true);
                    }
                }
            }

            using (StreamWriter sw = new StreamWriter(Path.Combine(DownloadFolder, "DownloadInfo.Info")))
            {
                sw.WriteLine("#link:" + LinkGetter.videolink);
                sw.WriteLine("#name:" + LinkGetter.Name);
                sw.WriteLine("#prefix:" + prefix);
                Linklist.Keys.ToList().ForEach(item => sw.WriteLine(item));
            }

            using (StreamWriter sw = new StreamWriter(Path.Combine(DownloadFolder, "files.txt")))
            {
                Linklist.Keys.ToList().ForEach(item => sw.WriteLine("file '" + item.Split(new string[] { "-chunk-" }, StringSplitOptions.None)[1] + "'"));
            }

            dm.manageDownload(Linklist, prefix, DownloadFolder);
            dm.DownloadCompleted += JustContinue;
        }

        private void JustContinue(object sender, EventArgs e)
        {
            Merge(false, sender as Dictionary<string, bool>);
        }

        public void Merge(bool resume, Dictionary<string, bool> Linklist)
        {
            if (resume)
            {
                //we have to resuma a already started download, so all the needed files are still there
                Log.addLogEntry("Resuming Download");

                Linklist = new Dictionary<string, bool>();
                string[] data = File.ReadAllLines(Path.Combine(DownloadFolder, "DownloadInfo.Info"));
                LinkGetter.Name = data[1].Substring(data[1].IndexOf(':')).Replace(":", "");
                for (int i = 3; i < data.Length; i++)       //reading the downloadinfo file
                {
                    Linklist.Add(data[i], !File.Exists(Path.Combine(DownloadFolder, data[i].Split(new string[] { "-chunk-" }, StringSplitOptions.None)[1])));      //saving all the small parts
                }

                if (!File.Exists(Path.Combine(DownloadFolder, "files.txt")))
                    using (StreamWriter sw = new StreamWriter(Path.Combine(DownloadFolder, "files.txt")))
                    {
                        Linklist.Keys.ToList().ForEach(item => sw.WriteLine("file '" + item.Split(new string[] { "-chunk-" }, StringSplitOptions.None)[1] + "'"));
                    }

                //start downloading again
                dm.DownloadCompleted += JustContinue;
                dm.manageDownload(Linklist, data[2].Split(':')[1] + ":" + data[2].Split(':')[2], DownloadFolder);
                return;
            }

            Log.addLogEntry("Merging Video");

            if (File.Exists(Path.Combine(DownloadFolder, "output.mp4")))
            {
                File.Delete(Path.Combine(DownloadFolder, "output.mp4"));
            }

            using (StreamWriter sw = new StreamWriter(Path.Combine(DownloadFolder, "makemp4.bat")))
            {
                sw.WriteLine("..\\ffmpeg.exe -f concat -i files.txt -c copy output.mp4");
            }

            Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(DownloadFolder, "makemp4.bat"),
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = DownloadFolder
            };
            process.Start();

            process.WaitForExit();
            while (!process.HasExited) ;
            process.Dispose();

            Log.addLogEntry("Merging done ...");
            Log.addLogEntry("Deleting Files");

            string copyto = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), LinkGetter.Name + ".mp4");
            string copyfrom = Path.Combine(DownloadFolder, "output.mp4");
            File.Move(Path.Combine(copyfrom), Path.Combine(copyto));

            File.Delete(Path.Combine(DownloadFolder, "makemp4.bat"));
            StreamReader sr = new StreamReader(Path.Combine(DownloadFolder, "files.txt"));
            while (File.Exists(Path.Combine(DownloadFolder, "files.txt")))
            {
                try
                {
                    File.Delete(Path.Combine(DownloadFolder, "files.txt"));
                }
                catch { }
                Thread.Sleep(100);
                sr.Close();
            }
            File.Delete(Path.Combine(DownloadFolder, "DownloadInfo.Info"));
            Linklist.Keys.ToList().ForEach(item => File.Delete(Path.Combine(DownloadFolder, item.Split(new string[] { "-chunk-" }, StringSplitOptions.None)[1])));

            sw.Stop();
            Log.addLogEntry("Finished, time elapsed: " + sw.Elapsed.ToString().Split('.')[0]);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            dm.StopDownload();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            dm.StopDownload();
        }
    }
}
