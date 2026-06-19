using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Achievements;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using GongdouSts2FrierenMod.Assets;
using GongdouSts2FrierenMod.Cards;
using GongdouSts2FrierenMod.Characters;
using GongdouSts2FrierenMod.Potions;
using GongdouSts2FrierenMod.Powers;
using GongdouSts2FrierenMod.Relics;
using FrierenInitializer = GongdouSts2FrierenMod.GongdouSts2FrierenMod;

namespace GongdouSts2FrierenMod.Patches;

internal static class FrierenCharacterPatchHelpers
{
    private const string FrierenCharacterSelectBackgroundNodeName = "GongdouFrierenCharacterSelectDynamicBackground";
    private const string FrierenCombatModelNodeName = "GongdouFrierenCombatModel";
    private const string FrierenCharacterSelectGateKey = "Gongdou.Sts2.Frieren.CharacterSelectEnabled";
    private const string FrierenCharacterSelectButtonName = "GongdouFrierenCharacterButton";
    private const string FrierenHiddenNativeBackgroundMeta = "GongdouFrierenHiddenNativeBackground";
    private const string FrierenHiddenNativeCharacterButtonMeta = "GongdouFrierenHiddenNativeCharacterButton";
    private const string FrierenHiddenNativeCombatVisualMeta = "GongdouFrierenHiddenNativeCombatVisual";
    private static readonly Vector2 FrierenCombatModelScale = new(0.70f, 0.70f);
    private static readonly Vector2 FrierenCombatModelOffset = new(-12.0f, -412.0f);
    private const int FrierenCombatModelZIndex = 10;

    public static bool IsFrieren(CharacterModel character) => character is FrierenCharacter;

    public static bool IsFrierenCharacterSelectEnabled()
    {
        return AppDomain.CurrentDomain.GetData(FrierenCharacterSelectGateKey) is bool enabled && enabled;
    }

    public static void ClearFrierenCharacterSelectGate()
    {
        AppDomain.CurrentDomain.SetData(FrierenCharacterSelectGateKey, false);
    }

    public static IEnumerable<T> AppendUnique<T>(IEnumerable<T> source, T item) where T : AbstractModel
    {
        var exists = false;
        foreach (var model in source)
        {
            if (model.Id == item.Id)
            {
                exists = true;
            }

            yield return model;
        }

        if (!exists)
        {
            yield return item;
        }
    }

    public static IEnumerable<T> AppendUnique<T>(IEnumerable<T> source, IEnumerable<T> items) where T : AbstractModel
    {
        var seen = new HashSet<ModelId>();
        foreach (var model in source)
        {
            seen.Add(model.Id);
            yield return model;
        }

        foreach (var item in items)
        {
            if (seen.Add(item.Id))
            {
                yield return item;
            }
        }
    }

    public static void ApplyCharacterVisual(NCreatureVisuals visuals)
    {
        if (!FrierenAssetPaths.TryGetOrLoadTexture(
                FrierenAssetPaths.CharacterModelImage,
                FrierenAssetPaths.CharacterModelRelativePath,
                out var texture) || texture == null)
        {
            return;
        }

        var overlayParent = ResolveCombatOverlayParent(visuals);
        var overlay = FindNodeByName<Sprite2D>(visuals, FrierenCombatModelNodeName);
        if (overlay == null)
        {
            overlay = new FrierenCombatModelSprite
            {
                Name = FrierenCombatModelNodeName,
                Centered = true,
                ZIndex = FrierenCombatModelZIndex
            };
            overlayParent.AddChild(overlay);
        }
        else if (!ReferenceEquals(overlay.GetParent(), overlayParent))
        {
            overlay.GetParent()?.RemoveChild(overlay);
            overlayParent.AddChild(overlay);
        }

        overlay.Texture = texture;
        ConfigureCombatOverlay(overlay);
        HideNativeCombatVisuals(visuals, overlay);
    }

    public static bool TryGetCharacterSelectCoverTexture(out Texture2D? texture)
    {
        return FrierenAssetPaths.TryGetOrLoadTexture(
            FrierenAssetPaths.CharacterCoverImage,
            FrierenAssetPaths.CharacterCoverRelativePath,
            out texture);
    }

    public static bool TryGetCharacterSelectButtonTexture(out Texture2D? texture)
    {
        return FrierenAssetPaths.TryGetOrLoadTexture(
            FrierenAssetPaths.CharacterModelImage,
            FrierenAssetPaths.CharacterModelRelativePath,
            out texture);
    }

    public static string CharacterSelectTitleKey => "FRIEREN_CHARACTER.title";

    public static string CharacterSelectDescriptionKey => "FRIEREN_CHARACTER.description";

    public static void EnsureLocalizationReady()
    {
        FrierenInitializer.EnsureLocalizationRegistered();
    }

