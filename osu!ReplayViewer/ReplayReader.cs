using SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CyuUtils
{
    class ReplayReader
    {
        private const bool _benchmarkMode = false;

        public class ReplayFrame
        {
            public int Time;

            public float X;
            public float Y;

            public ReplayFrame(int time, float x, float y)
            {
                Time = time;
                X    = x;
                Y    = y;
            }
        }

        [Flags]
        public enum Mods
        {
            None,
            NoFail,
            Easy,
            TouchDevice = 1 << 2,
            Hidden      = 1 << 3,
            HardRock    = 1 << 4,
            SuddenDeath = 1 << 5,
            DoubleTime  = 1 << 6,
            Relax       = 1 << 7,
            HalfTime    = 1 << 8,
            Nightcore   = 1 << 9,
            Flashlight  = 1 << 10,
            Autoplay    = 1 << 11,
            SpunOut     = 1 << 12,
            Relax2      = 1 << 13,
            Perfect     = 1 << 14,
            Key4        = 1 << 15,
            Key5        = 1 << 16,
            Key6        = 1 << 17,
            Key7        = 1 << 18,
            Key8        = 1 << 19,
            FadeIn      = 1 << 20,
            Random      = 1 << 21,
            Cinema      = 1 << 22,
            Target      = 1 << 23,
            Key9        = 1 << 24,
            KeyCoop     = 1 << 25,
            Key1        = 1 << 26,
            Key3        = 1 << 27,
            Key2        = 1 << 28,
            ScoreV2     = 1 << 29,
            LastMod     = 1 << 30,

            KeyMod            = Key1 | Key2 | Key3 | Key4 | Key5 | Key6 | Key7 | Key8 | Key9 | KeyCoop,
            FreeModAllowed    = NoFail | Easy | Hidden | HardRock | SuddenDeath | Flashlight | FadeIn | Relax | Relax2 | SpunOut | KeyMod,
            ScoreIncreaseMods = Hidden | HardRock | DoubleTime | Flashlight | FadeIn
        }

        public enum GameModes
        {
            Osu,
            Taiko,
            Ctb,
            Mania
        }

        private BinaryReader _reader;

        public GameModes GameMode;
        public int Version;
        public string BeatmapHash;
        public string PlayerName;
        public string ReplayHash;
        public ushort Count300;
        /// <summary>
        /// 150s in Taiko, 200s in mania.
        /// </summary>
        public ushort Count100;
        /// <summary>
        /// Small fruit in CtB.
        /// </summary>
        public ushort Count50;
        /// <summary>
        /// Max 300s in mania.
        /// </summary>
        public ushort CountGeki;
        /// <summary>
        /// 100s in mania.
        /// </summary>
        public ushort CountKatu;
        public ushort CountMiss;
        public int TotalScore;
        public ushort MaxCombo;
        public bool Perfect;
        public Mods EnabledMods;
        public string LifebarGraphString;
        public DateTime TimeStamp;
        public byte[] CompressedReplayData;
        /// <summary>
        /// If replayDecompress was set to false (when initializing ReplayReader) then this will be empty.
        /// </summary>
        public byte[] DecompressedReplayData;
        public List<ReplayFrame> ReplayFrames;
        /// <summary>
        /// Only usable on Version >= 20140721
        /// </summary>
        public long OnlineId;
        public int Seed;

        public ReplayReader(string fileName, bool replayDecompress = true)
        {
            Stopwatch sw = null;

            if (_benchmarkMode)
            {
                GC.Collect();
                sw = Stopwatch.StartNew();
            }

            _reader = new BinaryReader(File.OpenRead(fileName));

            ReadHeader();
            ReadReplay(replayDecompress);

            _reader.Dispose();

            if (_benchmarkMode)
            {
                sw.Stop();
                Debug.WriteLine($"Finished reading replay in {sw.ElapsedMilliseconds}ms! {(replayDecompress ? string.Empty : "(without decompressing replay data)")}");
            }
        }

        public ReplayReader(Stream fileStream, bool replayDecompress = true)
        {
            Stopwatch sw = null;

            if (_benchmarkMode)
            {
                GC.Collect();
                sw = Stopwatch.StartNew();
            }

            _reader = new BinaryReader(fileStream);

            ReadHeader();
            ReadReplay(replayDecompress);

            _reader.Dispose();

            if (_benchmarkMode)
            {
                sw.Stop();
                Debug.WriteLine($"Finished reading replay in {sw.ElapsedMilliseconds}ms! {(replayDecompress ? string.Empty : "(without decompressing replay data)")}");
            }
        }

        private void ReadHeader()
        {
            if (_reader == null) throw new NullReferenceException("_reader");

            GameMode           = (GameModes) _reader.ReadByte();
            Version            = _reader.ReadInt32();
            BeatmapHash        = ReadString();
            PlayerName         = ReadString();
            ReplayHash         = ReadString();
            Count300           = _reader.ReadUInt16();
            Count100           = _reader.ReadUInt16();
            Count50            = _reader.ReadUInt16();
            CountGeki          = _reader.ReadUInt16();
            CountKatu          = _reader.ReadUInt16();
            CountMiss          = _reader.ReadUInt16();
            TotalScore         = _reader.ReadInt32();
            MaxCombo           = _reader.ReadUInt16();
            Perfect            = _reader.ReadBoolean();
            EnabledMods        = (Mods) _reader.ReadInt32();
            LifebarGraphString = ReadString();
            TimeStamp          = ReadDateTime();
        }

        private void ReadReplay(bool replayDecompress)
        {
            CompressedReplayData = ReadByteArray();

            if (replayDecompress)
            {
                ReplayFrames           = new List<ReplayFrame>();
                DecompressedReplayData = SevenZipHelper.Decompress(CompressedReplayData);

                string replayData = Encoding.ASCII.GetString(DecompressedReplayData);
                if (replayData.Length > 0)
                {
                    string[] replayLines = replayData.Split(',');

                    ReplayFrame lastFrame;
                    if (ReplayFrames.Count > 0)
                        lastFrame = ReplayFrames[ReplayFrames.Count - 1];
                    else
                        lastFrame = new ReplayFrame(0, 0, 0);

                    foreach (string replayLine in replayLines)
                    {
                        if (replayLine.Length == 0)
                            continue;

                        string[] data = replayLine.Split('|');
                        if (data.Length < 4)
                            continue;

                        if (data[0] == "-12345")
                        {
                            Seed = int.Parse(data[3]);
                            continue;
                        }

                        ReplayFrame nextFrame = new ReplayFrame(
                            int.Parse(data[0]) + lastFrame.Time,
                            float.Parse(data[1]),
                            float.Parse(data[2])
                        );

                        ReplayFrames.Add(nextFrame);
                        lastFrame = nextFrame;
                    }
                }
            }

            if (Version >= 20140721) OnlineId = _reader.ReadInt64();
        }

        private string ReadString()
        {
            if (_reader.ReadByte() != 11) throw new Exception("Invalid string type ID.");
            return _reader.ReadString();
        }

        private DateTime ReadDateTime()
        {
            return new DateTime(_reader.ReadInt64());
        }

        private byte[] ReadByteArray()
        {
            return _reader.ReadBytes(_reader.ReadInt32());
        }
    }
}
