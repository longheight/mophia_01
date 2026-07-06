using System;
using System.Collections.Generic;

namespace Mophia.Data
{
    [Serializable]
    public class ChartData
    {
        public MetaData meta;
        public List<SectionData> sections;
    }

    [Serializable]
    public class MetaData
    {
        public string mp4;
        public string title;
        public int width;
        public int height;
    }

    [Serializable]
    public class SectionData
    {
        public int sectionId;
        public ColorData color;
        public List<NoteData> notes;
    }

    [Serializable]
    public class ColorData
    {
        // "solid" | "gradient" | "none"
        public string type;
        // hex (#RRGGBB) または空文字
        public string primary;
        public string secondary;
    }

    [Serializable]
    public class NoteData
    {
        public int index;
        public float time;
        public PositionData position;
        // "tap" | "flick" | "long" | "touch"
        public string noteType;
        // preset enum文字列 例: "shrink_circle_round"
        public string preset;
        // flick方向(度) 0=上 時計回り / flick以外は -1
        public float direction;
        // long持続秒 / long以外は -1
        public float duration;
        // StreamingAssets以下のフォルダ名
        public string images;
    }

    [Serializable]
    public class PositionData
    {
        public float x;
        public float y;
    }
}