    public static void ApplyCharacterSelectCover(NCharacterSelectScreen screen)
    {
        if (!TryGetCharacterSelectCoverTexture(out var texture) || texture == null)
        {
            return;
        }

        var bgContainer = GetCharacterSelectBackgroundContainer(screen);
        var parent = screen;
        var background = FindNodeByName<FrierenCharacterSelectDynamicBackground>(
            parent,
            FrierenCharacterSelectBackgroundNodeName);
        if (background == null)
        {
            background = new FrierenCharacterSelectDynamicBackground
            {
                Name = FrierenCharacterSelectBackgroundNodeName,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 0
            };
            parent.AddChild(background);
        }

        background.Configure(texture);
        background.Visible = true;
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        background.OffsetLeft = 0;
        background.OffsetTop = 0;
        background.OffsetRight = 0;
        background.OffsetBottom = 0;
        background.ForceViewportLayout();

        if (bgContainer != null)
        {
            HideNativeCharacterSelectBackground(bgContainer, background);
            var targetIndex = Math.Min(bgContainer.GetIndex() + 1, parent.GetChildCount() - 1);
            parent.MoveChild(background, targetIndex);
        }
    }

    public static void RestoreCharacterSelectCover(NCharacterSelectScreen screen)
    {
        var bgContainer = GetCharacterSelectBackgroundContainer(screen);
        if (bgContainer != null)
        {
            foreach (var child in bgContainer.GetChildren())
            {
                if (child is CanvasItem canvasItem && child.HasMeta(FrierenHiddenNativeBackgroundMeta))
                {
                    canvasItem.Visible = true;
                    child.RemoveMeta(FrierenHiddenNativeBackgroundMeta);
                }
            }
        }

        var background = FindNodeByName<FrierenCharacterSelectDynamicBackground>(
            screen,
            FrierenCharacterSelectBackgroundNodeName);
        if (background != null)
        {
            background.Visible = false;
        }
    }

    private static Control? GetCharacterSelectBackgroundContainer(NCharacterSelectScreen screen)
    {
        return AccessTools.Field(typeof(NCharacterSelectScreen), "_bgContainer")?.GetValue(screen) as Control;
    }

    private static void HideNativeCharacterSelectBackground(Node bgContainer, Node frierenBackground)
    {
        foreach (var child in bgContainer.GetChildren())
        {
            if (ReferenceEquals(child, frierenBackground))
            {
                continue;
            }

            if (child is CanvasItem canvasItem)
            {
                canvasItem.Visible = false;
                child.SetMeta(FrierenHiddenNativeBackgroundMeta, true);
            }
        }
    }

    public static void ApplyCharacterSelectButtonIcon(NCharacterSelectButton button)
    {
        if (!IsFrieren(button.Character) || !TryGetCharacterSelectButtonTexture(out var texture) || texture == null)
        {
            return;
        }

        if (AccessTools.Field(typeof(NCharacterSelectButton), "_icon")?.GetValue(button) is TextureRect icon)
        {
            icon.Texture = texture;
        }

        if (AccessTools.Field(typeof(NCharacterSelectButton), "_iconAdd")?.GetValue(button) is TextureRect iconAdd)
        {
            iconAdd.Texture = texture;
        }
    }

    public static void ApplyAllCharacterSelectButtonIcons(Node root)
    {
        foreach (var node in EnumerateNodes(root))
        {
            if (node is NCharacterSelectButton button)
            {
                ApplyCharacterSelectButtonIcon(button);
            }
        }
    }

