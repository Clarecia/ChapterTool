﻿using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

namespace ChapterTool.Util
{
    public static class XplData
    {
        public static IEnumerable<ChapterInfo> GetStreams(string location)
        {
            XDocument doc = XDocument.Load(location);
            XNamespace ns = "http://www.dvdforum.org/2005/HDDVDVideo/Playlist";
            foreach (XElement ts in doc.Element(ns + "Playlist").Elements(ns + "TitleSet"))
            {
                var timeBase = GetFps((string)ts.Attribute("timeBase")) ?? 60; //required
                var tickBase = GetFps((string)ts.Attribute("tickBase")) ?? 60; //optional
                foreach (XElement title in ts.Elements(ns + "Title").Where(t => t.Element(ns + "ChapterList") != null))
                {
                    ChapterInfo pgc = new ChapterInfo
                    {
                        SourceName      = location,
                        SourceType      = "HD-DVD",
                        FramesPerSecond = 24D,
                        Chapters        = new List<Chapter>()
                    };

                    int tickBaseDivisor = (int?)title.Attribute("tickBaseDivisor") ?? 1; //optional
                    pgc.Duration        = GetTimeSpan((string)title.Attribute("titleDuration"), timeBase, tickBase, tickBaseDivisor);
                    var titleName       = Path.GetFileNameWithoutExtension(location);
                    if (title.Attribute("id") != null)          titleName = title.Attribute("id").Value; //optional
                    if (title.Attribute("displayName") != null) titleName = title.Attribute("displayName").Value; //optional
                    pgc.Title           = titleName;
                    foreach (XElement chapter in title.Element(ns + "ChapterList").Elements(ns + "Chapter"))
                    {
                        var chapterName = string.Empty;
                        if (chapter.Attribute("id") != null) chapterName = chapter.Attribute("id").Value; //optional
                        if (chapter.Attribute("displayName") != null) chapterName = chapter.Attribute("displayName").Value; //optional
                        pgc.Chapters.Add(new Chapter
                        {
                            Name = chapterName,
                            Time = GetTimeSpan((string)chapter.Attribute("titleTimeBegin"), timeBase, tickBase, tickBaseDivisor) //required
                        });
                    }
                    yield return pgc;
                }
            }
        }

        /// <summary>
        /// Eg: Convert string "\d+fps" to a double value
        /// </summary>
        /// <param name="fps"></param>
        /// <returns></returns>
        private static double? GetFps(string fps)
        {
            if (string.IsNullOrEmpty(fps)) return null;
            fps = fps.Replace("fps", string.Empty);
            return float.Parse(fps);
        }

        /// <summary>
        /// Constructs a TimeSpan from a string formated as "HH:MM:SS:TT"
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="timeBase"></param>
        /// <param name="tickBase"></param>
        /// <param name="tickBaseDivisor"></param>
        /// <returns></returns>
        private static TimeSpan GetTimeSpan(string timeSpan, double timeBase, double tickBase, int tickBaseDivisor)
        {
            var colonPosition = timeSpan.LastIndexOf(':');
            TimeSpan ts = TimeSpan.Parse(timeSpan.Substring(0, colonPosition));
            ts = new TimeSpan((long)(ts.TotalSeconds / 60D * timeBase) * TimeSpan.TicksPerSecond);

            //convert ticks to ticks timebase
            decimal newTick = TimeSpan.TicksPerSecond / ((decimal)tickBase / tickBaseDivisor);
            decimal ticks   = decimal.Parse(timeSpan.Substring(colonPosition + 1)) * newTick;
            return ts.Add(new TimeSpan((long)ticks));
        }
    }
}
