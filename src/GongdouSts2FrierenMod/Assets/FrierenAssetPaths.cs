using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;

namespace GongdouSts2FrierenMod.Assets;

public static class FrierenAssetPaths
{
    public const bool UseCustomCardPortraits = true;
    private static readonly Dictionary<string, Texture2D> LoadedTextures = new(StringComparer.OrdinalIgnoreCase);
    private static string? _modDirectory;

    public static string CardPortrait(string portraitKey, string fallbackAtlasId)
    {
        return UseCustomCardPortraits && IsCustomArtKey(portraitKey)
            ? ImageHelper.GetImagePath($"frieren/cards/{portraitKey}.png")
            : ImageHelper.GetImagePath($"atlases/card_atlas.sprites/ironclad/{fallbackAtlasId}.tres");
    }

    public static string RelicIcon(string iconKey) => ImageHelper.GetImagePath($"frieren/relics/{iconKey}.png");

    public static string PotionIcon(string iconKey) => ImageHelper.GetImagePath($"frieren/potions/{iconKey}.png");

    public static string CharacterModelImage => ImageHelper.GetImagePath("frieren/character/model.png");

    public static string CharacterCoverImage => ImageHelper.GetImagePath("frieren/character/cover.png");

    public static string CharacterModelRelativePath => "character/model.png";

    public static string CharacterCoverRelativePath => "character/cover.png";

    public static bool IsCustomArtKey(string key) => key.Contains('/') || key.Contains('\\');

    public static string CardRelativePath(string portraitKey) => $"cards/{portraitKey}.png";

    public static string RelicRelativePath(string iconKey) => $"relics/{iconKey}.png";

    public static string PotionRelativePath(string iconKey) => $"potions/{iconKey}.png";

    public static string AssetRoot => Path.Combine(ModDirectory, "assets", "frieren");

    private static string ModDirectory => _modDirectory ??= Path.GetDirectoryName(typeof(FrierenAssetPaths).Assembly.Location) ?? ".";

    public static Texture2D GetOrLoadTexture(string virtualPath, string relativePath)
    {
        if (LoadedTextures.TryGetValue(virtualPath, out var texture) && GodotObject.IsInstanceValid(texture))
        {
            return texture;
        }

        LoadedTextures.Remove(virtualPath);
        if (PreloadManager.Cache.ContainsKey(virtualPath))
        {
            texture = PreloadManager.Cache.GetTexture2D(virtualPath);
            if (GodotObject.IsInstanceValid(texture))
            {
                LoadedTextures[virtualPath] = texture;
                return texture;
            }
        }

        var absolutePath = ResolveAssetPath(relativePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException($"Frieren asset missing: {absolutePath}", absolutePath);
        }

        using var image = Image.LoadFromFile(absolutePath);
        texture = ImageTexture.CreateFromImage(image);
        LoadedTextures[virtualPath] = texture;
        return texture;
    }