    public static void SyncFrierenCharacterSelectButton(NCharacterSelectScreen screen)
    {
        try
        {
            SyncFrierenCharacterSelectButtonInternal(screen);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou Frieren] Failed to sync character select screen: {ex}");
        }
    }

    private static void SyncFrierenCharacterSelectButtonInternal(NCharacterSelectScreen screen)
    {
        var container = AccessTools.Field(typeof(NCharacterSelectScreen), "_charButtonContainer")?.GetValue(screen) as Control;
        if (container == null)
        {
            return;
        }

        var existing = FindFrierenCharacterSelectButton(container);
        if (!IsFrierenCharacterSelectEnabled())
        {
            RestoreCharacterSelectCover(screen);
            RestoreNativeCharacterSelectButtons(container);
            RemoveFrierenCharacterSelectButtons(container);
            return;
        }

        HideNonFrierenCharacterSelectButtons(container);

        if (existing == null)
        {
            var scene = AccessTools.Field(typeof(NCharacterSelectScreen), "_charSelectButtonScene")?.GetValue(screen) as PackedScene;
            if (scene == null)
            {
                GD.PrintErr("[GongDou Frieren] Character select button scene is not available.");
                return;
            }

            existing = scene.Instantiate<NCharacterSelectButton>(PackedScene.GenEditState.Disabled);
            existing.Name = FrierenCharacterSelectButtonName;
            container.AddChild(existing);
            existing.Init(ModelDb.Character<FrierenCharacter>(), screen);
        }

        existing.Visible = true;
        AccessTools.Method(typeof(NCharacterSelectButton), "DebugUnlock")?.Invoke(existing, null);
        AccessTools.Method(typeof(NCharacterSelectButton), "RefreshState")?.Invoke(existing, null);
        ApplyCharacterSelectButtonIcon(existing);
        HideNonFrierenCharacterSelectButtons(container);
        FocusCharacterSelectButtonOnItself(existing);
        if (IsCharacterSelectLobbyReady(screen) && !existing.IsSelected)
        {
            existing.Select();
        }

        ApplyCharacterSelectCover(screen);
    }

    private static NCharacterSelectButton? FindFrierenCharacterSelectButton(Node container)
    {
        return container.GetNodeOrNull<NCharacterSelectButton>(FrierenCharacterSelectButtonName)
            ?? container.GetChildren().OfType<NCharacterSelectButton>().FirstOrDefault(IsFrierenCharacterSelectButton);
    }

    private static bool IsFrierenCharacterSelectButton(NCharacterSelectButton button)
    {
        return button.Name == FrierenCharacterSelectButtonName || IsFrieren(button.Character);
    }

    private static void HideNonFrierenCharacterSelectButtons(Node container)
    {
        foreach (var button in container.GetChildren().OfType<NCharacterSelectButton>())
        {
            if (IsFrierenCharacterSelectButton(button))
            {
                button.Visible = true;
                continue;
            }

            if (button.IsSelected)
            {
                TryDeselectCharacterSelectButton(button, "hide-native");
            }

            if (button.Visible && !button.HasMeta(FrierenHiddenNativeCharacterButtonMeta))
            {
                button.SetMeta(FrierenHiddenNativeCharacterButtonMeta, true);
            }

            button.Visible = false;
        }
    }

    private static void RestoreNativeCharacterSelectButtons(Node container)
    {
        foreach (var button in container.GetChildren().OfType<NCharacterSelectButton>())
        {
            if (!button.HasMeta(FrierenHiddenNativeCharacterButtonMeta))
            {
                continue;
            }

            button.Visible = true;
            button.RemoveMeta(FrierenHiddenNativeCharacterButtonMeta);
        }
    }

    private static void RemoveFrierenCharacterSelectButtons(Node container)
    {
        foreach (var button in container.GetChildren().OfType<NCharacterSelectButton>().ToList())
        {
            if (!IsFrierenCharacterSelectButton(button))
            {
                continue;
            }

            if (button.IsSelected)
            {
                TryDeselectCharacterSelectButton(button, "remove-frieren");
            }

            button.Visible = false;
            button.QueueFree();
        }
    }

    private static void TryDeselectCharacterSelectButton(NCharacterSelectButton button, string reason)
    {
        try
        {
            if (button.IsSelected)
            {
                button.Deselect();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GongDou Frieren] Failed to deselect character select button ({reason}); continuing cleanup: {ex.Message}");
        }
    }

    private static void FocusCharacterSelectButtonOnItself(NCharacterSelectButton button)
    {
        var path = button.GetPath();
        button.FocusNeighborTop = path;
        button.FocusNeighborBottom = path;
        button.FocusNeighborLeft = path;
        button.FocusNeighborRight = path;
    }

    private static bool IsCharacterSelectLobbyReady(NCharacterSelectScreen screen)
    {
        return AccessTools.Field(typeof(NCharacterSelectScreen), "_lobby")?.GetValue(screen) != null;
    }

    public static void ConfigureCombatOverlay(Sprite2D overlay)
    {
        overlay.Visible = true;
        overlay.Show();
        overlay.Modulate = Colors.White;
        overlay.ZIndex = FrierenCombatModelZIndex;
        overlay.ZAsRelative = true;
        overlay.Centered = true;
        overlay.Scale = FrierenCombatModelScale;
        overlay.Position = FrierenCombatModelOffset;

        var parent = overlay.GetParent();
        while (parent != null)
        {
            if (parent is CanvasItem canvasItem)
            {
                canvasItem.Visible = true;
            }

            parent = parent.GetParent();
        }

        if (!overlay.IsInsideTree())
        {
            return;
        }

        var viewportSize = overlay.GetViewportRect().Size;
        if (viewportSize.X < 1.0f || viewportSize.Y < 1.0f)
        {
            return;
        }

        var globalPosition = overlay.GlobalPosition;
        if (globalPosition.X < -128.0f
            || globalPosition.X > viewportSize.X + 128.0f
            || globalPosition.Y < -256.0f
            || globalPosition.Y > viewportSize.Y + 128.0f)
        {
            overlay.GlobalPosition = new Vector2(viewportSize.X * 0.24f, viewportSize.Y * 0.54f);
        }
    }

    private static void HideNativeCombatVisuals(Node root, Node overlay)
    {
        var keepVisible = new HashSet<Node>();
        var nodeToKeep = overlay;
        while (nodeToKeep != null)
        {
            keepVisible.Add(nodeToKeep);
            if (ReferenceEquals(nodeToKeep, root))
            {
                break;
            }

            nodeToKeep = nodeToKeep.GetParent();
        }

        foreach (var node in EnumerateNodes(root))
        {
            if (ReferenceEquals(node, root) || keepVisible.Contains(node))
            {
                continue;
            }

            if (node is CanvasItem canvasItem)
            {
                canvasItem.Visible = false;
                node.SetMeta(FrierenHiddenNativeCombatVisualMeta, true);
            }
        }
    }

    private static Node ResolveCombatOverlayParent(Node root)
    {
        Node? bestParent = null;
        var bestDepth = -1;
        foreach (var node in EnumerateNodes(root))
        {
            if (ReferenceEquals(node, root)
                || node.Name == FrierenCombatModelNodeName
                || node is Control)
            {
                continue;
            }

            if (node is not CanvasItem)
            {
                continue;
            }

            var parent = node.GetParent();
            if (parent == null)
            {
                continue;
            }

            var depth = GetDepth(node, root);
            if (depth > bestDepth)
            {
                bestParent = parent;
                bestDepth = depth;
            }
        }

        return bestParent ?? root;
    }

    private static int GetDepth(Node node, Node root)
    {
        var depth = 0;
        var current = node;
        while (current != null && !ReferenceEquals(current, root))
        {
            depth += 1;
            current = current.GetParent();
        }

        return depth;
    }

    private static TextureRect? FindLargestTextureRect(Node root)
    {
        TextureRect? best = null;
        long bestArea = -1;
        foreach (var node in EnumerateNodes(root))
        {
            if (node is not TextureRect textureRect)
            {
                continue;
            }

            var size = textureRect.Size;
            var area = (long)Math.Round(size.X) * (long)Math.Round(size.Y);
            if (area > bestArea)
            {
                best = textureRect;
                bestArea = area;
            }
        }

        return best;
    }

    private static T? FindNodeByName<T>(Node root, string name) where T : Node
    {
        foreach (var node in EnumerateNodes(root))
        {
            if (node is T typed && node.Name == name)
            {
                return typed;
            }
        }

        return null;
    }

    private static IEnumerable<Node> EnumerateNodes(Node root)
    {
        yield return root;
        foreach (var child in root.GetChildren())
        {
            foreach (var nested in EnumerateNodes(child))
            {
                yield return nested;
            }
        }
    }
}

