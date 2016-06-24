﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChapterTool.Util;

namespace ChapterTool.Util.Tests
{
    [TestClass()]
    public class CueSheetTests
    {
        [TestMethod()]
        public void CueSheetTest()
        {
            string path = @"..\..\[cue_Sample]\ARCHIVES 2.cue";
            if (!File.Exists(path)) path = @"..\" + path;
            var cue = new CueSheet(path);
            var ci = cue.ToChapterInfo();
            foreach (var track in cue.Tracks)
            {
                Console.WriteLine($"{track.Title} {track.Performer} {track.Indices.Last().Time}");
            }
        }
    }
}