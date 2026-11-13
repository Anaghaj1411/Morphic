using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// Translates Morphic UI and Lisa lines from the main-menu language page:
/// English (en), Traditional Chinese Taiwan (zh-TW), Indonesian (id).
public static class MorphicLoc
{
    public const string PrefsKey = "Morphic.Language";

    public static string Code { get; private set; } = "en";

    public static event System.Action Changed;

    private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> Id = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> ZhRev = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> IdRev = new Dictionary<string, string>();
    private static TMP_FontAsset cjkFont;
    private static bool cjkFallbackReady;
    private static bool cjkSearchDone;

    static MorphicLoc()
    {
        FillZh();
        FillId();
        SetCode(PlayerPrefs.GetString(PrefsKey, "en"), false);
    }

    public static void SetCode(string code, bool notify = true)
    {
        if (string.IsNullOrEmpty(code))
            code = "en";
        if (code != "en" && code != "zh-TW" && code != "id")
            code = "en";

        bool changed = Code != code;
        Code = code;
        PlayerPrefs.SetString(PrefsKey, code);
        PlayerPrefs.Save();
        if (notify && changed)
            Changed?.Invoke();
    }

    public static string EnglishKey(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        if (Zh.ContainsKey(text) || Id.ContainsKey(text))
            return text;
        if (ZhRev.TryGetValue(text, out string fromZh))
            return fromZh;
        if (IdRev.TryGetValue(text, out string fromId))
            return fromId;

        if (text.IndexOf('.') >= 0 || text.IndexOf(',') >= 0 || text.Length > 28)
            return text;

        string spaced = InsertDigitSpace(text.Replace('_', ' ').Replace('-', ' ').Trim());
        while (spaced.Contains("  "))
            spaced = spaced.Replace("  ", " ");
        string titled = TitleAsset(spaced);
        string upper = spaced.ToUpperInvariant();
        if (Zh.ContainsKey(titled) || Id.ContainsKey(titled))
            return titled;
        if (Zh.ContainsKey(upper) || Id.ContainsKey(upper))
            return upper;
        if (ZhRev.TryGetValue(titled, out fromZh) || ZhRev.TryGetValue(upper, out fromZh))
            return fromZh;
        if (IdRev.TryGetValue(titled, out fromId) || IdRev.TryGetValue(upper, out fromId))
            return fromId;
        return text;
    }