internal sealed class FrierenCombatModelSprite : Sprite2D
{
    public override void _Ready()
    {
        FrierenCharacterPatchHelpers.ConfigureCombatOverlay(this);
    }

    public override void _Process(double delta)
    {
        FrierenCharacterPatchHelpers.ConfigureCombatOverlay(this);
    }
}

[HarmonyPatch(typeof(PowerModel), "get_IconPath")]
internal static class FrierenPowerIconPathPatch
{
    private static bool Prefix(PowerModel __instance, ref string __result)
    {
        if (!FrierenPowerIconPaths.TryGet(__instance, out var iconPath))
        {
            return true;
        }

        __result = iconPath;
        return false;
    }
}

[HarmonyPatch(typeof(PowerModel), "get_PackedIconPath")]
internal static class FrierenPowerPackedIconPathPatch
{
    private static bool Prefix(PowerModel __instance, ref string __result)
    {
        if (!FrierenPowerIconPaths.TryGet(__instance, out var iconPath))
        {
            return true;
        }

        __result = iconPath;
        return false;
    }
}

[HarmonyPatch(typeof(PowerModel), "get_ResolvedBigIconPath")]
internal static class FrierenPowerResolvedBigIconPathPatch
{
    private static bool Prefix(PowerModel __instance, ref string __result)
    {
        if (!FrierenPowerIconPaths.TryGet(__instance, out var iconPath))
        {
            return true;
        }

        __result = iconPath;
        return false;
    }
}

internal static class FrierenPowerIconPaths
{
    private const string Artifact = "res://images/atlases/power_atlas.sprites/artifact_power.tres";
    private const string Barricade = "res://images/atlases/power_atlas.sprites/barricade_power.tres";
    private const string Buffer = "res://images/atlases/power_atlas.sprites/buffer_power.tres";
    private const string Dexterity = "res://images/atlases/power_atlas.sprites/dexterity_power.tres";
    private const string Energy = "res://images/atlases/power_atlas.sprites/energy_next_turn_power.tres";
    private const string Focus = "res://images/atlases/power_atlas.sprites/focus_power.tres";
    private const string Intangible = "res://images/atlases/power_atlas.sprites/intangible_power.tres";
    private const string NoDraw = "res://images/atlases/power_atlas.sprites/no_draw_power.tres";
    private const string Poison = "res://images/atlases/power_atlas.sprites/poison_power.tres";
    private const string Rage = "res://images/atlases/power_atlas.sprites/rage_power.tres";
    private const string Ritual = "res://images/atlases/power_atlas.sprites/ritual_power.tres";
    private const string Strength = "res://images/atlases/power_atlas.sprites/strength_power.tres";
    private const string Vulnerable = "res://images/atlases/power_atlas.sprites/vulnerable_power.tres";
    private const string Weak = "res://images/atlases/power_atlas.sprites/weak_power.tres";

    public static bool TryGet(PowerModel power, out string iconPath)
    {
        iconPath = power switch
        {
            FrierenAnalysisPower or FrierenInsightPower or FrierenCombatAnalysisPower => Vulnerable,
            FrierenConcealedManaPower or FrierenDelayedConcealedManaPower or FrierenHugeManaPower or FrierenLimitedManaAuraPower => Focus,
            FrierenReleasePower or FrierenDelayedReleasePower or FrierenHeroesPartyMagicPower => Strength,
            FrierenMemoryPower or FrierenLongJourneyPower or FrierenHimmelMemoryPower => Ritual,
            FrierenSuppressionPower or FrierenQuietStepsPower or FrierenManaConcealmentPower => Intangible,
            FrierenDelayedEnergyPower or FrierenDelayedBlockEnergyPower or FrierenConcealedManaReservePower => Energy,
            FrierenDelayedBlockPower or FrierenBarrierInversionPower or FrierenDelayedBarrierInversionRewardPower or FrierenDisciplinePower => Barricade,
            FrierenNoAttackThisTurnPower => NoDraw,
            FrierenNextAttackDiscountPower or FrierenEfficientChantPower => Dexterity,
            FrierenTemporaryStrengthDownPower or FrierenDemonKillerPower => Weak,
            FrierenFlowerFieldHealPower or FrierenFlowerFieldPotionPower => Buffer,
            FrierenGreatMagePower or FrierenMillenniumComposurePower or FrierenMillenniumResearchPower => Artifact,
            FrierenGrimoireCollectorPower or FrierenMagicEncyclopediaPower => Ritual,
            FrierenHumanMagicEraPower or FrierenNormalMagicMasteryPower => Rage,
            FrierenSerieTeachingPower or FrierenFlammeTeachingPower or FrierenMasterApprenticeRhythmPower => Strength,
            _ when power.GetType().Namespace == typeof(FrierenAnalysisPower).Namespace => Poison,
            _ => string.Empty
        };

        return iconPath.Length > 0;
    }
}