    public static bool TryGetOrLoadTexture(string virtualPath, string relativePath, out Texture2D? texture)
    {
        try
        {
            texture = GetOrLoadTexture(virtualPath, relativePath);
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou STS2 Frieren] Failed to load custom texture {relativePath}: {ex.Message}");
            texture = null;
            return false;
        }
    }

    private static string ResolveAssetPath(string relativePath)
    {
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var primary = Path.Combine(AssetRoot, normalizedRelativePath);
        if (File.Exists(primary))
        {
            return primary;
        }

        var parent = Directory.GetParent(ModDirectory)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent))
        {
            var rootAssetPath = Path.Combine(parent, "assets", "frieren", normalizedRelativePath);
            if (File.Exists(rootAssetPath))
            {
                return rootAssetPath;
            }
        }

        return primary;
    }

    public static void PreloadCustomTextures()
    {
        foreach (var entry in FrierenArtManifest.All)
        {
            TryGetOrLoadTexture(entry.VirtualPath, entry.RelativePath, out _);
        }
    }

    public static string CreatureVisuals => SceneHelper.GetScenePath("creature_visuals/ironclad");
    public static string TopPanelIcon => ImageHelper.GetImagePath("ui/top_panel/character_icon_ironclad.png");
    public static string TopPanelIconOutline => ImageHelper.GetImagePath("ui/top_panel/character_icon_ironclad_outline.png");
    public static string CharacterIconScene => SceneHelper.GetScenePath("ui/character_icons/ironclad_icon");
    public static string CharacterSelectIcon => ImageHelper.GetImagePath("packed/character_select/char_select_ironclad.png");
    public static string CharacterSelectLockedIcon => ImageHelper.GetImagePath("packed/character_select/char_select_ironclad_locked.png");
    public static string CharacterSelectBackground => SceneHelper.GetScenePath("screens/char_select/char_select_bg_ironclad");
    public static string EnergyCounter => SceneHelper.GetScenePath("combat/energy_counters/ironclad_energy_counter");
    public static string RestSite => SceneHelper.GetScenePath("rest_site/characters/ironclad_rest_site");
    public static string Merchant => SceneHelper.GetScenePath("merchant/characters/ironclad_merchant");
    public static string TransitionMaterial => "res://materials/transitions/ironclad_transition_mat.tres";
    public static string MapMarker => ImageHelper.GetImagePath("packed/map/icons/map_marker_ironclad.png");
    public static string CardTrail => SceneHelper.GetScenePath("vfx/card_trail_ironclad");
    public static string MultiplayerHandPoint => ImageHelper.GetImagePath("ui/hands/multiplayer_hand_ironclad_point.png");

    public static IEnumerable<string> RuntimeAssetPaths =>
    [
        CreatureVisuals,
        TopPanelIcon,
        CharacterIconScene,
        EnergyCounter,
        RestSite,
        Merchant,
        TransitionMaterial,
        MapMarker,
        CardTrail
    ];

    public static IEnumerable<string> CharacterSelectAssetPaths =>
    [
        CharacterSelectBackground,
        CharacterSelectIcon,
        TopPanelIcon,
        CharacterSelectLockedIcon,
        TransitionMaterial
    ];
}

public readonly record struct FrierenArtEntry(string VirtualPath, string RelativePath);

