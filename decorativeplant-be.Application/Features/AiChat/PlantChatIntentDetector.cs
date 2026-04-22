using System.Globalization;
using System.Text;

namespace decorativeplant_be.Application.Features.AiChat;

/// <summary>
/// Lightweight keyword intent for chat (no extra LLM call). Detects disease / pest / damage help.
/// </summary>
public static class PlantChatIntentDetector
{
    public const string DiseaseDiagnosisIntent = "disease_diagnosis";

    /// <summary>
    /// Some clients send a placeholder caption when the user attaches an image without typing.
    /// Treat these as "no intent expressed" (not an implicit disease-check request).
    /// </summary>
    public static bool IsPlaceholderImageCaption(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var t = text.Trim().ToLowerInvariant();
        var folded = FoldVietnamese(t);

        // English
        if (t is "photo attached" or "image attached" or "attached photo" or "attached image")
        {
            return true;
        }

        // Vietnamese (folded)
        if (folded is "da dinh kem anh" or "da dinh kem hinh" or "co dinh kem anh" or "co dinh kem hinh")
        {
            return true;
        }

        // Very short captions like "." or "-"
        if (t.Length <= 2 && t.All(c => !char.IsLetterOrDigit(c)))
        {
            return true;
        }

        return false;
    }

    /// <summary>Returns true if the latest user text suggests disease, pests, mold, spots, or plant damage.</summary>
    public static bool IsDiseaseDiagnosisIntent(string? lastUserMessage)
    {
        if (string.IsNullOrWhiteSpace(lastUserMessage))
        {
            return false;
        }

        var t = lastUserMessage.Trim().ToLowerInvariant();
        // Normalize Vietnamese accents loosely by checking both original and stripped forms for common words
        var folded = FoldVietnamese(t);

        // English
        string[] en =
        {
            "disease", "diseased", "sick plant", "fungus", "fungal", "mold", "mildew", "powdery",
            "rust", "blight", "rot", "root rot", "stem rot", "leaf spot", "spots on", "yellowing",
            "browning", "wilting", "pest", "aphid", "mealybug", "scale insect", "spider mite", "mite",
            "caterpillar", "snail", "slug", "infection", "infected", "dying", "black spot", "anthracnose",
            "diagnos", "is my plant", "looks sick", "looks bad", "look bad",
            "weird mark", "strange mark", "odd mark", "mark on", "this mark", "dark mark", "brown mark",
            "holes in leaves", "chewed", "discoloration", "tumor", "gall", "canker", "edema",
            "bacterial", "viral", "nematode", "white fuzz", "brown spots", "yellow leaves",
        };

        foreach (var k in en)
        {
            if (t.Contains(k, StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Vietnamese (ASCII-folded substring checks)
        string[] vi =
        {
            "benh", // bệnh
            "nam", // nấm
            "dom", // đốm
            "vang la", // vàng lá
            "chay la", // cháy lá
            "ray", // rầy
            "sau", // sâu
            "nhen", // nhện
            "duoi", // đuôi (rệp?)
            "chet", // chết
            "om", // ốm
            "moc", // mốc
            "thoi", // thối
            "chan thu", // chẩn đoán (approx)
        };

        foreach (var k in vi)
        {
            if (folded.Contains(k, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the user text is about watering, ID, light, pets/toxicity, etc. — prefer general vision chat over formal disease pipeline.
    /// </summary>
    public static bool LooksLikeNonDiseaseImageChat(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var t = text.Trim().ToLowerInvariant();
        var folded = FoldVietnamese(t);

        // Avoid bare "water" — it matches "overwatered", "underwatered", etc. mixed with disease wording.
        string[] en =
        {
            "watering", "how often", "schedule", "fertiliz", "repot", "soil mix", "light level",
            "how much light", "sunlight", "identify this plant", "what plant is", "plant id", "species name",
            "toxic to", "safe for cats", "safe for dogs", "pet safe", "for cats", "for dogs", "humidity for",
            "prune", "propagat", "transplant", "pot size", "growth rate",
        };

        foreach (var k in en)
        {
            if (t.Contains(k, StringComparison.Ordinal))
            {
                return true;
            }
        }

        string[] vi =
        {
            "tuoi nuoc", "tuoi cay", "cach cham", "anh sang", "dat trong", "cho meo", "thu cung",
            "cay gi", "ten cay", "bam phan",
        };

        foreach (var k in vi)
        {
            if (folded.Contains(k, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Use Gemini + Ollama formal diagnosis when an image is attached and the message is empty (photo-only),
    /// disease-related, or not clearly a general-care-only question.
    /// </summary>
    public static bool ShouldUseGeminiOllamaDiagnosisPipeline(string? lastUserText, bool hasAttachedImage)
    {
        if (!hasAttachedImage)
        {
            return false;
        }

        // Placeholder-only captions are not an implicit request for a disease check.
        if (IsPlaceholderImageCaption(lastUserText))
        {
            return false;
        }

        // Disease / damage wording wins over general-care phrases in the same message (order matters).
        if (IsDiseaseDiagnosisIntent(lastUserText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(lastUserText) && LooksLikeNonDiseaseImageChat(lastUserText))
        {
            return false;
        }

        // Only route photo-only messages to the formal pipeline when the user truly sent no caption,
        // not when the UI injected a placeholder caption.
        return string.IsNullOrWhiteSpace(lastUserText);
    }

    /// <summary>Remove common Vietnamese diacritics for simple matching.</summary>
    private static string FoldVietnamese(string s)
    {
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