internal sealed class FrierenCharacterSelectDynamicBackground : Control
{
    private const int ParticleCount = 18;
    private readonly Random _random = new(0xF311E);
    private readonly List<FrierenCharacterSelectParticle> _particles = [];
    private TextureRect? _cover;
    private ColorRect? _veil;
    private double _time;

    public void Configure(Texture2D texture)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = false;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0;
        OffsetTop = 0;
        OffsetRight = 0;
        OffsetBottom = 0;

        _cover ??= CreateCover();
        _cover.Texture = texture;
        _cover.Visible = true;

        _veil ??= CreateVeil();
        _veil.Visible = true;

        EnsureParticles();
        ForceViewportLayout();
    }

    public void ForceViewportLayout()
    {
        var area = GetViewportRect().Size;
        if (area.X < 1.0f || area.Y < 1.0f)
        {
            area = new Vector2(1920, 1080);
        }

        Position = Vector2.Zero;
        Size = area;
        CustomMinimumSize = area;
        UpdateChildLayout(area, Vector2.Zero);
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _time += delta;
        AnimateCover();
        AnimateParticles();
    }

    private TextureRect CreateCover()
    {
        var cover = new TextureRect
        {
            Name = "GongdouFrierenCharacterSelectCover",
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            ZIndex = 0
        };
        AddChild(cover);
        MoveChild(cover, 0);
        return cover;
    }

    private ColorRect CreateVeil()
    {
        var veil = new ColorRect
        {
            Name = "GongdouFrierenCharacterSelectDarkVeil",
            MouseFilter = MouseFilterEnum.Ignore,
            Color = new Color(0.015f, 0.022f, 0.035f, 0.34f),
            ZIndex = 10
        };
        AddChild(veil);
        return veil;
    }

    private void EnsureParticles()
    {
        while (_particles.Count < ParticleCount)
        {
            var node = new ColorRect
            {
                Name = $"GongdouFrierenMagicParticle{_particles.Count:00}",
                MouseFilter = MouseFilterEnum.Ignore,
                Color = _particles.Count % 3 == 0
                    ? new Color(0.78f, 0.96f, 1.0f, 1.0f)
                    : new Color(0.92f, 0.80f, 0.42f, 1.0f),
                ZIndex = 20
            };
            AddChild(node);
            _particles.Add(new FrierenCharacterSelectParticle(
                node,
                new Vector2(RandomRange(0.04f, 0.96f), RandomRange(0.06f, 0.96f)),
                RandomRange(9.0f, 28.0f),
                RandomRange(5.0f, 22.0f),
                RandomRange(0.45f, 1.25f),
                RandomRange(0.16f, 0.42f),
                RandomRange(3.0f, 7.0f),
                RandomRange(0.0f, MathF.Tau)));
        }
    }

    private void AnimateCover()
    {
        if (_cover == null)
        {
            return;
        }

        var panX = (float)Math.Sin(_time * 0.12) * 10.0f;
        var panY = (float)Math.Cos(_time * 0.10) * 6.0f;
        var area = GetViewportRect().Size;
        if (area.X < 1.0f || area.Y < 1.0f)
        {
            area = Size;
        }

        if (area.X < 1.0f || area.Y < 1.0f)
        {
            area = new Vector2(1920, 1080);
        }

        Position = Vector2.Zero;
        Size = area;
        UpdateChildLayout(area, new Vector2(panX, panY));
    }

    private void UpdateChildLayout(Vector2 area, Vector2 pan)
    {
        if (_cover != null)
        {
            _cover.Position = new Vector2(-56.0f + pan.X, -32.0f + pan.Y);
            _cover.Size = area + new Vector2(112.0f, 64.0f);
        }

        if (_veil != null)
        {
            _veil.Position = Vector2.Zero;
            _veil.Size = area;
        }
    }

    private void AnimateParticles()
    {
        var area = Size;
        if (area.X < 1.0f || area.Y < 1.0f)
        {
            area = GetViewportRect().Size;
        }

        if (area.X < 1.0f || area.Y < 1.0f)
        {
            area = new Vector2(1920, 1080);
        }

        foreach (var particle in _particles)
        {
            var cycleHeight = area.Y + 96.0f;
            var driftX = (float)Math.Sin(_time * particle.Speed + particle.Phase) * particle.Drift;
            var driftY = (float)Math.Cos(_time * (particle.Speed * 0.67f) + particle.Phase) * 8.0f;
            var rise = (float)((_time * particle.RiseSpeed + particle.Phase * 11.0f) % cycleHeight);
            var x = particle.Anchor.X * area.X + driftX;
            var y = particle.Anchor.Y * area.Y - rise + cycleHeight;
            y = y % cycleHeight - 48.0f + driftY;
            var pulse = 0.55f + 0.45f * (float)Math.Sin(_time * particle.Speed + particle.Phase);

            particle.Node.Size = new Vector2(particle.Size, particle.Size);
            particle.Node.Position = new Vector2(x, y);
            particle.Node.Modulate = new Color(1.0f, 1.0f, 1.0f, particle.Alpha * pulse);
        }
    }

    private float RandomRange(float min, float max)
    {
        return min + (float)_random.NextDouble() * (max - min);
    }

    private sealed record FrierenCharacterSelectParticle(
        ColorRect Node,
        Vector2 Anchor,
        float RiseSpeed,
        float Drift,
        float Speed,
        float Alpha,
        float Size,
        float Phase);
}