public static class FrierenArtManifest
{
    public static readonly FrierenArtEntry[] CardPortraits =
    [
        Card("奖励卡牌/001_杀人魔法"),
        Card("奖励卡牌/002_破魔射击"),
        Card("奖励卡牌/003_贯穿防御"),
        Card("奖励卡牌/004_近距施法"),
        Card("奖励卡牌/005_重复射击"),
        Card("奖励卡牌/006_魔力针"),
        Card("奖励卡牌/007_闪烁术式"),
        Card("奖励卡牌/008_散射光弹"),
        Card("奖励卡牌/009_旧式杀魔法"),
        Card("奖励卡牌/010_防御魔法"),
        Card("奖励卡牌/011_魔力探知"),
        Card("奖励卡牌/012_清洁魔法"),
        Card("奖励卡牌/013_修补魔法"),
        Card("奖励卡牌/014_鉴定魔法书"),
        Card("奖励卡牌/015_探索地城"),
        Card("奖励卡牌/016_缓慢咏唱"),
        Card("奖励卡牌/017_安静脚步"),
        Card("奖励卡牌/018_解除魔法"),
        Card("奖励卡牌/019_魔力丝线"),
        Card("奖励卡牌/020_小小收藏癖"),
        Card("奖励卡牌/021_杀人魔法连射"),
        Card("奖励卡牌/022_复写杀人魔法"),
        Card("奖励卡牌/023_对魔族术式"),
        Card("奖励卡牌/024_穿刺解析"),
        Card("奖励卡牌/025_镜像射击"),
        Card("奖励卡牌/026_魔力爆散"),
        Card("奖励卡牌/027_包围火线"),
        Card("奖励卡牌/028_闪烁齐射"),
        Card("奖励卡牌/029_杖击与施法"),
        Card("奖励卡牌/030_反屏障术式"),
        Card("奖励卡牌/031_隐蔽狙击"),
        Card("奖励卡牌/032_老练射线"),
        Card("奖励卡牌/033_深度魔力抑制"),
        Card("奖励卡牌/034_弱点解析"),
        Card("奖励卡牌/035_长咏唱"),
        Card("奖励卡牌/036_防御结界"),
        Card("奖励卡牌/037_飞行魔法"),
        Card("奖励卡牌/038_魔法收藏"),
        Card("奖励卡牌/039_鉴别宝箱怪"),
        Card("奖励卡牌/040_祛除诅咒"),
        Card("奖励卡牌/041_菲伦的掩护"),
        Card("奖励卡牌/042_平静观察"),
        Card("奖励卡牌/043_古旧魔导书"),
        Card("奖励卡牌/044_半世纪研究"),
        Card("奖励卡牌/045_屏障反转"),
        Card("奖励卡牌/046_咏唱保存"),
        Card("奖励卡牌/047_魔力隐蔽"),
        Card("奖励卡牌/048_魔导书收藏家"),
        Card("奖励卡牌/049_战斗解析"),
        Card("奖励卡牌/050_高效咏唱"),
        Card("奖励卡牌/051_人类魔法时代"),
        Card("奖励卡牌/052_菲伦的纪律"),
        Card("奖励卡牌/053_隐匿魔力储备"),
        Card("奖励卡牌/054_师徒节奏"),
        Card("奖励卡牌/055_千年的从容"),
        Card("奖励卡牌/056_葬送的杀人魔法"),
        Card("奖励卡牌/057_全力杀人魔法"),
        Card("奖励卡牌/058_千年齐射"),
        Card("奖励卡牌/059_魔力压倒"),
        Card("奖励卡牌/060_复写：菲伦的一击"),
        Card("奖励卡牌/061_破界射线"),
        Card("奖励卡牌/062_最后一课"),
        Card("奖励卡牌/063_开出花田的魔法"),
        Card("奖励卡牌/064_认真起来"),
        Card("奖励卡牌/065_魔法使的直觉"),
        Card("奖励卡牌/066_封印魔导书"),
        Card("奖励卡牌/067_静默蓄势"),
        Card("奖励卡牌/068_古代屏障"),
        Card("奖励卡牌/069_解析一切"),
        Card("奖励卡牌/070_漫长旅途"),
        Card("奖励卡牌/071_大魔法使芙莉莲"),
        Card("奖励卡牌/072_赛丽艾的教诲"),
        Card("奖励卡牌/073_辛美尔的回忆"),
        Card("奖励卡牌/074_芙兰梅的教导"),
        Card("奖励卡牌/075_魔法百科全书"),
        Card("奖励卡牌/076_受限的魔力气息"),
        Card("奖励卡牌/077_魔族杀手"),
        Card("奖励卡牌/078_普通魔法精通"),
        Card("奖励卡牌/079_千年研究"),
        Card("奖励卡牌/080_巨大魔力"),
        Card("奖励卡牌/081_勇者一行的魔法"),
        Card("奖励卡牌/082_最后的杀人魔法"),
        Card("起始牌/打击"),
        Card("起始牌/防御"),
        Card("起始牌/083_基础杀人魔法"),
        Card("起始牌/084_魔力抑制"),
        Card("生成牌与状态/085_临时基础杀人魔法"),
        Card("生成牌与状态/086_宝箱怪咬痕"),
        Card("生成牌与状态/087_无用小魔法"),
        Card("生成牌与状态/088_找铜币的魔法"),
        Card("生成牌与状态/089_除锈魔法")
    ];

    public static readonly FrierenArtEntry[] Relics =
    [
        Relic("090_蓝月草书签"),
        Relic("091_褪色的魔导书"),
        Relic("092_小型手提箱"),
        Relic("093_破旧魔法书页"),
        Relic("094_菲伦的发饰"),
        Relic("095_赞因的祷言"),
        Relic("096_宝箱怪图鉴"),
        Relic("097_芙兰梅的手记"),
        Relic("098_赛丽艾的证书"),
        Relic("099_欣梅尔的戒指")
    ];

    public static readonly FrierenArtEntry[] Potions =
    [
        Potion("100_魔力解放药水"),
        Potion("101_解析药水"),
        Potion("102_普通魔法瓶"),
        Potion("103_隐匿魔力药水"),
        Potion("104_花田药水")
    ];

    public static readonly FrierenArtEntry[] CharacterImages =
    [
        new(FrierenAssetPaths.CharacterModelImage, FrierenAssetPaths.CharacterModelRelativePath),
        new(FrierenAssetPaths.CharacterCoverImage, FrierenAssetPaths.CharacterCoverRelativePath)
    ];

    public static IEnumerable<FrierenArtEntry> All => CardPortraits.Concat(Relics).Concat(Potions).Concat(CharacterImages);

    private static FrierenArtEntry Card(string key) =>
        new(FrierenAssetPaths.CardPortrait(key, ""), $"cards/{key}.png");

    private static FrierenArtEntry Relic(string key) =>
        new(FrierenAssetPaths.RelicIcon(key), $"relics/{key}.png");

    private static FrierenArtEntry Potion(string key) =>
        new(FrierenAssetPaths.PotionIcon(key), $"potions/{key}.png");
}