    private static string InsertDigitSpace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        StringBuilder sb = new StringBuilder(text.Length + 4);
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsDigit(text[i]) && char.IsLetter(text[i - 1]))
                sb.Append(' ');
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    private static string TitleAsset(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0 || char.IsDigit(parts[i][0]))
                continue;
            parts[i] = char.ToUpperInvariant(parts[i][0]) +
                (parts[i].Length > 1 ? parts[i].Substring(1).ToLowerInvariant() : "");
        }
        return string.Join(" ", parts);
    }

    public static string T(string english)
    {
        if (string.IsNullOrEmpty(english))
            return english;

        english = EnglishKey(english);
        if (Code == "en")
            return english;

        // Don't swap in CJK if the font failed; empty boxes are worse than English.
        if (Code == "zh-TW" && GetCjkFont() == null)
            return english;

        Dictionary<string, string> table = Code == "zh-TW" ? Zh : Code == "id" ? Id : null;
        if (table != null && table.TryGetValue(english, out string translated) &&
            !string.IsNullOrEmpty(translated))
            return translated;
        return english;
    }

    public static string F(string englishTemplate, params object[] args)
    {
        return string.Format(T(englishTemplate), args);
    }

    public static string AiLanguageName()
    {
        if (Code == "zh-TW")
            return "Traditional Chinese (Taiwan)";
        if (Code == "id")
            return "Indonesian";
        return "English";
    }

    public static string WithAiLanguage(string question)
    {
        string lang = AiLanguageName();
        if (Code == "en")
            return string.IsNullOrEmpty(question)
                ? "Reply in English only, as Lisa."
                : "Reply in English only, as Lisa. " + question;

        StringBuilder names = new StringBuilder();
        for (int i = 0; i < AiUiTerms.Length; i++)
        {
            if (i > 0)
                names.Append("; ");
            names.Append(AiUiTerms[i]).Append(" = ").Append(T(AiUiTerms[i]));
        }

        string instruction =
            "You are Lisa. Reply 100 percent in " + lang +
            ". Do not leave any English words in the answer. " +
            "When you mention a button or panel, use these exact translations: " +
            names + ".";
        if (string.IsNullOrEmpty(question))
            return instruction;
        return instruction + " User question: " + question;
    }

    public static string LocalizeAiReply(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        string known = EnglishKey(message);
        if (Zh.ContainsKey(known) || Id.ContainsKey(known))
            return T(known);

        if (Code == "en")
            return message;

        for (int i = 0; i < AiUiTerms.Length; i++)
        {
            string term = AiUiTerms[i];
            int index = message.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                continue;
            message = message.Substring(0, index) + T(term) + message.Substring(index + term.Length);
        }

        if (message.StartsWith("LISA:", StringComparison.OrdinalIgnoreCase))
            message = T("LISA") + (Code == "zh-TW" ? "：" : ": ") + message.Substring(5).TrimStart();
        return message;
    }

    private static readonly string[] AiUiTerms =
    {
        "COLOR PALETTE", "VERTEX SCULPT", "SELECT PART", "CHANGE COLOR",
        "ADD DESIGN", "DRAW BAND", "CLEAR DRAWINGS", "CLEAR DESIGN",
        "TAKE SELFIE", "ATTACH TO LANTERN", "ATTACH TO TEAPOT", "ATTACH TO KERIS",
        "REMOVE SELFIE", "START SCULPTING", "BAND COLOR", "BAND WIDTH",
        "CLAY COLOR", "STAMP COLOR", "BRUSH SIZE", "ASK LISA", "HELP"
    };

    public static void Apply(TMP_Text label, string english)
    {
        if (label == null)
            return;
        Bind(label, english);
    }

    public static void Bind(TMP_Text label, string english)
    {
        if (label == null)
            return;

        english = EnglishKey(english);
        MorphicLocKey key = label.GetComponent<MorphicLocKey>();
        if (key == null)
            key = label.gameObject.AddComponent<MorphicLocKey>();
        key.SetKey(english);
        Write(label, english);
    }

    public static void Write(TMP_Text label, string english)
    {
        if (label == null)
            return;
        ApplyFont(label);
        label.text = T(english);
        if (label.gameObject.name == "Instruction")
            MorphicText.FitBody(label);
        if (label.isActiveAndEnabled)
            label.ForceMeshUpdate(true);
    }

    public static void BindTree(Transform root)
    {
        if (root == null)
            return;

        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null)
                continue;

            MorphicLocKey existing = label.GetComponent<MorphicLocKey>();
            if (existing != null)
            {
                existing.Refresh();
                continue;
            }

            string english = EnglishKey(label.text);
            if (string.IsNullOrWhiteSpace(english) || LooksDynamic(english))
                continue;
            if (!Zh.ContainsKey(english) && !Id.ContainsKey(english))
                continue;
            Bind(label, english);
        }
    }

    private static bool LooksDynamic(string text)
    {
        if (text.Length <= 4 && char.IsDigit(text[0]))
            return true;
        if (text.EndsWith("%", StringComparison.Ordinal))
            return true;
        return false;
    }

    public static void ApplyFont(TMP_Text label)
    {
        if (label == null)
            return;

        MorphicText.Style(label);

        TMP_FontAsset cjk = Code == "zh-TW" ? GetCjkFont() : cjkFont;
        if (Code == "zh-TW" && cjk != null)
        {
            if (label.font != cjk)
                label.font = cjk;
            return;
        }

        if (cjkFont != null && label.font == cjkFont)
        {
            TMP_FontAsset def = TMP_Settings.defaultFontAsset;
            if (def != null)
                label.font = def;
        }

        MorphicText.ApplyBoldFace(label);
    }

    public static void ApplyCjkGlyphs(TMP_Text label)
    {
        if (label == null)
            return;

        TMP_FontAsset cjk = GetCjkFont();
        if (cjk == null)
            return;
        if (label.font != cjk)
            label.font = cjk;
    }

    private static void EnsureCjkFallback(TMP_FontAsset cjk)
    {
        if (cjkFallbackReady || cjk == null)
            return;
        cjkFallbackReady = true;
        TMP_FontAsset baseFont = TMP_Settings.defaultFontAsset;
        if (baseFont == null || baseFont == cjk)
            return;
        if (baseFont.fallbackFontAssetTable == null)
            baseFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (!baseFont.fallbackFontAssetTable.Contains(cjk))
            baseFont.fallbackFontAssetTable.Add(cjk);
    }

    private static TMP_FontAsset GetCjkFont()
    {
        if (cjkFont != null)
            return cjkFont;
        if (cjkSearchDone)
            return null;
        cjkSearchDone = true;

        cjkFont =
            TryCreateCjkFromWindowsFonts() ??
            TryCreateCjkByFamily() ??
            TryCreateCjkFromOsPaths();

        if (cjkFont != null)
        {
            cjkFont.name = "MorphicCJK";
            cjkFont.isMultiAtlasTexturesEnabled = true;
            SeedCjkAtlas(cjkFont);
            EnsureCjkFallback(cjkFont);
            Debug.Log("MORPHIC: Traditional Chinese font ready (" + cjkFont.faceInfo.familyName + ")");
        }
        else
        {
            Debug.LogWarning("MORPHIC: Could not load a Traditional Chinese font. Keeping English text.");
        }

        return cjkFont;
    }

    private static TMP_FontAsset TryCreateCjkByFamily()
    {
        string[] families =
        {
            "Microsoft JhengHei",
            "Microsoft JhengHei UI",
            "Noto Sans CJK TC",
            "Noto Sans TC",
            "PingFang TC",
            "MingLiU",
            "PMingLiU"
        };
        string[] styles = { "Regular", "Normal", "Book" };
        for (int i = 0; i < families.Length; i++)
        {
            for (int s = 0; s < styles.Length; s++)
            {
                TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(families[i], styles[s], 42);
                if (created != null)
                    return created;
            }
        }
        return null;
    }

    private static TMP_FontAsset TryCreateCjkFromWindowsFonts()
    {
        string fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (string.IsNullOrEmpty(fontsDir))
            return null;

        string[] files = { "msjh.ttc", "msjhl.ttc", "msjhbd.ttc", "mingliu.ttc", "kaiu.ttf", "msyh.ttc" };
        for (int i = 0; i < files.Length; i++)
        {
            string path = Path.Combine(fontsDir, files[i]);
            if (!File.Exists(path))
                continue;
            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
                path, 0, 42, 5, GlyphRenderMode.SDFAA, 2048, 2048);
            if (created != null)
                return created;
        }
        return null;
    }

    private static TMP_FontAsset TryCreateCjkFromOsPaths()
    {
        string[] paths = Font.GetPathsToOSFonts();
        if (paths == null)
            return null;

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            if (string.IsNullOrEmpty(path))
                continue;
            string lower = path.ToLowerInvariant();
            bool match =
                lower.Contains("msjh") ||
                lower.Contains("jhenghei") ||
                lower.Contains("mingliu") ||
                lower.Contains("pmingliu") ||
                (lower.Contains("noto") && (lower.Contains("cjk") || lower.Contains("tc")));
            if (!match)
                continue;

            Font osFont = new Font(path);
            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
                osFont, 42, 5, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            if (created != null)
                return created;
        }
        return null;
    }

    private static void SeedCjkAtlas(TMP_FontAsset font)
    {
        if (font == null)
            return;
        StringBuilder sb = new StringBuilder(2048);
        foreach (KeyValuePair<string, string> pair in Zh)
        {
            if (!string.IsNullOrEmpty(pair.Value))
                sb.Append(pair.Value);
        }
        sb.Append("繁中");
        font.TryAddCharacters(sb.ToString());
    }

    private static void Add(Dictionary<string, string> table, string english, string translated)
    {
        if (table == null || string.IsNullOrEmpty(english) || table.ContainsKey(english))
            return;
        table.Add(english, translated);
        if (string.IsNullOrEmpty(translated))
            return;
        if (table == Zh)
            ZhRev[translated] = english;
        else if (table == Id)
            IdRev[translated] = english;
    }

    private static void FillZh()
    {
        Add(Zh, "START", "開始");
        Add(Zh, "LANGUAGE", "語言");
        Add(Zh, "SETTINGS", "設定");
        Add(Zh, "MAIN MENU", "主選單");
        Add(Zh, "EXIT", "離開");
        Add(Zh, "LISA VOICE: ON", "麗莎語音：開");
        Add(Zh, "LISA VOICE: OFF", "麗莎語音：關");
        Add(Zh, "Point your right index finger at the lantern and move it to push the clay.", "用右手食指指向燈籠並移動來按壓陶土。");
        Add(Zh, "Point your right index finger at the teapot and move it to push the clay.", "用右手食指指向茶壺並移動來按壓陶土。");
        Add(Zh, "Point your right index finger at the keris and move it to push the clay.", "用右手食指指向克里斯劍並移動來按壓陶土。");
        Add(Zh, "Point your right index finger and trace a band. Open your palm to lock.", "用右手食指描繪飾帶，張開手掌鎖定。");
        Add(Zh, "ENGLISH", "英語");
        Add(Zh, "TAIWANESE", "繁體中文");
        Add(Zh, "TW", "繁中");
        Add(Zh, "INDONESIAN", "印尼語");
        Add(Zh, "Music", "音樂");
        Add(Zh, "MUSIC", "音樂");
        Add(Zh, "SFX", "音效");
        Add(Zh, "Sound Effects", "音效");
        Add(Zh, "CHOOSE A CRAFT", "選擇工藝");
        Add(Zh, "Taiwan", "台灣");
        Add(Zh, "India", "印度");
        Add(Zh, "Indonesia", "印尼");
        Add(Zh, "LISA", "麗莎");
        Add(Zh, "Hi! I'm Lisa. Nice to meet you!", "嗨！我是麗莎，很高興認識你！");
        Add(Zh, "Hi, Lisa!", "嗨，麗莎！");
        Add(Zh, "Where are we?", "我們在哪裡？");
        Add(Zh, "It's wonderful to finally be back in Yuanlin. I opened a pottery studio here so I can work with clay every day.", "終於回到員林了，真好。我在這裡開了一間陶藝工作室，每天都能與陶土為伴。");
        Add(Zh, "Tell me more", "多告訴我一些");
        Add(Zh, "Why pottery?", "為什麼是陶藝？");
        Add(Zh, "We're in Yuanlin, my hometown. This pottery studio is my new home for making art with clay.", "我們在員林，我的故鄉。這間陶藝工作室是我用陶土創作的新家。");
        Add(Zh, "Your journey", "你的旅程");
        Add(Zh, "Yuanlin is full of little workshops and people who still make things by hand. I wanted my studio to be one of those places — a quiet corner for clay.", "員林有許多小工坊，還有人堅持用手做東西。我希望我的工作室也成為那樣的地方——一處安靜的陶土角落。");
        Add(Zh, "That's lovely", "真美好");
        Add(Zh, "So why pottery?", "那為什麼是陶藝？");
        Add(Zh, "Because clay lets you turn an idea into something you can actually hold. During my studies I saw how differently cultures shape the same material.", "因為陶土能把想法變成可以拿在手上的東西。求學時我看到不同文化如何用同一種材料塑形。");
        Add(Zh, "What did you learn?", "你學到了什麼？");
        Add(Zh, "Let's make something!", "我們來做東西吧！");
        Add(Zh, "I studied art in India and Indonesia. Their sculpting traditions inspired me to create a studio where those cultures can meet through clay — along with the lantern craft I grew up with in Taiwan.", "我在印度與印尼學藝術。他們的雕塑傳統啟發我打造一間工作室，讓那些文化能透過陶土相遇——還有我在台灣長大時熟悉的天燈工藝。");
        Add(Zh, "I want to see!", "我想看看！");
        Add(Zh, "Amazing!", "太棒了！");
        Add(Zh, "That's the heart of this studio. I want people to experiment, learn, and leave with something they shaped themselves.", "這就是這間工作室的核心。我希望大家能實驗、學習，並帶走自己親手塑形的作品。");
        Add(Zh, "Let's start!", "我們開始吧！");
        Add(Zh, "Welcome to my pottery studio. Inside, you can try a Taiwanese lantern, an Indian teapot, or an Indonesian keris — all in clay. Shall we go in?", "歡迎來到我的陶藝工作室。裡面可以試試台灣天燈、印度茶壺，或印尼克里斯劍——都是陶土做的。要進去嗎？");
        Add(Zh, "Enter the studio", "進入工作室");
        Add(Zh, "Everything here is about clay. Come into the studio and pick a craft to shape.", "這裡的一切都與陶土有關。進工作室挑選一件工藝來塑形吧。");
        Add(Zh, "Welcome to my pottery studio! Here, you can explore different artistic traditions through clay. Which creation would you like to try?", "歡迎來到我的陶藝工作室！在這裡，你可以透過陶土探索不同的藝術傳統。想試試哪一件作品？");
        Add(Zh, "Let's begin with Taiwan. This piece is inspired by the traditional sky lanterns that have become such a beautiful part of Taiwanese culture. But this isn't a finished lantern. I've given you the basic form. You decide what it becomes.", "我們從台灣開始。這件作品靈感來自已成為台灣文化美麗一部分的傳統天燈。但這還不是完成的燈。我給了你基本形狀。它會變成什麼，由你決定。");
        Add(Zh, "India has a long and diverse history of pottery and ceramic art. I wanted to capture some of that inspiration in this piece. Here's the basic form. Shape it however you like.", "印度有悠久而多元的陶藝與陶瓷史。我想把那樣的靈感放進這件作品。這是基本形狀。隨你喜歡去塑形。");
        Add(Zh, "Indonesia was another important part of my art studies. Its traditional objects and sculptural forms inspired me in a completely different way. Let's see what you can create from this base.", "印尼也是我藝術學習裡重要的一部分。它的傳統器物與雕塑形式，用完全不同的方式啟發我。看看你能從這個胚體做出什麼。");
        Add(Zh, "Don't want to follow a particular tradition? That's completely fine. This time, the clay is yours.", "不想跟隨特定傳統？完全沒問題。這次，陶土是你的。");
        Add(Zh, "Start Sculpting", "開始雕塑");
        Add(Zh, "START SCULPTING", "開始雕塑");
        Add(Zh, "Create Freely", "自由創作");
        Add(Zh, "GESTURE: NO HANDS", "手勢：沒有手");
        Add(Zh, "GESTURE: NONE", "手勢：無");
        Add(Zh, "GESTURE: FIST REVOLVE", "手勢：拳頭旋轉");
        Add(Zh, "RESET ROTATION", "重設旋轉");
        Add(Zh, "GESTURES", "手勢");
        Add(Zh, "ZOOM", "放大");
        Add(Zh, "ZOOM OUT", "縮小");
        Add(Zh, "NO HANDS", "沒有手");
        Add(Zh, "FIST", "拳頭");
        Add(Zh, "TWO V", "雙手 V");
        Add(Zh, "TWO FISTS", "雙拳");
        Add(Zh, "RIGHT V", "右手 V");
        Add(Zh, "LEFT V", "左手 V");
        Add(Zh, "RIGHT PINCH", "右手捏");
        Add(Zh, "LEFT PINCH", "左手捏");
        Add(Zh, "OPEN PALM", "張開手掌");
        Add(Zh, "WIDTH & HEIGHT", "寬度與高度");
        Add(Zh, "AI HELP", "AI 說明");
        Add(Zh, "HELP", "說明");
        Add(Zh, "Ask Lisa anything...", "問麗莎任何問題...");
        Add(Zh, "LISA: Type a question first.", "麗莎：請先輸入問題。");
        Add(Zh, "LISA: Asking...", "麗莎：正在詢問...");
        Add(Zh, "LISA: Could not reach help. Try again.", "麗莎：暫時無法連線。請再試一次。");
        Add(Zh, "LISA: Help is waking up. Send again.", "麗莎：幫忙正在啟動。請再送一次。");
        Add(Zh, "SEND", "送出");
        Add(Zh, "ASK LISA", "問麗莎");
        Add(Zh, "COLOR PALETTE", "調色盤");
        Add(Zh, "SELECT PART", "選擇部位");
        Add(Zh, "VERTEX SCULPT", "頂點雕塑");
        Add(Zh, "FINISH", "完成");
        Add(Zh, "DONE", "完成");
        Add(Zh, "UNDO", "復原");
        Add(Zh, "REDO", "重做");
        Add(Zh, "RESET", "重設");
        Add(Zh, "CANCEL", "取消");
        Add(Zh, "DELETE", "刪除");
        Add(Zh, "TAKE BACK", "取回");
        Add(Zh, "RETAKE", "重拍");
        Add(Zh, "SELFIE", "自拍");
        Add(Zh, "TAKE SELFIE", "拍自拍");
        Add(Zh, "ATTACH TO TEAPOT", "貼到茶壺");
        Add(Zh, "ATTACH TO LANTERN", "貼到天燈");
        Add(Zh, "ATTACH TO KERIS", "貼到克里斯劍");
        Add(Zh, "REMOVE SELFIE", "移除自拍");
        Add(Zh, "BACK", "返回");
        Add(Zh, "SKIP", "略過");
        Add(Zh, "REPLAY", "重播");
        Add(Zh, "PREVIOUS", "上一步");
        Add(Zh, "NEXT", "下一步");
        Add(Zh, "GOOD", "很好");
        Add(Zh, "MORE COLORS", "更多顏色");
        Add(Zh, "CLAY COLOR", "陶土顏色");
        Add(Zh, "SIZE", "大小");
        Add(Zh, "STRENGTH", "強度");
        Add(Zh, "COLOR", "顏色");
        Add(Zh, "SHAPE", "形狀");
        Add(Zh, "DESIGN", "圖案");
        Add(Zh, "ADD DESIGN", "新增圖案");
        Add(Zh, "Design cleared.", "已清除圖案。");
        Add(Zh, "That stamp is missing.", "找不到這個印章。");
        Add(Zh, "DRAW BAND", "繪製飾帶");
        Add(Zh, "← BACK", "← 返回");
        Add(Zh, "← SCULPT", "← 雕塑");
        Add(Zh, "← PARTS", "← 部位");
        Add(Zh, "← DESIGNS", "← 圖案");
        Add(Zh, "← BODY", "← 本體");
        Add(Zh, "← SHAPE", "← 形狀");
        Add(Zh, "← DRAW", "← 繪製");
        Add(Zh, "← MOTIFS", "← 紋樣");
        Add(Zh, "Whole Lantern", "整盞天燈");
        Add(Zh, "Lantern Body", "燈身");
        Add(Zh, "Lantern Top", "燈頂");
        Add(Zh, "Lantern Bottom", "燈底");
        Add(Zh, "Lantern Tassel", "燈穗");
        Add(Zh, "Circle", "圓形");
        Add(Zh, "Rectangle", "方形");
        Add(Zh, "Single Tassel", "單穗");
        Add(Zh, "3 Tassel", "三穗");
        Add(Zh, "Spout 1", "壺嘴 1");
        Add(Zh, "Spout 2", "壺嘴 2");
        Add(Zh, "Handle 1", "壺把 1");
        Add(Zh, "Handle 2", "壺把 2");
        Add(Zh, "Lid 1", "壺蓋 1");
        Add(Zh, "Lid 2", "壺蓋 2");
        Add(Zh, "DESIGNS", "圖案");
        Add(Zh, "LANTERN BODY", "燈身");
        Add(Zh, "SELECT SHAPE", "選擇形狀");
        Add(Zh, "SELECT PART", "選擇部位");
        Add(Zh, "Whole Teapot", "整把茶壺");
        Add(Zh, "Teapot Body", "壺身");
        Add(Zh, "Teapot Lid", "壺蓋");
        Add(Zh, "Teapot Spout", "壺嘴");
        Add(Zh, "Teapot Handle", "壺把");
        Add(Zh, "KERIS BODY", "劍身");
        Add(Zh, "KERIS CARVING", "雕刻");
        Add(Zh, "KERIS HANDLE", "劍柄");
        Add(Zh, "Choose the whole lantern or one part.", "選擇整盞天燈或其中一個部位。");
        Add(Zh, "Choose the whole teapot or one part.", "選擇整把茶壺或其中一個部位。");
        Add(Zh, "Choose the body, a carving, or the handle.", "選擇劍身、雕刻或劍柄。");
        Add(Zh, "Choose a shape first.", "請先選擇形狀。");
        Add(Zh, "WIDTH & HEIGHT", "寬度與高度");
        Add(Zh, "TOP LID", "上蓋");
        Add(Zh, "BOTTOM LID", "下蓋");
        Add(Zh, "TASSEL", "燈穗");
        Add(Zh, "BAMBOO", "竹框");
        Add(Zh, "ROTATE", "旋轉");
        Add(Zh, "TOPPER", "頂飾");
        Add(Zh, "BOTTOM", "底部");
        Add(Zh, "LOCK", "鎖定");
        Add(Zh, "LID", "壺蓋");
        Add(Zh, "SPOUT", "壺嘴");
        Add(Zh, "HANDLE", "握把");
        Add(Zh, "MIDDLE", "中段");
        Add(Zh, "BEVEL", "斜邊");
        Add(Zh, "SHARPNESS", "鋒利");
        Add(Zh, "CURVE", "彎曲");
        Add(Zh, "CARVING", "雕刻");
        Add(Zh, "Point both index fingers. Move them apart or together, and up or down.", "伸出兩手食指。分開或靠攏，也可上下移動。");
        Add(Zh, "Pinch with your right hand, then lift.", "用右手捏合，然後上提。");
        Add(Zh, "Pinch with your left hand, then lift.", "用左手捏合，然後上提。");
        Add(Zh, "Pinch with your right hand. Lift to open, lower to close.", "用右手捏合。上提打開，放下收回。");
        Add(Zh, "Pinch with your left hand. Lift to open, lower to close.", "用左手捏合。上提打開，放下收回。");
        Add(Zh, "Make two fists and pull them down.", "握兩個拳頭，再往下拉。");
        Add(Zh, "Make a V with both hands and spread them to the sides.", "兩手比出 V 字，再向兩側張開。");
        Add(Zh, "Make one fist and move it left, right, up, or down.", "握一個拳頭，向左、右、上或下移動。");
        Add(Zh, "Make a V with your right hand and raise or lower it.", "右手比出 V 字，再上舉或放下。");
        Add(Zh, "Make a V with your left hand and raise or lower it.", "左手比出 V 字，再上舉或放下。");
        Add(Zh, "After you change the lantern, open your palm to lock the shape.", "改變天燈後，張開手掌鎖定形狀。");
        Add(Zh, "Pinch with your right hand, then lift to raise the lid.", "用右手捏合，再上提以升起壺蓋。");
        Add(Zh, "Pinch with your left hand, then lift to pull the spout.", "用左手捏合，再上提以拉出壺嘴。");
        Add(Zh, "After you change the teapot, open your palm to rest.", "改變茶壺後，張開手掌休息。");
        Add(Zh, "Pinch with your right hand, then lift to sharpen the blade.", "用右手捏合，再上提讓刀刃更鋒利。");
        Add(Zh, "Pinch with your left hand, then lift to curve the blade.", "用左手捏合，再上提讓刀刃彎曲。");
        Add(Zh, "After you change the keris, open your palm to rest.", "改變克里斯劍後，張開手掌休息。");
        Add(Zh, "GESTURE {0} OF {1}", "手勢 {0} / {1}");
        Add(Zh, "Frame your photo, then take a selfie.", "對好畫面，然後自拍。");
        Add(Zh, "Last clay coil undone.", "已復原上一圈陶土。");
        Add(Zh, "Last sculpt stroke undone.", "已復原上一筆雕塑。");
        Add(Zh, "Last drawing stroke undone.", "已復原上一筆繪圖。");
        Add(Zh, "Last change undone.", "已復原上一次變更。");
        Add(Zh, "Last clay coil redone.", "已重做上一圈陶土。");
        Add(Zh, "Last sculpt stroke redone.", "已重做上一筆雕塑。");
        Add(Zh, "Last drawing stroke redone.", "已重做上一筆繪圖。");
        Add(Zh, "Last change redone.", "已重做上一次變更。");
        Add(Zh, "Nothing to undo.", "沒有可復原的步驟。");
        Add(Zh, "The shelf is full.", "架子已經滿了。");
        Add(Zh, "LOCKED. Point both index fingers to unlock.", "已鎖定。伸出兩手食指解鎖。");
        Add(Zh, "CHANGE COLOR", "更換顏色");
        Add(Zh, "CLEAR DESIGN", "清除圖案");
        Add(Zh, "CLEAR DRAWINGS", "清除繪圖");
        Add(Zh, "BAND COLOR", "飾帶顏色");
        Add(Zh, "BAND WIDTH", "飾帶寬度");
        Add(Zh, "SLIM", "細");
        Add(Zh, "MEDIUM", "中");
        Add(Zh, "BOLD", "粗");
        Add(Zh, "WIDE", "寬");
        Add(Zh, "Stamp 1", "印章 1");
        Add(Zh, "Stamp 2", "印章 2");
        Add(Zh, "Stamp 3", "印章 3");
        Add(Zh, "Stamp 4", "印章 4");
        Add(Zh, "BATIK", "蠟染");
        Add(Zh, "BRAIDED", "編織");
        Add(Zh, "CHEVRON", "人字紋");
        Add(Zh, "DIAMONDS", "菱形");
        Add(Zh, "LOTUS", "蓮花");
        Add(Zh, "PEARLS", "珍珠");
        Add(Zh, "STRAIGHT", "直線");
        Add(Zh, "WAVE", "波浪");
        Add(Zh, "Change the paper color, or add a stamped design.", "更換紙色，或加上印章圖案。");
        Add(Zh, "Change the paper color, or add another design.", "更換紙色，或再加一個圖案。");
        Add(Zh, "Point and hold until the border completes.", "指向顏色並按住，直到外框完成。");
        Add(Zh, "Paint and hold until the border completes.", "指向顏色並按住，直到外框完成。");
        Add(Zh, "Point at a color to tint the stamp.", "指向顏色為印章上色。");
        Add(Zh, "Point and hold on a color to tint the band.", "指向顏色並按住，為飾帶上色。");
        Add(Zh, "Choose a stamp. Then tint it with Change Color.", "選一個印章，再用「更換顏色」上色。");
        Add(Zh, "No stamps found in StampPatterns.", "找不到印章圖案。");
        Add(Zh, "Select a motif, adjust size & color, then trace on lantern.", "選一個紋樣，調整大小與顏色，再沿著天燈描繪。");
        Add(Zh, "Select a motif, adjust size & color, then trace on the lantern.", "選一個紋樣，調整大小與顏色，再沿著天燈描繪。");
        Add(Zh, "Choose Circle or Rectangle, then pinch and lift.", "選擇圓形或方形，然後捏合上提。");
        Add(Zh, "Choose one tassel or three tassels.", "選擇單穗或三穗。");
        Add(Zh, "Attach the photo to the lantern, retake it, or press DONE.", "把照片貼上天燈、重拍，或按完成。");
        Add(Zh, "Remove the selfie, retake it, or press DONE.", "移除自拍、重拍，或按完成。");
        Add(Zh, "Frame your photo, then point and hold TAKE SELFIE.", "對好畫面，然後指向並按住「拍自拍」。");
        Add(Zh, "All drawings cleared.", "已清除所有繪圖。");
        Add(Zh, " is already showing. Change color, or pick another shape.", " 已顯示。可換顏色，或選別的形狀。");
        Add(Zh, "Pinch with your right hand and lift to pop the shape.", "用右手捏合上提，頂出形狀。");
        Add(Zh, "Pinch with your left hand and lift to pop the shape.", "用左手捏合上提，頂出形狀。");
        Add(Zh, "Make two fists and pull them down to bring the tassel out.", "握兩個拳頭往下拉，讓燈穗出來。");
        Add(Zh, "Stamp tint selected. Choose another color or press DONE.", "已選印章顏色。可再選顏色，或按完成。");
        Add(Zh, "Color selected. Choose another color or press DONE.", "已選顏色。可再選顏色，或按完成。");
        Add(Zh, "Change the pot color, or add a stamped design.", "更換壺色，或加上印章圖案。");
        Add(Zh, "Change the pot color, or add another design.", "更換壺色，或再加一個圖案。");
        Add(Zh, "TEAPOT BODY", "壺身");
        Add(Zh, "Attach the photo to the teapot, retake it, or press DONE.", "把照片貼上茶壺、重拍，或按完成。");
        Add(Zh, "Attach the photo to the keris, retake it, or press DONE.", "把照片貼上克里斯劍、重拍，或按完成。");
        Add(Zh, "STAMP COLOR", "印章顏色");
        Add(Zh, "Pick a clay color.", "選一個陶土顏色。");
        Add(Zh, "Pick a tint for the stamp.", "選一個印章顏色。");
        Add(Zh, "Pick a color for the drawn band.", "選一個飾帶顏色。");
        Add(Zh, "Pick a color for the keris.", "選一個克里斯劍顏色。");
        Add(Zh, "Change the blade color, add a stamped design, or draw a band.", "更換刀刃顏色、加上印章，或繪製飾帶。");
        Add(Zh, "Change the blade color, add another stamp, or draw a band.", "更換刀刃顏色、再加印章，或繪製飾帶。");
        Add(Zh, "ATTACH TO LANTERN", "貼到天燈");
        Add(Zh, "RETAKE", "重拍");
        Add(Zh, "REMOVE SELFIE", "移除自拍");
        Add(Zh, "BRUSH SIZE", "筆刷大小");
        Add(Zh, "CLAY COLOR", "陶土顏色");
        Add(Zh, "SOFT", "柔");
        Add(Zh, "MED", "中等");
        Add(Zh, "STRONG", "強");
        Add(Zh, "MAX", "最大");
        Add(Zh, "S", "小");
        Add(Zh, "M", "中");
        Add(Zh, "L", "大");
        Add(Zh, "XL", "特大");
        Add(Zh, "HANDLE 1", "劍柄 1");
        Add(Zh, "HANDLE 2", "劍柄 2");
        Add(Zh, "CARVING 1", "雕刻 1");
        Add(Zh, "CARVING 2", "雕刻 2");
        Add(Zh, "Carving 1", "雕刻 1");
        Add(Zh, "Carving 2", "雕刻 2");
        Add(Zh, "Choose Handle 1 or Handle 2.", "選擇壺把 1 或壺把 2。");
        Add(Zh, "Choose HANDLE 1 or HANDLE 2.", "選擇劍柄 1 或劍柄 2。");
        Add(Zh, "Choose Spout 1 or Spout 2.", "選擇壺嘴 1 或壺嘴 2。");
        Add(Zh, "Choose Lid 1 or Lid 2.", "選擇壺蓋 1 或壺蓋 2。");
        Add(Zh, "Choose Carving 1 or Carving 2.", "選擇雕刻 1 或雕刻 2。");
        Add(Zh, "Pinch with your right hand and lift to raise the lid.", "用右手捏合上提，升起壺蓋。");
        Add(Zh, "Pinch with your left hand and lift to pull the spout.", "用左手捏合上提，拉出壺嘴。");
        Add(Zh, "Make two fists and pull them down to bring the handle out.", "握兩個拳頭往下拉，讓握把出來。");
        Add(Zh, "Make two V signs and spread them outwards to bring the carving out.", "兩手比出 V 字再向外張開，讓雕刻出來。");
        Add(Zh, "Select a motif, adjust size & color, then trace on the keris.", "選一個紋樣，調整大小與顏色，再沿著克里斯劍描繪。");
        Add(Zh, "Select a motif, adjust size & color, then trace on teapot.", "選一個紋樣，調整大小與顏色，再沿著茶壺描繪。");
        Add(Zh, "Choose a stamp first.", "請先選擇印章。");
        Add(Zh, "Choose a color or sculpt clay.", "選擇顏色，或雕塑陶土。");
        Add(Zh, "Point both index fingers for width and height. Right pinch up for sharpness, left pinch up for curve. Open palm to lock.", "伸出兩手食指調整寬高。右手捏合上提讓刀刃更鋒利，左手捏合上提讓刀刃彎曲。張開手掌鎖定。");
        Add(Zh, " selected.", " 已選取。");
        Add(Zh, "REVIEW", "檢視");
        Add(Zh, "SHELF", "架子");
        Add(Zh, "← PALETTE", "← 調色盤");
        Add(Zh, "← REVIEW", "← 檢視");
        Add(Zh, "← BAND", "← 飾帶");
        Add(Zh, "Go back, or put this teapot on the shelf.", "返回，或把這把茶壺放到架子上。");
        Add(Zh, "Go back, or put this lantern on the shelf.", "返回，或把這盞天燈放到架子上。");
        Add(Zh, "Go back, or put this keris on the shelf.", "返回，或把這把克里斯劍放到架子上。");
        Add(Zh, "Take it back, delete it, or go back when you are done.", "取回、刪除，或完成後返回。");
        Add(Zh, "Take it back, or go back when you are done.", "取回，或完成後返回。");
        Add(Zh, "This removes the teapot from the shelf.", "這會把茶壺從架子上取下。");
        Add(Zh, "This removes the lantern from the shelf.", "這會把天燈從架子上取下。");
        Add(Zh, "This removes the keris from the shelf.", "這會把克里斯劍從架子上取下。");
        Add(Zh, "The shelf is not ready yet.", "架子還沒準備好。");
        Add(Zh, "Could not place this teapot on the shelf.", "無法把這把茶壺放到架子上。");
        Add(Zh, "Could not place this lantern on the shelf.", "無法把這盞天燈放到架子上。");
        Add(Zh, "Could not place this keris on the shelf.", "無法把這把克里斯劍放到架子上。");
        Add(Zh, "This clears sculpt, color, design, and selfie.", "這會清除雕塑、顏色、圖案與自拍。");
        Add(Zh, "This clears sculpt, color, and stamp.", "這會清除雕塑、顏色與印章。");
        Add(Zh, "Teapot reset.", "茶壺已重設。");
        Add(Zh, "Clay color selected. Pick another color or press DONE.", "已選陶土顏色。可再選顏色，或按完成。");
        Add(Zh, "Band color selected. Pick another color or press DONE.", "已選飾帶顏色。可再選顏色，或按完成。");
        Add(Zh, "Point and hold on a color to tint the clay.", "指向顏色並按住，為陶土上色。");
        Add(Zh, "Point and hold on a color to tint the band designs.", "指向顏色並按住，為飾帶圖案上色。");
        Add(Zh, "Trace index finger across teapot to draw. Open palm to lock.", "用食指沿著茶壺描繪。張開手掌鎖定。");
        Add(Zh, "Trace index finger across the keris to draw. Open palm to lock.", "用食指沿著克里斯劍描繪。張開手掌鎖定。");
        Add(Zh, "Trace index finger across lantern to draw. Open palm to lock.", "用食指沿著天燈描繪。張開手掌鎖定。");
        Add(Zh, "DRAWING LOCKED (Open palm detected). Show open palm again to unlock, or tap <- MOTIFS.", "繪圖已鎖定（偵測到張開手掌）。再張開手掌解鎖，或點「← 紋樣」。");
        Add(Zh, "DRAWING LOCKED (Open palm detected). Show open palm again to unlock, or tap ← MOTIFS.", "繪圖已鎖定（偵測到張開手掌）。再張開手掌解鎖，或點「← 紋樣」。");
        Add(Zh, "DRAWING... Trace index finger across teapot to draw!", "繪製中...用食指沿著茶壺描繪！");
        Add(Zh, "DRAWING... Trace index finger across lantern to draw!", "繪製中...用食指沿著天燈描繪！");
        Add(Zh, "Point index finger to draw. Open palm to lock.", "伸出食指描繪。張開手掌鎖定。");
        Add(Zh, "Aim at teapot. Point index finger to draw, open palm to lock.", "對準茶壺。伸出食指描繪，張開手掌鎖定。");
        Add(Zh, "Aim at lantern. Point index finger to draw, open palm to lock.", "對準天燈。伸出食指描繪，張開手掌鎖定。");
        Add(Zh, "Last stroke undone.", "已復原上一筆。");
        Add(Zh, "Last stroke redone.", "已重做上一筆。");
        Add(Zh, "Nothing to redo.", "沒有可重做的步驟。");
        Add(Zh, "Camera is not ready yet.", "相機還沒準備好。");
        Add(Zh, "Camera is not ready yet. Please try again in a moment.", "相機還沒準備好。請稍後再試。");
        Add(Zh, "Could not capture camera image.", "無法擷取相機畫面。");
        Add(Zh, "Could not capture the camera image. Please try again.", "無法擷取相機畫面。請再試一次。");
        Add(Zh, "Take a selfie before attaching it.", "請先自拍再貼上。");
        Add(Zh, "Take a selfie before attaching it to the lantern.", "請先自拍再貼上天燈。");
        Add(Zh, "Selfie attached to the teapot body.", "自拍已貼到壺身。");
        Add(Zh, "Selfie attached to the lantern body.", "自拍已貼到燈身。");
        Add(Zh, "Selfie removed.", "已移除自拍。");
        Add(Zh, "Selfie restored.", "已還原自拍。");
        Add(Zh, "Selfie removed. Take a new one, or press BACK.", "已移除自拍。可再拍一張，或按返回。");
        Add(Zh, " applied. Change Color tints the stamp.", " 已套用。用「更換顏色」為印章上色。");
        Add(Zh, "PATTERN", "圖案");
        Add(Zh, "BAND", "飾帶");
        Add(Zh, "BAND {0}", "飾帶 {0}");
        Add(Zh, "SIZE: {0}", "大小：{0}");
        Add(Zh, "DRAW: {0}", "繪製：{0}");
        Add(Zh, "Reset the Keris and clear your current decorations?", "重設克里斯劍並清除目前的裝飾？");
        Add(Zh, "SCULPT MODE", "雕塑模式");
        Add(Zh, "PAINT MODE", "上色模式");
        Add(Zh, "CLEAR", "清除");
        Add(Zh, "SIZE: SLIM", "大小：細");
        Add(Zh, "SIZE: MEDIUM", "大小：中");
        Add(Zh, "SIZE: BOLD", "大小：粗");
        Add(Zh, "SIZE: WIDE", "大小：寬");
        Add(Zh, "LISA:", "麗莎：");
        Add(Zh, "lantern", "天燈");
        Add(Zh, "teapot", "陶壺");
        Add(Zh, "keris", "克里斯劍");
        Add(Zh, "sculpture", "作品");
        Add(Zh, "and", "和");
        Add(Zh, "stamp", "印章");
        Add(Zh, "painted band", "彩帶");
        Add(Zh, "width", "寬度");
        Add(Zh, "height", "高度");
        Add(Zh, "color", "顏色");
        Add(Zh, "wide body", "寬身");
        Add(Zh, "tall form", "高挑造型");
        Add(Zh, "soft middle", "柔和腰身");
        Add(Zh, "beveled rim", "斜切壺口");
        Add(Zh, "expressive lid", "有個性的蓋子");
        Add(Zh, "proud spout", "挺拔壺嘴");
        Add(Zh, "curved handle", "彎把手");
        Add(Zh, "wide blade", "寬刃");
        Add(Zh, "long blade", "長刃");
        Add(Zh, "strong carving", "深雕花紋");
        Add(Zh, "expressive handle", "有個性的握把");
        Add(Zh, "lifted top", "上揚頂部");
        Add(Zh, "full bottom", "飽滿底部");
        Add(Zh, "open lid", "開展燈蓋");
        Add(Zh, "deep base", "厚實底座");
        Add(Zh, "bamboo detail", "竹飾");
        Add(Zh, "proud tassel", "飄逸穗子");
        Add(Zh, "bold {0}", "大膽的{0}");
        Add(Zh, "gentle {0}", "柔和的{0}");
        Add(Zh, "Your {0} looks wonderful.", "你的{0}看起來很棒。");
        Add(Zh, "Your {0} is coming together so nicely.", "你的{0}愈來愈有味道了。");
        Add(Zh, "I really like what you made with this {0}.", "我真的很喜歡你做的這件{0}。");
        Add(Zh, "The shape feels generous — especially the {0}.", "造型很大氣，尤其是{0}。");
        Add(Zh, "That {0} is a beautiful color choice.", "{0}這個顏色選得很美。");
        Add(Zh, "The stamp design sits beautifully on it.", "印章圖案貼得很漂亮。");
        Add(Zh, "The {0} design sits beautifully on it.", "{0}圖案貼得很漂亮。");
        Add(Zh, "Your selfie makes this piece feel personal.", "加上自拍，這件作品更有你的味道。");
        Add(Zh, "The painted bands add a handmade rhythm.", "彩帶為它加上手作的節奏。");
        Add(Zh, "The clay coils give it real pottery character.", "泥條讓它更有陶藝的個性。");
        Add(Zh, "The little sculpted details make it one of a kind.", "那些雕塑細節讓它獨一無二。");
        Add(Zh, "Keep going — this already feels like it belongs in a collection.", "繼續做下去吧——這件已經很有收藏的感覺。");
        Add(Zh, "If you want to push it further, try a bolder {0} or another color you love.", "若想再推進一步，可以試試更大膽的{0}，或再加一個你喜歡的顏色。");
        Add(Zh, "You're just getting started, and that's a great place to be. Shape it a bit more, then try a color you love — even a small change will make it feel like yours.", "你才剛開始，這正是最棒的時候。先再塑一點形，再選一個喜歡的顏色——小小改變就會讓它變成你的作品。");
        Add(Zh, "terracotta", "陶土紅");
        Add(Zh, "warm red", "暖紅");
        Add(Zh, "deep red", "深紅");
        Add(Zh, "coral", "珊瑚橘");
        Add(Zh, "orange", "橘色");
        Add(Zh, "gold", "金色");
        Add(Zh, "amber", "琥珀");
        Add(Zh, "yellow", "黃色");
        Add(Zh, "fresh green", "翠綠");
        Add(Zh, "forest green", "深綠");
        Add(Zh, "teal", "青藍");
        Add(Zh, "bright blue", "亮藍");
        Add(Zh, "deep blue", "深藍");
        Add(Zh, "purple", "紫色");
        Add(Zh, "pink", "粉色");
        Add(Zh, "magenta", "洋紅");
        Add(Zh, "cream", "乳白色");
        Add(Zh, "soft gray", "淺灰");
        Add(Zh, "charcoal", "炭灰");
        Add(Zh, "deep black", "深黑");
    }

    private static void FillId()
    {
        Add(Id, "START", "MULAI");
        Add(Id, "LANGUAGE", "BAHASA");
        Add(Id, "SETTINGS", "PENGATURAN");
        Add(Id, "MAIN MENU", "MENU UTAMA");
        Add(Id, "LISA VOICE: ON", "SUARA LISA: NYALA");
        Add(Id, "LISA VOICE: OFF", "SUARA LISA: MATI");
        Add(Id, "Point your right index finger at the lantern and move it to push the clay.", "Arahkan jari telunjuk kanan ke lentera dan gerakkan untuk mendorong tanah liat.");
        Add(Id, "Point your right index finger at the teapot and move it to push the clay.", "Arahkan jari telunjuk kanan ke teko dan gerakkan untuk mendorong tanah liat.");
        Add(Id, "Point your right index finger at the keris and move it to push the clay.", "Arahkan jari telunjuk kanan ke keris dan gerakkan untuk mendorong tanah liat.");
        Add(Id, "Point your right index finger and trace a band. Open your palm to lock.", "Arahkan jari telunjuk kanan dan gambar pita. Buka telapak untuk mengunci.");
        Add(Id, "EXIT", "KELUAR");
        Add(Id, "ENGLISH", "INGGRIS");
        Add(Id, "TAIWANESE", "MANDARIN");
        Add(Id, "TW", "TW");
        Add(Id, "INDONESIAN", "INDONESIA");
        Add(Id, "Music", "Musik");
        Add(Id, "MUSIC", "MUSIK");
        Add(Id, "SFX", "SFX");
        Add(Id, "Sound Effects", "Efek Suara");
        Add(Id, "CHOOSE A CRAFT", "PILIH KERAJINAN");
        Add(Id, "Taiwan", "Taiwan");
        Add(Id, "India", "India");
        Add(Id, "Indonesia", "Indonesia");
        Add(Id, "LISA", "LISA");
        Add(Id, "Hi! I'm Lisa. Nice to meet you!", "Hai! Aku Lisa. Senang bertemu denganmu!");
        Add(Id, "Hi, Lisa!", "Hai, Lisa!");
        Add(Id, "Where are we?", "Kita di mana?");
        Add(Id, "It's wonderful to finally be back in Yuanlin. I opened a pottery studio here so I can work with clay every day.", "Senang sekali akhirnya kembali ke Yuanlin. Aku membuka studio tembikar di sini agar bisa bekerja dengan tanah liat setiap hari.");
        Add(Id, "Tell me more", "Ceritakan lagi");
        Add(Id, "Why pottery?", "Mengapa tembikar?");
        Add(Id, "We're in Yuanlin, my hometown. This pottery studio is my new home for making art with clay.", "Kita di Yuanlin, kampung halamanku. Studio tembikar ini rumah baruku untuk berkarya dengan tanah liat.");
        Add(Id, "Your journey", "Perjalananmu");
        Add(Id, "Yuanlin is full of little workshops and people who still make things by hand. I wanted my studio to be one of those places — a quiet corner for clay.", "Yuanlin penuh bengkel kecil dan orang yang masih membuat barang dengan tangan. Aku ingin studiku menjadi salah satu tempat itu — sudut tenang untuk tanah liat.");
        Add(Id, "That's lovely", "Itu indah");
        Add(Id, "So why pottery?", "Jadi mengapa tembikar?");
        Add(Id, "Because clay lets you turn an idea into something you can actually hold. During my studies I saw how differently cultures shape the same material.", "Karena tanah liat mengubah ide menjadi sesuatu yang bisa dipegang. Saat belajar, aku melihat betapa berbedanya budaya membentuk bahan yang sama.");
        Add(Id, "What did you learn?", "Apa yang kamu pelajari?");
        Add(Id, "Let's make something!", "Ayo membuat sesuatu!");
        Add(Id, "I studied art in India and Indonesia. Their sculpting traditions inspired me to create a studio where those cultures can meet through clay — along with the lantern craft I grew up with in Taiwan.", "Aku belajar seni di India dan Indonesia. Tradisi patung mereka menginspirasiku membuat studio tempat budaya itu bertemu lewat tanah liat — bersama kerajinan lampion yang kukenal sejak kecil di Taiwan.");
        Add(Id, "I want to see!", "Aku ingin melihat!");
        Add(Id, "Amazing!", "Luar biasa!");
        Add(Id, "That's the heart of this studio. I want people to experiment, learn, and leave with something they shaped themselves.", "Itulah jantung studio ini. Aku ingin orang bereksperimen, belajar, dan pulang dengan karya yang mereka bentuk sendiri.");
        Add(Id, "Let's start!", "Ayo mulai!");
        Add(Id, "Welcome to my pottery studio. Inside, you can try a Taiwanese lantern, an Indian teapot, or an Indonesian keris — all in clay. Shall we go in?", "Selamat datang di studio tembikarku. Di dalam, kamu bisa mencoba lampion Taiwan, teko India, atau keris Indonesia — semuanya dari tanah liat. Masuk yuk?");
        Add(Id, "Enter the studio", "Masuk studio");
        Add(Id, "Everything here is about clay. Come into the studio and pick a craft to shape.", "Semua di sini tentang tanah liat. Masuk studio dan pilih kerajinan untuk dibentuk.");
        Add(Id, "Welcome to my pottery studio! Here, you can explore different artistic traditions through clay. Which creation would you like to try?", "Selamat datang di studio tembikarku! Di sini kamu bisa menjelajahi tradisi seni yang berbeda lewat tanah liat. Karya mana yang ingin kamu coba?");
        Add(Id, "Let's begin with Taiwan. This piece is inspired by the traditional sky lanterns that have become such a beautiful part of Taiwanese culture. But this isn't a finished lantern. I've given you the basic form. You decide what it becomes.", "Kita mulai dari Taiwan. Karya ini terinspirasi lampion langit tradisional yang menjadi bagian indah budaya Taiwan. Tapi ini bukan lampion jadi. Aku memberimu bentuk dasar. Kamu yang menentukan jadinya.");
        Add(Id, "India has a long and diverse history of pottery and ceramic art. I wanted to capture some of that inspiration in this piece. Here's the basic form. Shape it however you like.", "India punya sejarah tembikar dan keramik yang panjang dan beragam. Aku ingin menangkap inspirasi itu di karya ini. Ini bentuk dasarnya. Bentuk sesukamu.");
        Add(Id, "Indonesia was another important part of my art studies. Its traditional objects and sculptural forms inspired me in a completely different way. Let's see what you can create from this base.", "Indonesia juga bagian penting studiku. Benda tradisional dan bentuk patungnya menginspirasiku dengan cara yang sama sekali berbeda. Mari lihat apa yang bisa kamu buat dari dasar ini.");
        Add(Id, "Don't want to follow a particular tradition? That's completely fine. This time, the clay is yours.", "Tidak ingin mengikuti tradisi tertentu? Tidak apa-apa. Kali ini, tanah liat ini milikmu.");
        Add(Id, "Start Sculpting", "Mulai Memahat");
        Add(Id, "START SCULPTING", "MULAI MEMAHAT");
        Add(Id, "Create Freely", "Berkarya Bebas");
        Add(Id, "GESTURE: NO HANDS", "GERAKAN: TIDAK ADA TANGAN");
        Add(Id, "GESTURE: NONE", "GERAKAN: TIDAK ADA");
        Add(Id, "GESTURE: FIST REVOLVE", "GERAKAN: PUTAR TINJU");
        Add(Id, "RESET ROTATION", "ATUR ULANG PUTARAN");
        Add(Id, "GESTURES", "GERAKAN");
        Add(Id, "ZOOM", "PERBESAR");
        Add(Id, "ZOOM OUT", "KECILKAN");
        Add(Id, "NO HANDS", "TIDAK ADA TANGAN");
        Add(Id, "FIST", "KEPAL");
        Add(Id, "TWO V", "DUA V");
        Add(Id, "TWO FISTS", "DUA KEPAL");
        Add(Id, "RIGHT V", "V KANAN");
        Add(Id, "LEFT V", "V KIRI");
        Add(Id, "RIGHT PINCH", "CUBIT KANAN");
        Add(Id, "LEFT PINCH", "CUBIT KIRI");
        Add(Id, "OPEN PALM", "TELAKAP TERBUKA");
        Add(Id, "WIDTH & HEIGHT", "LEBAR & TINGGI");
        Add(Id, "AI HELP", "BANTUAN AI");
        Add(Id, "HELP", "BANTUAN");
        Add(Id, "Ask Lisa anything...", "Tanya Lisa apa saja...");
        Add(Id, "LISA: Type a question first.", "LISA: Ketik pertanyaan dulu.");
        Add(Id, "LISA: Asking...", "LISA: Sedang bertanya...");
        Add(Id, "LISA: Could not reach help. Try again.", "LISA: Bantuan tidak terjangkau. Coba lagi.");
        Add(Id, "LISA: Help is waking up. Send again.", "LISA: Bantuan sedang bangun. Kirim lagi.");
        Add(Id, "SEND", "KIRIM");
        Add(Id, "ASK LISA", "TANYA LISA");
        Add(Id, "COLOR PALETTE", "PALET WARNA");
        Add(Id, "SELECT PART", "PILIH BAGIAN");
        Add(Id, "VERTEX SCULPT", "PAHAT TITIK");
        Add(Id, "FINISH", "SELESAI");
        Add(Id, "DONE", "SELESAI");
        Add(Id, "UNDO", "URUNG");
        Add(Id, "REDO", "ULANG");
        Add(Id, "RESET", "ATUR ULANG");
        Add(Id, "CANCEL", "BATAL");
        Add(Id, "DELETE", "HAPUS");
        Add(Id, "TAKE BACK", "AMBIL KEMBALI");
        Add(Id, "RETAKE", "FOTO ULANG");
        Add(Id, "SELFIE", "SELFIE");
        Add(Id, "TAKE SELFIE", "AMBIL SELFIE");
        Add(Id, "ATTACH TO TEAPOT", "TEMPPEL KE TEKO");
        Add(Id, "ATTACH TO LANTERN", "TEMPPEL KE LAMPION");
        Add(Id, "ATTACH TO KERIS", "TEMPPEL KE KERIS");
        Add(Id, "REMOVE SELFIE", "HAPUS SELFIE");
        Add(Id, "BACK", "KEMBALI");
        Add(Id, "SKIP", "LEWATI");
        Add(Id, "REPLAY", "ULANG");
        Add(Id, "PREVIOUS", "SEBELUM");
        Add(Id, "NEXT", "LANJUT");
        Add(Id, "GOOD", "BAGUS");
        Add(Id, "MORE COLORS", "WARNA LAIN");
        Add(Id, "CLAY COLOR", "WARNA TANAH LIAT");
        Add(Id, "SIZE", "UKURAN");
        Add(Id, "STRENGTH", "KEKUATAN");
        Add(Id, "COLOR", "WARNA");
        Add(Id, "SHAPE", "BENTUK");
        Add(Id, "DESIGN", "DESAIN");
        Add(Id, "ADD DESIGN", "TAMBAH DESAIN");
        Add(Id, "Design cleared.", "Desain dihapus.");
        Add(Id, "That stamp is missing.", "Stempel itu tidak ada.");
        Add(Id, "DRAW BAND", "GAMBAR PITA");
        Add(Id, "← BACK", "← KEMBALI");
        Add(Id, "← SCULPT", "← PAHAT");
        Add(Id, "← PARTS", "← BAGIAN");
        Add(Id, "← DESIGNS", "← DESAIN");
        Add(Id, "← BODY", "← BADAN");
        Add(Id, "← SHAPE", "← BENTUK");
        Add(Id, "← DRAW", "← GAMBAR");
        Add(Id, "← MOTIFS", "← MOTIF");
        Add(Id, "Whole Lantern", "Seluruh Lampion");
        Add(Id, "Lantern Body", "Badan Lampion");
        Add(Id, "Lantern Top", "Atas Lampion");
        Add(Id, "Lantern Bottom", "Bawah Lampion");
        Add(Id, "Lantern Tassel", "Rumbai Lampion");
        Add(Id, "Circle", "Lingkaran");
        Add(Id, "Rectangle", "Persegi");
        Add(Id, "Single Tassel", "Satu Rumbai");
        Add(Id, "3 Tassel", "Tiga Rumbai");
        Add(Id, "Spout 1", "Corong 1");
        Add(Id, "Spout 2", "Corong 2");
        Add(Id, "Handle 1", "Gagang 1");
        Add(Id, "Handle 2", "Gagang 2");
        Add(Id, "Lid 1", "Tutup 1");
        Add(Id, "Lid 2", "Tutup 2");
        Add(Id, "DESIGNS", "DESAIN");
        Add(Id, "LANTERN BODY", "BADAN LAMPION");
        Add(Id, "SELECT SHAPE", "PILIH BENTUK");
        Add(Id, "SELECT PART", "PILIH BAGIAN");
        Add(Id, "Whole Teapot", "Seluruh Teko");
        Add(Id, "Teapot Body", "Badan Teko");
        Add(Id, "Teapot Lid", "Tutup Teko");
        Add(Id, "Teapot Spout", "Corong Teko");
        Add(Id, "Teapot Handle", "Gagang Teko");
        Add(Id, "KERIS BODY", "BADAN KERIS");
        Add(Id, "KERIS CARVING", "UKIRAN KERIS");
        Add(Id, "KERIS HANDLE", "HULU KERIS");
        Add(Id, "Choose the whole lantern or one part.", "Pilih seluruh lampion atau satu bagian.");
        Add(Id, "Choose the whole teapot or one part.", "Pilih seluruh teko atau satu bagian.");
        Add(Id, "Choose the body, a carving, or the handle.", "Pilih badan, ukiran, atau hulu.");
        Add(Id, "Choose a shape first.", "Pilih bentuk dulu.");
        Add(Id, "WIDTH & HEIGHT", "LEBAR & TINGGI");
        Add(Id, "TOP LID", "TUTUP ATAS");
        Add(Id, "BOTTOM LID", "TUTUP BAWAH");
        Add(Id, "TASSEL", "RUMBAI");
        Add(Id, "BAMBOO", "BAMBU");
        Add(Id, "ROTATE", "PUTAR");
        Add(Id, "TOPPER", "PUNCAK");
        Add(Id, "BOTTOM", "BAWAH");
        Add(Id, "LOCK", "KUNCI");
        Add(Id, "LID", "TUTUP");
        Add(Id, "SPOUT", "CORONG");
        Add(Id, "HANDLE", "GAGANG");
        Add(Id, "MIDDLE", "TENGAH");
        Add(Id, "BEVEL", "SUDUT");
        Add(Id, "SHARPNESS", "KETAJAMAN");
        Add(Id, "CURVE", "LENGKUNG");
        Add(Id, "CARVING", "UKIRAN");
        Add(Id, "Point both index fingers. Move them apart or together, and up or down.", "Tunjuk kedua jari telunjuk. Geser menjauh atau mendekat, dan naik atau turun.");
        Add(Id, "Pinch with your right hand, then lift.", "Cubit dengan tangan kanan, lalu angkat.");
        Add(Id, "Pinch with your left hand, then lift.", "Cubit dengan tangan kiri, lalu angkat.");
        Add(Id, "Pinch with your right hand. Lift to open, lower to close.", "Cubit dengan tangan kanan. Angkat untuk membuka, turunkan untuk menutup.");
        Add(Id, "Pinch with your left hand. Lift to open, lower to close.", "Cubit dengan tangan kiri. Angkat untuk membuka, turunkan untuk menutup.");
        Add(Id, "Make two fists and pull them down.", "Kepalkan dua tangan lalu tarik ke bawah.");
        Add(Id, "Make a V with both hands and spread them to the sides.", "Buat huruf V dengan kedua tangan dan buka ke samping.");
        Add(Id, "Make one fist and move it left, right, up, or down.", "Kepalkan satu tangan dan gerakkan kiri, kanan, atas, atau bawah.");
        Add(Id, "Make a V with your right hand and raise or lower it.", "Buat V dengan tangan kanan lalu naikkan atau turunkan.");
        Add(Id, "Make a V with your left hand and raise or lower it.", "Buat V dengan tangan kiri lalu naikkan atau turunkan.");
        Add(Id, "After you change the lantern, open your palm to lock the shape.", "Setelah mengubah lampion, buka telapak untuk mengunci bentuk.");
        Add(Id, "Pinch with your right hand, then lift to raise the lid.", "Cubit dengan tangan kanan, lalu angkat untuk menaikkan tutup.");
        Add(Id, "Pinch with your left hand, then lift to pull the spout.", "Cubit dengan tangan kiri, lalu angkat untuk menarik corong.");
        Add(Id, "After you change the teapot, open your palm to rest.", "Setelah mengubah teko, buka telapak untuk istirahat.");
        Add(Id, "Pinch with your right hand, then lift to sharpen the blade.", "Cubit dengan tangan kanan, lalu angkat untuk menajamkan bilah.");
        Add(Id, "Pinch with your left hand, then lift to curve the blade.", "Cubit dengan tangan kiri, lalu angkat untuk melengkungkan bilah.");
        Add(Id, "After you change the keris, open your palm to rest.", "Setelah mengubah keris, buka telapak untuk istirahat.");
        Add(Id, "GESTURE {0} OF {1}", "GERAKAN {0} DARI {1}");
        Add(Id, "Frame your photo, then take a selfie.", "Atur foto, lalu ambil selfie.");
        Add(Id, "Last clay coil undone.", "Lilitan tanah liat terakhir dibatalkan.");
        Add(Id, "Last sculpt stroke undone.", "Goresan pahat terakhir dibatalkan.");
        Add(Id, "Last drawing stroke undone.", "Goresan gambar terakhir dibatalkan.");
        Add(Id, "Last change undone.", "Perubahan terakhir dibatalkan.");
        Add(Id, "Last clay coil redone.", "Lilitan tanah liat terakhir diulang.");
        Add(Id, "Last sculpt stroke redone.", "Goresan pahat terakhir diulang.");
        Add(Id, "Last drawing stroke redone.", "Goresan gambar terakhir diulang.");
        Add(Id, "Last change redone.", "Perubahan terakhir diulang.");
        Add(Id, "Nothing to undo.", "Tidak ada yang bisa diurungkan.");
        Add(Id, "The shelf is full.", "Rak sudah penuh.");
        Add(Id, "LOCKED. Point both index fingers to unlock.", "TERKUNCI. Tunjuk kedua telunjuk untuk membuka.");
        Add(Id, "CHANGE COLOR", "UBAH WARNA");
        Add(Id, "CLEAR DESIGN", "HAPUS DESAIN");
        Add(Id, "CLEAR DRAWINGS", "HAPUS GAMBAR");
        Add(Id, "BAND COLOR", "WARNA PITA");
        Add(Id, "BAND WIDTH", "LEBAR PITA");
        Add(Id, "SLIM", "RAMPING");
        Add(Id, "MEDIUM", "SEDANG");
        Add(Id, "BOLD", "TEBAL");
        Add(Id, "WIDE", "LEBAR");
        Add(Id, "Stamp 1", "Stempel 1");
        Add(Id, "Stamp 2", "Stempel 2");
        Add(Id, "Stamp 3", "Stempel 3");
        Add(Id, "Stamp 4", "Stempel 4");
        Add(Id, "BATIK", "BATIK");
        Add(Id, "BRAIDED", "ANYAMAN");
        Add(Id, "CHEVRON", "CHEVRON");
        Add(Id, "DIAMONDS", "BERLIAN");
        Add(Id, "LOTUS", "TERATAI");
        Add(Id, "PEARLS", "MUTIARA");
        Add(Id, "STRAIGHT", "LURUS");
        Add(Id, "WAVE", "GELOMBANG");
        Add(Id, "Change the paper color, or add a stamped design.", "Ubah warna kertas, atau tambah stempel.");
        Add(Id, "Change the paper color, or add another design.", "Ubah warna kertas, atau tambah desain lain.");
        Add(Id, "Point and hold until the border completes.", "Arahkan dan tahan sampai bingkai selesai.");
        Add(Id, "Paint and hold until the border completes.", "Arahkan dan tahan sampai bingkai selesai.");
        Add(Id, "Point at a color to tint the stamp.", "Arahkan ke warna untuk mewarnai stempel.");
        Add(Id, "Point and hold on a color to tint the band.", "Arahkan dan tahan warna untuk mewarnai pita.");
        Add(Id, "Choose a stamp. Then tint it with Change Color.", "Pilih stempel. Lalu warnai dengan Ubah Warna.");
        Add(Id, "No stamps found in StampPatterns.", "Stempel tidak ditemukan.");
        Add(Id, "Select a motif, adjust size & color, then trace on lantern.", "Pilih motif, atur ukuran & warna, lalu gambar di lampion.");
        Add(Id, "Select a motif, adjust size & color, then trace on the lantern.", "Pilih motif, atur ukuran & warna, lalu gambar di lampion.");
        Add(Id, "Choose Circle or Rectangle, then pinch and lift.", "Pilih Lingkaran atau Persegi, lalu cubit dan angkat.");
        Add(Id, "Choose one tassel or three tassels.", "Pilih satu rumbai atau tiga rumbai.");
        Add(Id, "Attach the photo to the lantern, retake it, or press DONE.", "Tempel foto ke lampion, foto ulang, atau tekan SELESAI.");
        Add(Id, "Remove the selfie, retake it, or press DONE.", "Hapus selfie, foto ulang, atau tekan SELESAI.");
        Add(Id, "Frame your photo, then point and hold TAKE SELFIE.", "Atur foto, lalu arahkan dan tahan AMBIL SELFIE.");
        Add(Id, "All drawings cleared.", "Semua gambar dihapus.");
        Add(Id, " is already showing. Change color, or pick another shape.", " sudah tampil. Ubah warna, atau pilih bentuk lain.");
        Add(Id, "Pinch with your right hand and lift to pop the shape.", "Cubit dengan tangan kanan lalu angkat untuk mengeluarkan bentuk.");
        Add(Id, "Pinch with your left hand and lift to pop the shape.", "Cubit dengan tangan kiri lalu angkat untuk mengeluarkan bentuk.");
        Add(Id, "Make two fists and pull them down to bring the tassel out.", "Kepalkan dua tangan dan tarik ke bawah untuk mengeluarkan rumbai.");
        Add(Id, "Stamp tint selected. Choose another color or press DONE.", "Warna stempel dipilih. Pilih warna lain atau tekan SELESAI.");
        Add(Id, "Color selected. Choose another color or press DONE.", "Warna dipilih. Pilih warna lain atau tekan SELESAI.");
        Add(Id, "Change the pot color, or add a stamped design.", "Ubah warna teko, atau tambah stempel.");
        Add(Id, "Change the pot color, or add another design.", "Ubah warna teko, atau tambah desain lain.");
        Add(Id, "TEAPOT BODY", "BADAN TEKO");
        Add(Id, "Attach the photo to the teapot, retake it, or press DONE.", "Tempel foto ke teko, foto ulang, atau tekan SELESAI.");
        Add(Id, "Attach the photo to the keris, retake it, or press DONE.", "Tempel foto ke keris, foto ulang, atau tekan SELESAI.");
        Add(Id, "STAMP COLOR", "WARNA STEMPEL");
        Add(Id, "Pick a clay color.", "Pilih warna tanah liat.");
        Add(Id, "Pick a tint for the stamp.", "Pilih warna stempel.");
        Add(Id, "Pick a color for the drawn band.", "Pilih warna pita.");
        Add(Id, "Pick a color for the keris.", "Pilih warna keris.");
        Add(Id, "Change the blade color, add a stamped design, or draw a band.", "Ubah warna bilah, tambah stempel, atau gambar pita.");
        Add(Id, "Change the blade color, add another stamp, or draw a band.", "Ubah warna bilah, tambah stempel lain, atau gambar pita.");
        Add(Id, "BRUSH SIZE", "UKURAN KUAS");
        Add(Id, "SOFT", "LEMBUT");
        Add(Id, "MED", "SEDANG");
        Add(Id, "STRONG", "KUAT");
        Add(Id, "MAX", "MAKS");
        Add(Id, "S", "S");
        Add(Id, "M", "M");
        Add(Id, "L", "L");
        Add(Id, "XL", "XL");
        Add(Id, "HANDLE 1", "HULU 1");
        Add(Id, "HANDLE 2", "HULU 2");
        Add(Id, "CARVING 1", "UKIRAN 1");
        Add(Id, "CARVING 2", "UKIRAN 2");
        Add(Id, "Carving 1", "Ukiran 1");
        Add(Id, "Carving 2", "Ukiran 2");
        Add(Id, "Choose Handle 1 or Handle 2.", "Pilih Gagang 1 atau Gagang 2.");
        Add(Id, "Choose HANDLE 1 or HANDLE 2.", "Pilih Hulu 1 atau Hulu 2.");
        Add(Id, "Choose Spout 1 or Spout 2.", "Pilih Corong 1 atau Corong 2.");
        Add(Id, "Choose Lid 1 or Lid 2.", "Pilih Tutup 1 atau Tutup 2.");
        Add(Id, "Choose Carving 1 or Carving 2.", "Pilih Ukiran 1 atau Ukiran 2.");
        Add(Id, "Pinch with your right hand and lift to raise the lid.", "Cubit dengan tangan kanan lalu angkat untuk menaikkan tutup.");
        Add(Id, "Pinch with your left hand and lift to pull the spout.", "Cubit dengan tangan kiri lalu angkat untuk menarik corong.");
        Add(Id, "Make two fists and pull them down to bring the handle out.", "Kepalkan dua tangan dan tarik ke bawah untuk mengeluarkan gagang.");
        Add(Id, "Make two V signs and spread them outwards to bring the carving out.", "Buat dua tanda V dan buka ke samping untuk mengeluarkan ukiran.");
        Add(Id, "Select a motif, adjust size & color, then trace on the keris.", "Pilih motif, atur ukuran & warna, lalu gambar di keris.");
        Add(Id, "Select a motif, adjust size & color, then trace on teapot.", "Pilih motif, atur ukuran & warna, lalu gambar di teko.");
        Add(Id, "Choose a stamp first.", "Pilih stempel dulu.");
        Add(Id, "Choose a color or sculpt clay.", "Pilih warna, atau pahat tanah liat.");
        Add(Id, "Point both index fingers for width and height. Right pinch up for sharpness, left pinch up for curve. Open palm to lock.", "Tunjuk kedua telunjuk untuk lebar dan tinggi. Cubit kanan ke atas untuk ketajaman, cubit kiri ke atas untuk lengkung. Buka telapak untuk mengunci.");
        Add(Id, " selected.", " dipilih.");
        Add(Id, "REVIEW", "TINJAU");
        Add(Id, "SHELF", "RAK");
        Add(Id, "← PALETTE", "← PALET");
        Add(Id, "← REVIEW", "← TINJAU");
        Add(Id, "← BAND", "← PITA");
        Add(Id, "Go back, or put this teapot on the shelf.", "Kembali, atau taruh teko ini di rak.");
        Add(Id, "Go back, or put this lantern on the shelf.", "Kembali, atau taruh lampion ini di rak.");
        Add(Id, "Go back, or put this keris on the shelf.", "Kembali, atau taruh keris ini di rak.");
        Add(Id, "Take it back, delete it, or go back when you are done.", "Ambil kembali, hapus, atau kembali jika selesai.");
        Add(Id, "Take it back, or go back when you are done.", "Ambil kembali, atau kembali jika selesai.");
        Add(Id, "This removes the teapot from the shelf.", "Ini menurunkan teko dari rak.");
        Add(Id, "This removes the lantern from the shelf.", "Ini menurunkan lampion dari rak.");
        Add(Id, "This removes the keris from the shelf.", "Ini menurunkan keris dari rak.");
        Add(Id, "The shelf is not ready yet.", "Rak belum siap.");
        Add(Id, "Could not place this teapot on the shelf.", "Tidak bisa menaruh teko ini di rak.");
        Add(Id, "Could not place this lantern on the shelf.", "Tidak bisa menaruh lampion ini di rak.");
        Add(Id, "Could not place this keris on the shelf.", "Tidak bisa menaruh keris ini di rak.");
        Add(Id, "This clears sculpt, color, design, and selfie.", "Ini menghapus pahatan, warna, desain, dan selfie.");
        Add(Id, "This clears sculpt, color, and stamp.", "Ini menghapus pahatan, warna, dan stempel.");
        Add(Id, "Teapot reset.", "Teko diatur ulang.");
        Add(Id, "Clay color selected. Pick another color or press DONE.", "Warna tanah liat dipilih. Pilih warna lain atau tekan SELESAI.");
        Add(Id, "Band color selected. Pick another color or press DONE.", "Warna pita dipilih. Pilih warna lain atau tekan SELESAI.");
        Add(Id, "Point and hold on a color to tint the clay.", "Arahkan dan tahan warna untuk mewarnai tanah liat.");
        Add(Id, "Point and hold on a color to tint the band designs.", "Arahkan dan tahan warna untuk mewarnai desain pita.");
        Add(Id, "Trace index finger across teapot to draw. Open palm to lock.", "Geser jari telunjuk di teko untuk menggambar. Buka telapak untuk mengunci.");
        Add(Id, "Trace index finger across the keris to draw. Open palm to lock.", "Geser jari telunjuk di keris untuk menggambar. Buka telapak untuk mengunci.");
        Add(Id, "Trace index finger across lantern to draw. Open palm to lock.", "Geser jari telunjuk di lampion untuk menggambar. Buka telapak untuk mengunci.");
        Add(Id, "DRAWING LOCKED (Open palm detected). Show open palm again to unlock, or tap <- MOTIFS.", "GAMBAR TERKUNCI (Telapak terbuka). Buka telapak lagi untuk membuka, atau ketuk ← MOTIF.");
        Add(Id, "DRAWING LOCKED (Open palm detected). Show open palm again to unlock, or tap ← MOTIFS.", "GAMBAR TERKUNCI (Telapak terbuka). Buka telapak lagi untuk membuka, atau ketuk ← MOTIF.");
        Add(Id, "DRAWING... Trace index finger across teapot to draw!", "MENGGAMBAR... Geser telunjuk di teko!");
        Add(Id, "DRAWING... Trace index finger across lantern to draw!", "MENGGAMBAR... Geser telunjuk di lampion!");
        Add(Id, "Point index finger to draw. Open palm to lock.", "Tunjuk telunjuk untuk menggambar. Buka telapak untuk mengunci.");
        Add(Id, "Aim at teapot. Point index finger to draw, open palm to lock.", "Arahkan ke teko. Tunjuk telunjuk untuk menggambar, buka telapak untuk mengunci.");
        Add(Id, "Aim at lantern. Point index finger to draw, open palm to lock.", "Arahkan ke lampion. Tunjuk telunjuk untuk menggambar, buka telapak untuk mengunci.");
        Add(Id, "Last stroke undone.", "Goresan terakhir dibatalkan.");
        Add(Id, "Last stroke redone.", "Goresan terakhir diulang.");
        Add(Id, "Nothing to redo.", "Tidak ada yang bisa diulang.");
        Add(Id, "Camera is not ready yet.", "Kamera belum siap.");
        Add(Id, "Camera is not ready yet. Please try again in a moment.", "Kamera belum siap. Coba lagi sebentar.");
        Add(Id, "Could not capture camera image.", "Tidak bisa mengambil gambar kamera.");
        Add(Id, "Could not capture the camera image. Please try again.", "Tidak bisa mengambil gambar kamera. Coba lagi.");
        Add(Id, "Take a selfie before attaching it.", "Ambil selfie dulu sebelum menempel.");
        Add(Id, "Take a selfie before attaching it to the lantern.", "Ambil selfie dulu sebelum menempel ke lampion.");
        Add(Id, "Selfie attached to the teapot body.", "Selfie ditempel ke badan teko.");
        Add(Id, "Selfie attached to the lantern body.", "Selfie ditempel ke badan lampion.");
        Add(Id, "Selfie removed.", "Selfie dihapus.");
        Add(Id, "Selfie restored.", "Selfie dikembalikan.");
        Add(Id, "Selfie removed. Take a new one, or press BACK.", "Selfie dihapus. Ambil yang baru, atau tekan KEMBALI.");
        Add(Id, " applied. Change Color tints the stamp.", " diterapkan. Ubah Warna mewarnai stempel.");
        Add(Id, "PATTERN", "POLA");
        Add(Id, "BAND", "PITA");
        Add(Id, "BAND {0}", "PITA {0}");
        Add(Id, "SIZE: {0}", "UKURAN: {0}");
        Add(Id, "DRAW: {0}", "GAMBAR: {0}");
        Add(Id, "Reset the Keris and clear your current decorations?", "Atur ulang keris dan hapus hiasan saat ini?");
        Add(Id, "SCULPT MODE", "MODE PAHAT");
        Add(Id, "PAINT MODE", "MODE CAT");
        Add(Id, "CLEAR", "HAPUS");
        Add(Id, "SIZE: SLIM", "UKURAN: RAMPING");
        Add(Id, "SIZE: MEDIUM", "UKURAN: SEDANG");
        Add(Id, "SIZE: BOLD", "UKURAN: TEBAL");
        Add(Id, "SIZE: WIDE", "UKURAN: LEBAR");
        Add(Id, "LISA:", "LISA:");
        Add(Id, "lantern", "lentera");
        Add(Id, "teapot", "teko");
        Add(Id, "keris", "keris");
        Add(Id, "sculpture", "karya");
        Add(Id, "and", "dan");
        Add(Id, "stamp", "stempel");
        Add(Id, "painted band", "pita");
        Add(Id, "width", "lebar");
        Add(Id, "height", "tinggi");
        Add(Id, "color", "warna");
        Add(Id, "wide body", "badan lebar");
        Add(Id, "tall form", "bentuk tinggi");
        Add(Id, "soft middle", "pinggang lembut");
        Add(Id, "beveled rim", "tepi miring");
        Add(Id, "expressive lid", "tutup yang berkarakter");
        Add(Id, "proud spout", "cerat yang tegak");
        Add(Id, "curved handle", "gagang lengkung");
        Add(Id, "wide blade", "bilah lebar");
        Add(Id, "long blade", "bilah panjang");
        Add(Id, "strong carving", "ukiran tegas");
        Add(Id, "expressive handle", "gagang yang berkarakter");
        Add(Id, "lifted top", "puncak terangkat");
        Add(Id, "full bottom", "dasar penuh");
        Add(Id, "open lid", "tudung terbuka");
        Add(Id, "deep base", "alas yang dalam");
        Add(Id, "bamboo detail", "detail bambu");
        Add(Id, "proud tassel", "rumbai yang anggun");
        Add(Id, "bold {0}", "{0} yang berani");
        Add(Id, "gentle {0}", "{0} yang lembut");
        Add(Id, "Your {0} looks wonderful.", "{0}-mu terlihat indah.");
        Add(Id, "Your {0} is coming together so nicely.", "{0}-mu makin terasa utuh dan bagus.");
        Add(Id, "I really like what you made with this {0}.", "Aku sangat suka {0} yang kamu buat.");
        Add(Id, "The shape feels generous — especially the {0}.", "Bentuknya terasa longgar dan hangat — terutama {0}.");
        Add(Id, "That {0} is a beautiful color choice.", "{0} itu pilihan warna yang indah.");
        Add(Id, "The stamp design sits beautifully on it.", "Desain stempelnya duduk dengan indah.");
        Add(Id, "The {0} design sits beautifully on it.", "Desain {0} duduk dengan indah di atasnya.");
        Add(Id, "Your selfie makes this piece feel personal.", "Selfie-mu membuat karya ini terasa personal.");
        Add(Id, "The painted bands add a handmade rhythm.", "Pita lukisannya menambah irama buatan tangan.");
        Add(Id, "The clay coils give it real pottery character.", "Lilitan tanah liat memberinya karakter gerabah yang nyata.");
        Add(Id, "The little sculpted details make it one of a kind.", "Detail pahatannya membuatnya unik.");
        Add(Id, "Keep going — this already feels like it belongs in a collection.", "Terus berkarya — ini sudah terasa layak masuk koleksi.");
        Add(Id, "If you want to push it further, try a bolder {0} or another color you love.", "Kalau ingin lebih jauh, coba {0} yang lebih berani atau warna lain yang kamu suka.");
        Add(Id, "You're just getting started, and that's a great place to be. Shape it a bit more, then try a color you love — even a small change will make it feel like yours.", "Kamu baru mulai, dan itu tempat yang bagus. Bentuk sedikit lagi, lalu coba warna yang kamu suka — perubahan kecil pun akan membuatnya terasa milikmu.");
        Add(Id, "terracotta", "terakota");
        Add(Id, "warm red", "merah hangat");
        Add(Id, "deep red", "merah tua");
        Add(Id, "coral", "karang");
        Add(Id, "orange", "oranye");
        Add(Id, "gold", "emas");
        Add(Id, "amber", "amber");
        Add(Id, "yellow", "kuning");
        Add(Id, "fresh green", "hijau segar");
        Add(Id, "forest green", "hijau hutan");
        Add(Id, "teal", "teal");
        Add(Id, "bright blue", "biru cerah");
        Add(Id, "deep blue", "biru tua");
        Add(Id, "purple", "ungu");
        Add(Id, "pink", "merah muda");
        Add(Id, "magenta", "magenta");
        Add(Id, "cream", "krem");
        Add(Id, "soft gray", "abu lembut");
        Add(Id, "charcoal", "arang");
        Add(Id, "deep black", "hitam pekat");
    }
}

public sealed class MorphicLocKey : MonoBehaviour
{
    [SerializeField] private string key;

    public void SetKey(string english)
    {
        key = MorphicLoc.EnglishKey(english);
    }

    private void Awake()
    {
        MorphicLoc.Changed += Refresh;
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        MorphicLoc.Changed -= Refresh;
    }

    public void Refresh()
    {
        if (string.IsNullOrEmpty(key))
            return;
        key = MorphicLoc.EnglishKey(key);
        TMP_Text label = GetComponent<TMP_Text>();
        if (label == null)
            return;
        MorphicLoc.Write(label, key);
    }
}