internal static class FrierenFallbackLocalization
{
    private static readonly Dictionary<string, string> CharacterTexts = new(StringComparer.Ordinal)
    {
        ["FRIEREN_CHARACTER.title"] = "芙莉莲",
        ["FRIEREN_CHARACTER.titleObject"] = "芙莉莲",
        ["FRIEREN_CHARACTER.description"] = "跨越千年的精灵魔法使。解析敌人、隐匿魔力，并在关键回合释放法术。",
        ["FRIEREN_CHARACTER.pronounSubject"] = "她",
        ["FRIEREN_CHARACTER.pronounObject"] = "她",
        ["FRIEREN_CHARACTER.pronounPossessive"] = "她的",
        ["FRIEREN_CHARACTER.possessiveAdjective"] = "她的",
        ["FRIEREN_CHARACTER.cardsModifierTitle"] = "芙莉莲牌",
        ["FRIEREN_CHARACTER.cardsModifierDescription"] = "奖励中包含芙莉莲的法术牌。",
        ["FRIEREN_CHARACTER.unlockText"] = "测试角色：当前框架默认可用。",
        ["FRIEREN_CHARACTER.eventDeathPrevention"] = "现在倒下还太早。"
    };

    public static bool TryGet(string key, out string value)
    {
        return CharacterTexts.TryGetValue(key, out value!);
    }

    public static string Get(string key) => CharacterTexts[key];
}

[HarmonyPatch(typeof(CardModel), "get_Portrait")]
internal static class FrierenCardPortraitPatch
{
    private static bool Prefix(CardModel __instance, ref Texture2D __result)
    {
        if (__instance is not FrierenCard card || !card.HasFrierenCustomPortrait)
        {
            return true;
        }

        if (!FrierenAssetPaths.TryGetOrLoadTexture(card.PortraitPath, card.FrierenPortraitRelativePath, out var texture) || texture == null)
        {
            return true;
        }

        __result = texture;
        return false;
    }
}

[HarmonyPatch(typeof(LocTable), nameof(LocTable.GetRawText), new Type[] { typeof(string) })]
internal static class FrierenLocTableGetRawTextPatch
{
    private static bool Prefix(string key, ref string __result)
    {
        if (!key.StartsWith("FRIEREN_", StringComparison.Ordinal))
        {
            return true;
        }

        FrierenCharacterPatchHelpers.EnsureLocalizationReady();
        if (!FrierenFallbackLocalization.TryGet(key, out var text))
        {
            return true;
        }

        __result = text;
        return false;
    }
}

[HarmonyPatch(typeof(RelicModel), "get_Icon")]
internal static class FrierenRelicIconPatch
{
    private static bool Prefix(RelicModel __instance, ref Texture2D __result)
    {
        if (__instance is not FrierenRelic relic)
        {
            return true;
        }

        if (!FrierenAssetPaths.TryGetOrLoadTexture(relic.FrierenIconPath, relic.FrierenIconRelativePath, out var texture) || texture == null)
        {
            return true;
        }

        __result = texture;
        return false;
    }
}

[HarmonyPatch(typeof(RelicModel), "get_BigIcon")]
internal static class FrierenRelicBigIconPatch
{
    private static bool Prefix(RelicModel __instance, ref Texture2D __result)
    {
        if (__instance is not FrierenRelic relic)
        {
            return true;
        }

        if (!FrierenAssetPaths.TryGetOrLoadTexture(relic.FrierenIconPath, relic.FrierenIconRelativePath, out var texture) || texture == null)
        {
            return true;
        }

        __result = texture;
        return false;
    }
}

[HarmonyPatch(typeof(PotionModel), "get_ImagePath")]
internal static class FrierenPotionImagePathPatch
{
    private static bool Prefix(PotionModel __instance, ref string __result)
    {
        if (__instance is not FrierenPotion potion)
        {
            return true;
        }

        __result = potion.FrierenImagePath;
        return false;
    }
}

[HarmonyPatch(typeof(PotionModel), "get_Image")]
internal static class FrierenPotionImagePatch
{
    private static bool Prefix(PotionModel __instance, ref Texture2D __result)
    {
        if (__instance is not FrierenPotion potion)
        {
            return true;
        }

        if (!FrierenAssetPaths.TryGetOrLoadTexture(potion.FrierenImagePath, potion.FrierenImageRelativePath, out var texture) || texture == null)
        {
            return true;
        }

        __result = texture;
        return false;
    }
}

[HarmonyPatch(typeof(PotionModel), "get_OutlinePath")]
internal static class FrierenPotionOutlinePathPatch
{
    private static bool Prefix(PotionModel __instance, ref string? __result)
    {
        if (__instance is not FrierenPotion)
        {
            return true;
        }

        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(PotionModel), "get_Outline")]
