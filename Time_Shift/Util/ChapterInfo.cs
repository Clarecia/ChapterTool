﻿using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ChapterTool.Util
{
    public class ChapterInfo
    {
        public string Title           { get; set; }
        public string LangCode        { get; set; }
        public string SourceName      { get; set; }
        public int TitleNumber        { get; set; }
        public string SourceType      { get; set; }
        public double FramesPerSecond { get; set; }
        public TimeSpan Duration      { get; set; }
        public List<Chapter> Chapters { get; set; } = new List<Chapter>();
        public TimeSpan Offset        { get; set; } = TimeSpan.Zero;
        public bool Mul1K1            { get; set; }
        public Type TagType { get; set; }
        public object Tag
        {
            get { return _tag; }
            set
            {
                if (value == null)
                    return;
                _tag = value;
            }
        }
        private object _tag;

        public override string ToString() => $"{Title} - {SourceType} - {Duration.Time2String()} - [{Chapters.Count} Chapters]";

        public DataGridViewRow GetRow(int index, bool autoGenName)
        {
            var row = new DataGridViewRow
            {
                Tag = Chapters[index],
                DefaultCellStyle =
                {
                    BackColor = (Chapters[index].Number + 1)%2 == 0
                        ? Color.FromArgb(0x92, 0xAA, 0xF3)
                        : Color.FromArgb(0xF3, 0xF7, 0xF7)
                }
            };
            row.Cells.Add(new DataGridViewTextBoxCell {Value = $"{Chapters[index].Number:D2}"});
            row.Cells.Add(new DataGridViewTextBoxCell {Value = Chapters[index].Time2String(Offset, Mul1K1)});
            row.Cells.Add(new DataGridViewTextBoxCell {Value = autoGenName ? $"Chapter {row.Index + 1:D2}" : Chapters[index].Name});
            row.Cells.Add(new DataGridViewTextBoxCell {Value = Chapters[index].FramsInfo});
            return row;
        }

        public static ChapterInfo CombineChapter(List<ChapterInfo> source)
        {
            var fullChapter = new ChapterInfo
            {
                Title = "FULL Chapter",
                SourceType = "DVD",
                FramesPerSecond = source.First().FramesPerSecond
            };
            TimeSpan duration = TimeSpan.Zero;
            int index = 0;
            source.ForEach(chapterClip =>
            {
                chapterClip.Chapters.ForEach(item =>
                    fullChapter.Chapters.Add(new Chapter
                    {
                        Time = duration + item.Time,
                        Number = ++index,
                        Name = $"Chapter {index:D2}"
                    }));
                duration += chapterClip.Duration;
            });
            fullChapter.Duration = duration;
            return fullChapter;
        }

        public static Chapter WriteToChapterInfo(string line, string line2, int order, TimeSpan iniTime, bool notUseName)
        {
            Chapter temp = new Chapter { Number = order, Time = TimeSpan.Zero };
            if (!ConvertMethod.RLineOne.IsMatch(line) || !ConvertMethod.RLineTwo.IsMatch(line2)) return temp;
            temp.Name = notUseName ? $"Chapter {order:D2}"
                : ConvertMethod.RLineTwo.Match(line2).Groups["chapterName"].Value.Trim('\r');
            temp.Time = ConvertMethod.RTimeFormat.Match(line).Value.ToTimeSpan() - iniTime;
            return temp;
        }

        public void ChangeFps(double fps)
        {
            for (var i = 0; i < Chapters.Count; i++)
            {
                Chapter c = Chapters[i];
                double frames = c.Time.TotalSeconds * FramesPerSecond;
                Chapters[i] = new Chapter { Name = c.Name, Time = new TimeSpan((long)Math.Round(frames / fps * TimeSpan.TicksPerSecond)) };
            }
            double totalFrames = Duration.TotalSeconds * FramesPerSecond;
            Duration = new TimeSpan((long)Math.Round(totalFrames / fps * TimeSpan.TicksPerSecond));
            FramesPerSecond = fps;
        }

        #region updataInfo

        public void UpdataInfo(TimeSpan shift)
        {
            Chapters.ForEach(item => item.Time -= shift);
        }

        public void UpdataInfo(int shift)
        {
            int index = 0;
            Chapters.ForEach(item => item.Number = ++index + shift);
        }

        public void UpdataInfo(string chapterNameTemplate)
        {
            if (string.IsNullOrWhiteSpace(chapterNameTemplate)) return;
            var cn = chapterNameTemplate.Trim(' ', '\r', '\n').Split('\n').ToList().GetEnumerator();
            Chapters.ForEach(item => item.Name = cn.MoveNext() ? cn.Current : item.Name);
            cn.Dispose();
        }

        #endregion

        public string GetText(bool donotuseName)
        {
            StringBuilder lines = new StringBuilder();
            int i = 1;
            Chapters.ForEach(item =>
            {
                lines.Append($"CHAPTER{item.Number:D2}={item.Time.Time2String()}{Environment.NewLine}");
                lines.Append($"CHAPTER{item.Number:D2}NAME=");
                lines.Append(donotuseName ? $"Chapter {i++:D2}" : item.Name);
                lines.Append(Environment.NewLine);
            });
            return lines.ToString();
        }

        public void SaveText(string filename, bool notUseName)
        {
            StringBuilder lines = new StringBuilder();
            int i = 1;
            Chapters.ForEach(item =>
            {
                lines.Append($"CHAPTER{item.Number:D2}={item.Time2String(Offset, Mul1K1)}{Environment.NewLine}");
                lines.Append($"CHAPTER{item.Number:D2}NAME=");
                lines.Append(notUseName ? $"Chapter {i++:D2}" : item.Name);
                lines.Append(Environment.NewLine);
            });
            File.WriteAllText(filename, lines.ToString(), Encoding.UTF8);
        }

        public void SaveQpfile(string filename) => File.WriteAllLines(filename, Chapters.Select(c => c.FramsInfo.ToString().Replace("*", "I -1").Replace("K", "I -1")).ToArray());

        public void SaveCelltimes(string filename) => File.WriteAllLines(filename, Chapters.Select(c => ((long) Math.Round(c.Time.TotalSeconds*FramesPerSecond)).ToString()).ToArray());

        public void SaveTsmuxerMeta(string filename)
        {
            string text = $"--custom-{Environment.NewLine}chapters=";
            text = Chapters.Aggregate(text, (current, chapter) => current + chapter.Time2String(Offset, Mul1K1) + ";");
            text = text.Substring(0, text.Length - 1);
            File.WriteAllText(filename, text);
        }

        public void SaveTimecodes(string filename) => File.WriteAllLines(filename, Chapters.Select(item => item.Time2String(Offset, Mul1K1)).ToArray());

        public void SaveXml(string filename,string lang, bool notUseName)
        {
            if (string.IsNullOrWhiteSpace(lang)) lang = "und";
            Random rndb           = new Random();
            XmlTextWriter xmlchap = new XmlTextWriter(filename, Encoding.UTF8) {Formatting = Formatting.Indented};
            xmlchap.WriteStartDocument();
            xmlchap.WriteComment("<!DOCTYPE Tags SYSTEM \"matroskatags.dtd\">");
            xmlchap.WriteStartElement("Chapters");
              xmlchap.WriteStartElement("EditionEntry");
                xmlchap.WriteElementString("EditionFlagHidden", "0");
                xmlchap.WriteElementString("EditionFlagDefault", "0");
                xmlchap.WriteElementString("EditionUID", Convert.ToString(rndb.Next(1, int.MaxValue)));
                int i = 1;
                Chapters.ForEach(item =>
                {
                    xmlchap.WriteStartElement("ChapterAtom");
                      xmlchap.WriteStartElement("ChapterDisplay");
                        xmlchap.WriteElementString("ChapterString", notUseName ? $"Chapter {i++:D2}" : item.Name);
                        xmlchap.WriteElementString("ChapterLanguage", lang);
                      xmlchap.WriteEndElement();
                    xmlchap.WriteElementString("ChapterUID", Convert.ToString(rndb.Next(1, int.MaxValue)));
                    xmlchap.WriteElementString("ChapterTimeStart", item.Time2String(Offset, Mul1K1) + "0000");
                    xmlchap.WriteElementString("ChapterFlagHidden", "0");
                    xmlchap.WriteElementString("ChapterFlagEnabled", "1");
                    xmlchap.WriteEndElement();
                });
              xmlchap.WriteEndElement();
            xmlchap.WriteEndElement();
            xmlchap.Flush();
            xmlchap.Close();
        }
    }
}
