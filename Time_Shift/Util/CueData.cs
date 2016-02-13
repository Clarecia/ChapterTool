﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChapterTool.Util
{
    public static class CueData
    {
        private enum NextState
        {
            NsStart,
            NsTitle,
            NsNewTrack,
            NsTrack,
            NsError,
            NsFin
        }

        public static ChapterInfo PraseCue(string context)
        {
            var lines = context.Split('\n');
            var cue = new ChapterInfo { SourceType = "CUE" };
            Regex rTitle = new Regex(@"TITLE\s+\""(.+)\""");
            Regex rTrack = new Regex(@"TRACK (\d+) AUDIO");
            Regex rPerformer = new Regex(@"PERFORMER\s+\""(.+)\""");
            Regex rTime = new Regex(@"INDEX (?<index>\d+) (?<M>\d{2}):(?<S>\d{2}):(?<m>\d{2})");
            NextState nxState = NextState.NsStart;
            Chapter chapter = null;

            foreach (var line in lines)
            {
                switch (nxState)
                {
                    case NextState.NsStart:
                        var chapterTitleMatch = rTitle.Match(line);
                        if (chapterTitleMatch.Success)
                        {
                            cue.Title = chapterTitleMatch.Groups[0].Value;
                            nxState = NextState.NsNewTrack;
                        }
                        break;
                    case NextState.NsNewTrack:
                        var trackMatch = rTrack.Match(line);
                        if (trackMatch.Success)
                        {
                            chapter = new Chapter { Number = int.Parse(trackMatch.Groups[1].Value) };
                            nxState = NextState.NsTitle;
                        }
                        break;
                    case NextState.NsTitle:
                        var trackTitleMatch = rTitle.Match(line);
                        if (trackTitleMatch.Success)
                        {
                            Debug.Assert(chapter != null);
                            chapter.Name = trackTitleMatch.Groups[1].Value;
                            nxState = NextState.NsTrack;
                        }
                        break;
                    case NextState.NsTrack:
                        if (string.IsNullOrEmpty(line))
                        {
                            nxState = NextState.NsFin;
                            break;
                        }
                        var performerMatch = rPerformer.Match(line);
                        var timeMatch = rTime.Match(line);
                        var state = (1 << (performerMatch.Success ? 3 : 2)) | (1 << (timeMatch.Success ? 1 : 0));
                        switch (state)
                        {
                            case 1 << 2 | 1 << 0:
                                //nothing find
                                break;
                            case 1 << 2 | 1 << 1:
                                var trackIndex = int.Parse(timeMatch.Groups["index"].Value);
                                switch (trackIndex)
                                {
                                    case 0:
                                        // last track's end
                                        break;
                                    case 1:
                                        var minute = int.Parse(timeMatch.Groups["M"].Value);
                                        var second = int.Parse(timeMatch.Groups["S"].Value);
                                        var millisecond = int.Parse(timeMatch.Groups["m"].Value) * 10;
                                        Debug.Assert(chapter != null);
                                        chapter.Time = new TimeSpan(0, 0, minute, second, millisecond);
                                        cue.Chapters.Add(chapter);
                                        nxState = NextState.NsNewTrack;
                                        break;
                                    default:
                                        nxState = NextState.NsError;
                                        break;
                                }
                                break;
                            case 1 << 3 | 1 << 0:
                                Debug.Assert(chapter != null);
                                chapter.Name += $" [{performerMatch.Groups[1].Value}]";
                                break;
                            case 1 << 3 | 1 << 1:
                                nxState = NextState.NsError;
                                break;
                            default:
                                nxState = NextState.NsError;
                                break;
                        }
                        break;
                    case NextState.NsError:
                        throw new Exception("Unable to Prase this cue");
                    case NextState.NsFin:
                        goto EXIT_1;
                    default:
                        nxState = NextState.NsError;
                        break;
                }
            }
            EXIT_1:
            cue.Duration = cue.Chapters.Last().Time;
            return cue;
        }

        /// <summary>
        /// 从含有CueSheet的的区块中读取cue
        /// </summary>
        /// <param name="buffer">含有CueSheet的区块</param>
        /// <param name="type">音频格式类型, 大小写不敏感</param>
        /// <returns>UTF-8编码的cue</returns>
        /// <exception cref="T:System.ArgumentException"><paramref name="type"/> 不为 flac 或 tak。</exception>
        private static string GetCueSheet(byte[] buffer, string type)
        {
            type = type.ToLower();
            if (type != "flac" && type != "tak")
            {
                throw new ArgumentException($"Invalid parameter: [{nameof(type)}], which must be 'flac' or 'tak'");
            }
            int length = buffer.Length;
            //查找 Cuesheet 标记,自动机模型,大小写不敏感
            int state = 0, beginPos = 0;
            for (int i = 0; i < length; ++i)
            {
                if ((buffer[i] >= 0x41) && (buffer[i] <= 0x5A))
                    buffer[i] += 0x20;

                switch ((char)buffer[i])
                {
                    case 'c':
                        state = 1;      //C
                        break;
                    case 'u':
                        state = state == 1 ? 2 : 0;//Cu
                        break;
                    case 'e':
                        switch (state)
                        {
                            case 2:
                                state = 3;  //Cue
                                break;
                            case 5:
                                state = 6;  //Cueshe
                                break;
                            case 6:
                                state = 7;  //Cueshee
                                break;
                            default:
                                state = 0;
                                break;
                        }
                        break;
                    case 's':
                        state = state == 3 ? 4 : 0;//Cues
                        break;
                    case 'h':
                        state = state == 4 ? 5 : 0;//Cuesh
                        break;
                    case 't':
                        state = state == 7 ? 8 : 0;//Cuesheet
                        break;
                    default:
                        state = 0;
                        break;
                }
                if (state != 8) continue;
                beginPos = i + 2;
                break;
            }
            int controlCount = type == "flac" ? 3 : type == "tak" ? 6 : 0;
            int endPos = 0;
            state = 0;
            //查找终止符 0D 0A ? 00 00 00 (连续 controlCount 个终止符以上) (flac为3, tak为6)
            for (int i = beginPos; i < length; ++i)
            {
                switch (buffer[i])
                {
                    case 0:
                        state++;
                        break;
                    default:
                        state = 0;
                        break;
                }
                if (state == controlCount)
                {
                    endPos = i - controlCount; //指向0D 0A后的第一个字符
                    break;
                }
            }
            if (beginPos == 0 || endPos <= 1) return string.Empty;

            if ((buffer[endPos - 2] == '\x0D') && (buffer[endPos - 1] == '\x0A'))
                endPos--;

            var cueLength = endPos - beginPos + 1;
            if (cueLength <= 10) return string.Empty;
            string cueSheet = Encoding.UTF8.GetString(buffer, beginPos, cueLength);
            Debug.WriteLine(cueSheet);

            return cueSheet;
        }

        public static string GetCueFromTak(string takPath)
        {
            var fs = File.Open(takPath, FileMode.Open);
            if (fs.Length < 1048576)// 小于1M，文档太小了
            {
                fs.Close();
                return string.Empty;
            }
            var header = new byte[4];
            fs.Read(header, 0, 4);
            if (Encoding.ASCII.GetString(header, 0, 4) != "tBaK")
            {
                fs.Close();
                throw new InvalidDataException($"Except an tak but get an {Encoding.ASCII.GetString(header, 0, 4)}");
            }
            fs.Seek(-20480, SeekOrigin.End);
            var buffer = new byte[20480];
            fs.Read(buffer, 0, 20480);
            fs.Close();
            return GetCueSheet(buffer, "tak");
        }

        public static string GetCueFromFlac(string flacPath)
        {
            var fs = File.Open(flacPath, FileMode.Open);
            if (fs.Length < 1048576)// 小于1M，文档太小了
            {
                fs.Close();
                return string.Empty;
            }
            var header = new byte[4];
            fs.Read(header, 0, 4);
            if (Encoding.ASCII.GetString(header, 0, 4) != "fLaC")
            {
                fs.Close();
                throw new InvalidDataException($"Except an flac but get an {Encoding.ASCII.GetString(header, 0, 4)}");
            }

            var buffer = new byte[1];
            //4个字节的METADATA_BLOCK_HEADER
            do
            {
                fs.Read(header, 0, 4);
                //读取BLOCK长度
                int length = (header[1] << 16) | (header[2] << 8) | header[3];
                //解析
                //检查最高位是否为1
                if ((header[0] & 0x80) == 0x80)
                {
                    //最后一个METADATA_BLOCK
                    if ((header[0] & 0x7F) == 0x04)//是VORBIS_COMMENT
                    {
                        buffer = new byte[length];
                        //读取BLOCK DATA
                        fs.Read(buffer, 0, length);
                    }
                    break;
                }
                //不是最后一个METADATA_BLOCK
                if ((header[0] & 0x7F) == 0x04)//是VORBIS_COMMENT
                {
                    buffer = new byte[length];
                    //读取BLOCK DATA
                    fs.Read(buffer, 0, length);
                    break;
                }
                //移动文件指针
                fs.Seek(length, SeekOrigin.Current);
            } while (fs.Position <= 1048576L);
            fs.Close();

            return GetCueSheet(buffer, "flac");
        }
    }
}