internal static class FrierenPotionOutlinePatch
{
    private static bool Prefix(PotionModel __instance, ref Texture2D? __result)
    {
        if (__instance is not FrierenPotion)
        {
            return true;
        }

        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(ModelDb), "get_AllCharacters")]
internal static class FrierenAllCharactersPatch
{
    private static void Postfix(ref IEnumerable<CharacterModel> __result)
    {
        var withoutFrieren = __result.Where(character => !FrierenCharacterPatchHelpers.IsFrieren(character));
        if (FrierenCharacterPatchHelpers.IsFrierenCharacterSelectEnabled())
        {
            __result = FrierenCharacterPatchHelpers.AppendUnique(withoutFrieren, ModelDb.Character<FrierenCharacter>());
            return;
        }

        __result = withoutFrieren;
    }
}

[HarmonyPatch(typeof(ModelDb), "get_AllCharacterCardPools")]
internal static class FrierenAllCharacterCardPoolsPatch
{
    private static void Postfix(ref IEnumerable<CardPoolModel> __result)
    {
        __result = FrierenCharacterPatchHelpers.AppendUnique(__result, ModelDb.CardPool<FrierenCardPool>());
    }
}

[HarmonyPatch(typeof(ModelDb), "get_AllCardPools")]
internal static class FrierenAllCardPoolsPatch
{
    private static void Postfix(ref IEnumerable<CardPoolModel> __result)
    {
        __result = FrierenCharacterPatchHelpers.AppendUnique(__result, ModelDb.CardPool<FrierenCardPool>());
    }
}

[HarmonyPatch(typeof(ModelDb), "get_AllCards")]
internal static class FrierenAllCardsPatch
{
    private static void Postfix(ref IEnumerable<CardModel> __result)
    {
        var filteredBaseCards = __result.Where(FrierenCardCatalog.ShouldExposeToGlobalRandomCardPool);
        var extraCards = FrierenCardCatalog.GetAllCards();
        __result = FrierenCharacterPatchHelpers.AppendUnique(filteredBaseCards, extraCards);
    }
}

[HarmonyPatch(typeof(ModelDb), "get_AllCharacterPotionPools")]
internal static class FrierenAllCharacterPotionPoolsPatch
{
    private static void Postfix(ref IEnumerable<PotionPoolModel> __result)
    {
        __result = FrierenCharacterPatchHelpers.AppendUnique(__result, ModelDb.PotionPool<FrierenPotionPool>());
    }
}

[HarmonyPatch(typeof(ModelDb), "get_AllPotionPools")]
internal static class FrierenAllPotionPoolsPatch
{
    private static void Postfix(ref IEnumerable<PotionPoolModel> __result)
    {
        __result = FrierenCharacterPatchHelpers.AppendUnique(__result, ModelDb.PotionPool<FrierenPotionPool>());
    }
}

[HarmonyPatch(typeof(ModelDb), "get_AllPotions")]
internal static class FrierenAllPotionsPatch
{
    private static void Postfix(ref IEnumerable<PotionModel> __result)
    {
        __result = FrierenCharacterPatchHelpers.AppendUnique(__result, ModelDb.PotionPool<FrierenPotionPool>().AllPotions);
    }
}

[HarmonyPatch(typeof(ModelDb), "get_AllCharacterRelicPools")]
internal static class FrierenAllCharacterRelicPoolsPatch
{
    private static void Postfix(ref IEnumerable<RelicPoolModel> __result)
    {
        __result = FrierenCharacterPatchHelpers.AppendUnique(__result, ModelDb.RelicPool<FrierenRelicPool>());
    }
}

[HarmonyPatch(typeof(ModelDb), "get_CharacterRelicPools")]
internal static class FrierenCharacterRelicPoolsPatch
{
    private static void Postfix(ref IEnumerable<RelicPoolModel> __result)
    {
        __result = FrierenCharacterPatchHelpers.AppendUnique(__result, ModelDb.RelicPool<FrierenRelicPool>());
    }
}

[HarmonyPatch(typeof(ModelDb), "get_AllRelicPools")]
internal static class FrierenAllRelicPoolsPatch
{
    private static void Postfix(ref IEnumerable<RelicPoolModel> __result)
    {
        __result = FrierenCharacterPatchHelpers.AppendUnique(__result, ModelDb.RelicPool<FrierenRelicPool>());
    }
}

