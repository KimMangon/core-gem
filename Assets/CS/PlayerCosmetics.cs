using UnityEngine;

/// <summary>One torso item and one head item, encoded as two validated item indices.</summary>
public static class PlayerCosmetics
{
    public const int ColorCount = 11;
    public static readonly string[] ColorEnglish = { "ORIGINAL", "BLUE", "RED", "ORANGE", "YELLOW", "GREEN", "CYAN", "PURPLE", "PINK", "WHITE", "BLACK" };
    public static readonly string[] ColorKorean = { "기본 색", "파랑", "빨강", "주황", "노랑", "초록", "하늘색", "보라", "분홍", "흰색", "검정" };
    public static readonly string[][] English = {
        new[] { "NONE", "NECKTIE", "STAR BADGE", "BELT", "BOW TIE", "NECKLACE" },
        new[] { "NONE", "SUNGLASSES", "TOP HAT", "ARMY HELMET", "BUCKET HAT", "ROUND GLASSES", "BEANIE", "HOOD", "EXPLORER HAT", "SANTA HELMET", "CROWN", "RIBBON", "MOUSTACHE" }
    };
    public static readonly string[][] Korean = {
        new[] { "없음", "넥타이", "별 배지", "허리띠", "턱시도 리본", "목걸이" },
        new[] { "없음", "선글라스", "모자", "군인 헬멧", "벙거지 모자", "동그란 안경", "비니", "후드", "탐험가 모자", "산타 헬멧", "왕관", "리본", "콧수염" }
    };
    public static int Selected
    {
        get
        {
            if (PlayerPrefs.HasKey("PlayerCosmeticsV2")) return Normalize(PlayerPrefs.GetInt("PlayerCosmeticsV2", 0));
            int old = PlayerPrefs.GetInt("PlayerCosmetics", 0);
            int torso = (old & 1) != 0 ? 1 : (old & 2) != 0 ? 2 : 0;
            int head = (old & 4) != 0 ? 1 : (old & 8) != 0 ? 2 : 0;
            return torso | (head << 8);
        }
        set { PlayerPrefs.SetInt("PlayerCosmeticsV2", Normalize(value)); PlayerPrefs.Save(); }
    }

    public static int Normalize(int value)
    {
        int torso = value & 255, head = (value >> 8) & 255;
        if (torso >= English[0].Length) torso = 0;
        if (head >= English[1].Length) head = 0;
        int bodyColor=(value >> 16)&15,headColor=(value >> 20)&15;
        if(bodyColor>=ColorCount)bodyColor=0;
        if(headColor>=ColorCount)headColor=0;
        return torso | (head << 8) | (bodyColor << 16) | (headColor << 20);
    }

    public static int GetItem(int selection, int category)
    {
        return (Normalize(selection) >> (category * 8)) & 255;
    }

    public static void Cycle(int category, int direction)
    {
        int count = English[category].Length;
        int item = (GetItem(Selected, category) + direction + count) % count;
        int shift = category * 8;
        Selected = (Selected & ~(255 << shift)) | (item << shift);
    }

    public static int GetColor(int selection,int category) => (Normalize(selection) >> (16+category*4)) & 15;

    public static void SetColor(int category,int color)
    {
        int shift=16+category*4;
        Selected=(Selected & ~(15 << shift)) | (Mathf.Clamp(color,0,ColorCount-1) << shift);
    }

    public static Color Tint(int color)
    {
        if(color>=1 && color<=8)return PlayerColorPalette.Colors[color-1];
        return color==10?new Color(.055f,.065f,.08f):Color.white;
    }

    public static uint CustomColor(int category) => unchecked((uint)PlayerPrefs.GetInt("AccessoryRGB"+category,0));
    public static void SetCustomColor(int category,uint color)
    {
        PlayerPrefs.SetInt("AccessoryRGB"+category,unchecked((int)color));PlayerPrefs.Save();
    }
}


