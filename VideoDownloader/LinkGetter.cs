using mshtml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoDownloader
{
    static class LinkGetter
    {
        public static WebBrowser gecko;
        private static string returned = "";
        public static string videolink = "";
        public static ListBox listBox1;
        public static string Name = "";

        public static async Task<string> GetLinkAsync(string VideoLink)
        {
            videolink = VideoLink;
            //prevent popup window opening from geckowebbrowser 
            //gecko.NewWindow += stayClosed;

            start();
            Log.addLogEntry("Fetching Website");
            string result = await Task.Factory.StartNew(() =>
            {
                while (returned == null || returned == "") ;
                return returned;
            });

            return result;
        }

        private static void stayClosed(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }

        private static void start()
        {
            gecko.Navigate(videolink);

            while (geckoisbusy() == true) ;
            Gecko_Navigated();
        }

        private static bool? geckoisbusy()
        {
            bool? isbusy = null;
            gecko.Invoke((MethodInvoker)delegate ()
            {
                isbusy = gecko.IsBusy;
            });
            while (isbusy == null) ;
            return isbusy;
        }

        private static void Gecko_Navigated()
        {
            Log.addLogEntry("Fetching Partlist");
            gecko.Invoke((MethodInvoker)delegate ()
            {
                gecko.Navigate("javascript:load_episode();");
            });

            Thread.Sleep(1000);

            getPartListLink();
        }

        private static void getPartListLink()
        {
            string result = "";
            string name = "";
            string doc = "";

            //
            gecko.Invoke((MethodInvoker)delegate ()
            {
                HtmlElement head = gecko.Document.GetElementsByTagName("head")[0];
                HtmlElement scriptEl3 = gecko.Document.CreateElement("script");
                IHTMLScriptElement element3 = (IHTMLScriptElement)scriptEl3.DomElement;
                element3.text = "function GetStringDoc() { return document.documentElement.innerHTML; }";
                head.AppendChild(scriptEl3);
            });
            //

            while (result == "" || name == "")
            {
                gecko.Invoke((MethodInvoker)delegate ()
                {
                    try
                    {
                        doc = gecko.Document.InvokeScript("GetStringDoc") + "";
                        result = doc.Split(new string[] { "window.urlVideo = \"" }, StringSplitOptions.None)[1].Split('\"')[0];
                        name = doc.Split(new string[] { "<li class=\"active\">" }, StringSplitOptions.None)[1].Split(new string[] { "</li>" }, StringSplitOptions.None)[0];
                    }
                    catch { }
                });

                Thread.Sleep(100);
            }

            Name = name;
            string Link = result;
            gecko.Navigate("https://hdfilme.cc");

            returned = Link;
        }
    }
}
