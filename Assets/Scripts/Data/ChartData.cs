using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mophia.Data
{
    // -------------------------------------------------------
    // ChartData  ―  Mophia チャートデータ全体
    // -------------------------------------------------------
    [Serializable]
    public class ChartData
    {
        public MetaData            meta;
        public List<SectionData>   sections;
    }

    // -------------------------------------------------------
    // MetaData  ―  楽曲メタ情報
    // -------------------------------------------------------
    [Serializable]
    public class MetaData
    {
        public string mp4;
        public string title;
        public int    width;
        public int    height;
    }

    // -------------------------------------------------------
    // SectionData  ―  区間ごとのプリセット情報＋ノーツ一覧
    //   presets: セクション内の映像特性から選ばれた操作タイプ別プリセット
    //   notes:   このセクションに属するノーツ（noteTypeからpresetを引く）
    // -------------------------------------------------------
    [Serializable]
    public class SectionData
    {
        public int              sectionId;
        public float            startTime;
        public float            endTime;
        public ColorData        color;
        public PresetSelection  presets;   // noteType別プリセット（スコアリング結果）
        public List<NoteData>   notes;
    }

    // -------------------------------------------------------
    // PresetSelection  ―  操作タイプごとの採用プリセット
    //   例: { "tap": "shrink_sharp", "flick": "vector_round", ... }
    // -------------------------------------------------------
    [Serializable]
    public class PresetSelection
    {
        public string tap;
        public string flick;
        public string @long;
        public string touch;

        // noteTypeの文字列からプリセットを引くユーティリティ
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
    // ColorData  ―  セクション単位の色情報
    //   映像オブジェクトのカラーから自動生成される
    // -------------------------------------------------------
    [Serializable]
    public class ColorData
    {
        // "solid" | "gradient" | "none"
        public string type;
        // hex (#RRGGBB) または空文字
        public string primary;
        public string secondary;
    }

    // -------------------------------------------------------
    // NoteData  ―  1ノーツ分のデータ
    //   preset は持たない → SectionData.presets.Get(noteType) で取得
    // -------------------------------------------------------
    [Serializable]
    public class NoteData
    {
        public int          index;
        public float        time;
        public PositionData position;
        // "tap" | "flick" | "long" | "touch"
        public string       noteType;
        // flick方向(度) 0=上・時計回り / flick以外は -1
        public float        direction;
        // long持続秒 / long以外は -1
        public float        duration;
        // StreamingAssets以下のフォルダ名（スプライトシート格納先）
        public string       images;
    }

    // -------------------------------------------------------
    // PositionData  ―  画面上の位置（ピクセル座標）
    // -------------------------------------------------------
    [Serializable]
    public class PositionData
    {
        public float x;
        public float y;
    }
}