[HarmonyPatch(typeof(ModelDb), "get_AllRelics")]
internal static class FrierenAllRelicsPatch
{
    private static void Postfix(ref IEnumerable<RelicModel> __result)
    {
        var extraRelics = FrierenRelicCatalog.GetAllRelics()
            .Concat(ModelDb.Character<FrierenCharacter>().StartingRelics);
        __result = FrierenCharacterPatchHelpers.AppendUnique(__result, extraRelics);
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_AssetPaths")]
internal static class FrierenAssetPathsPatch
{
    private static bool Prefix(CharacterModel __instance, ref IEnumerable<string> __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = FrierenAssetPaths.RuntimeAssetPaths;
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_AssetPathsCharacterSelect")]
internal static class FrierenCharacterSelectAssetPathsPatch
{
    private static bool Prefix(CharacterModel __instance, ref IEnumerable<string> __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = FrierenAssetPaths.CharacterSelectAssetPaths;
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.CreateVisuals))]
internal static class FrierenCreateVisualsPatch
{
    private static bool Prefix(CharacterModel __instance, ref NCreatureVisuals __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = PreloadManager.Cache.GetScene(FrierenAssetPaths.CreatureVisuals)
            .Instantiate<NCreatureVisuals>(PackedScene.GenEditState.Disabled);
        FrierenCharacterPatchHelpers.ApplyCharacterVisual(__result);
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectIcon")]
internal static class FrierenCharacterSelectIconTexturePatch
{
    private static bool Prefix() => true;
}

[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectLockedIcon")]
internal static class FrierenCharacterSelectLockedIconTexturePatch
{
    private static bool Prefix() => true;
}

[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectTitle")]
internal static class FrierenCharacterSelectTitlePatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        FrierenCharacterPatchHelpers.EnsureLocalizationReady();
        __result = FrierenCharacterPatchHelpers.CharacterSelectTitleKey;
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectDesc")]
internal static class FrierenCharacterSelectDescriptionPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        FrierenCharacterPatchHelpers.EnsureLocalizationReady();
        __result = FrierenCharacterPatchHelpers.CharacterSelectDescriptionKey;
        return false;
    }
}

[HarmonyPatch(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Init))]
internal static class FrierenCharacterSelectButtonInitPatch
{
    private static void Postfix(NCharacterSelectButton __instance)
    {
        FrierenCharacterPatchHelpers.ApplyCharacterSelectButtonIcon(__instance);
    }
}

[HarmonyPatch(typeof(NCharacterSelectButton), "RefreshState")]
internal static class FrierenCharacterSelectButtonRefreshPatch
{
    private static void Postfix(NCharacterSelectButton __instance)
    {
        FrierenCharacterPatchHelpers.ApplyCharacterSelectButtonIcon(__instance);
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "InitCharacterButtons")]
internal static class FrierenCharacterSelectButtonsScreenPatch
{
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        FrierenCharacterPatchHelpers.SyncFrierenCharacterSelectButton(__instance);
        FrierenCharacterPatchHelpers.ApplyAllCharacterSelectButtonIcons(__instance);
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "OnSubmenuOpened")]
internal static class FrierenCharacterSelectOpenPatch
{
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        FrierenCharacterPatchHelpers.SyncFrierenCharacterSelectButton(__instance);
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "OnSubmenuClosed")]
internal static class FrierenCharacterSelectClosePatch
{
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        FrierenCharacterPatchHelpers.ClearFrierenCharacterSelectGate();
        FrierenCharacterPatchHelpers.SyncFrierenCharacterSelectButton(__instance);
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "SelectCharacter")]
internal static class FrierenCharacterSelectScreenPatch
{
    private static void Prefix(CharacterModel characterModel)
    {
        if (FrierenCharacterPatchHelpers.IsFrieren(characterModel))
        {
            FrierenCharacterPatchHelpers.EnsureLocalizationReady();
        }
    }

    private static void Postfix(NCharacterSelectScreen __instance, CharacterModel characterModel)
    {
        if (FrierenCharacterPatchHelpers.IsFrieren(characterModel))
        {
            FrierenCharacterPatchHelpers.ApplyCharacterSelectCover(__instance);
            return;
        }

        FrierenCharacterPatchHelpers.RestoreCharacterSelectCover(__instance);
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_IconTexture")]
internal static class FrierenIconTexturePatch
{
    private static bool Prefix(CharacterModel __instance, ref Texture2D __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = PreloadManager.Cache.GetTexture2D(FrierenAssetPaths.TopPanelIcon);
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_IconOutlineTexture")]
internal static class FrierenIconOutlineTexturePatch
{
    private static bool Prefix(CharacterModel __instance, ref Texture2D __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = PreloadManager.Cache.GetTexture2D(FrierenAssetPaths.TopPanelIconOutline);
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_EnergyCounterPath")]
internal static class FrierenEnergyCounterPathPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = FrierenAssetPaths.EnergyCounter;
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_MerchantAnimPath")]
internal static class FrierenMerchantAnimPathPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = FrierenAssetPaths.Merchant;
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_RestSiteAnimPath")]
internal static class FrierenRestSiteAnimPathPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = FrierenAssetPaths.RestSite;
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_TrailPath")]
internal static class FrierenTrailPathPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = FrierenAssetPaths.CardTrail;
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectBg")]
internal static class FrierenCharacterSelectBgPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = FrierenAssetPaths.CharacterSelectBackground;
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectTransitionPath")]
internal static class FrierenTransitionPathPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = FrierenAssetPaths.TransitionMaterial;
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_ArmPointingTexture")]
[HarmonyPatch(typeof(CharacterModel), "get_ArmRockTexture")]
[HarmonyPatch(typeof(CharacterModel), "get_ArmPaperTexture")]
[HarmonyPatch(typeof(CharacterModel), "get_ArmScissorsTexture")]
internal static class FrierenArmTexturePatch
{
    private static bool Prefix(CharacterModel __instance, ref Texture2D __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = PreloadManager.Cache.GetTexture2D(FrierenAssetPaths.MultiplayerHandPoint);
        return false;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_RunWonAchievement")]
internal static class FrierenRunWonAchievementPatch
{
    private static bool Prefix(CharacterModel __instance, ref Achievement __result)
    {
        if (!FrierenCharacterPatchHelpers.IsFrieren(__instance))
        {
            return true;
        }

        __result = Achievement.IroncladWin;
        return false;
    }
}
