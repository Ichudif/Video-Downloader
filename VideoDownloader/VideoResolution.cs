using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoDownloader
{
    class VideoResolution
    {
        public string Resolution { get; set; }
        public string Link { get; set; }

        public VideoResolution(string resolution, string link)
        {
            Resolution = resolution;
            Link = link;
        }
    }
}
