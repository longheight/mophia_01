using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mophia.Data
{
    [Serializable]
    public class ChartData
    {
        public MetaData          meta;
        public List<SectionData> sections;
    }

    [Serializable]
    public class MetaData
    {
        public string  mp4;
        public string  backgroundMp4; // 抽出されなかった箇所だけの背景動画。旧chart.jsonには無いのでnullの場合がある
        public string  title;
        public int     width;
        public int     height;
        public float[] backgroundColor; // [R, G, B] 0.0〜1.0。旧chart.jsonには無いのでnullの場合がある

        /// <summary>動画の背景色。未設定(旧形式のchart.json)の場合は白にフォールバックする</summary>
        public Color BackgroundColor =>
            backgroundColor != null && backgroundColor.Length >= 3
                ? new Color(backgroundColor[0], backgroundColor[1], backgroundColor[2])
                : Color.white;

        /// <summary>GamePlay背景に表示する動画。backgroundMp4が無ければ通常のmp4(元動画)にフォールバックする</summary>
        public string BackgroundMp4OrDefault =>
            !string.IsNullOrEmpty(backgroundMp4) ? backgroundMp4 : mp4;
    }

    [Serializable]
    public class SectionData
    {
        public int             sectionId;
        public float           startTime;
        public float           endTime;
        public ColorData       color;
        public PresetSelection presets;
        public List<NoteData>  notes;
    }

    [Serializable]
    public class PresetSelection
    {
        public string tap;
        public string flick;
        public string @long;
        public string touch;

        public string Get(string noteType) => noteType switch
        {
            "tap"   => tap,
            "flick" => flick,
            "long"  => @long,
            "touch" => touch,
            _       => tap
        };
    }

    // -------------------------------------------------------
    // ColorData  ―  RGBA配列形式 (Pythonパイプライン出力と対応)
    //   primary/secondary: [R, G, B, A] の float配列 (0.0〜1.0)
    //   isGradient: trueのとき secondary を使う
    //   type: "solid" | "gradient" | "none"
    // -------------------------------------------------------
    [Serializable]
    public class ColorData
    {
        public float[] primary;    // [R, G, B, A]
        public float[] secondary;  // [R, G, B, A]
        public bool    isGradient;
        public string  type;       // "solid" | "gradient" | "none"

        /// <summary>primary配列をUnityのColorに変換する</summary>
        public Color PrimaryColor =>
            primary != null && primary.Length >= 3
                ? new Color(primary[0], primary[1], primary[2],
                            primary.Length >= 4 ? primary[3] : 1f)
                : Color.white;

        /// <summary>secondary配列をUnityのColorに変換する</summary>
        public Color SecondaryColor =>
            secondary != null && secondary.Length >= 3
                ? new Color(secondary[0], secondary[1], secondary[2],
                            secondary.Length >= 4 ? secondary[3] : 1f)
                : Color.white;
    }

    [Serializable]
    public class NoteData
    {
        public int          index;
        public float        time;
        public PositionData position;
        public string       noteType;
        public float        direction;
        public float        duration;
        public string       images;
    }

    [Serializable]
    public class PositionData
    {
        public float x;
        public float y;
    }
}
