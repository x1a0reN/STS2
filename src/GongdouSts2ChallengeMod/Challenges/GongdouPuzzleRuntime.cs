using GongdouSts2ChallengeMod.Models;
using GongdouSts2ChallengeMod.Cards;
using GongdouSts2ChallengeMod.Monsters;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using System.Reflection;

namespace GongdouSts2ChallengeMod.Challenges;

public static class GongdouPuzzleRuntime
{
    private sealed record Orb(string Type, decimal Value);

    private static int _stage;
    private static HashSet<string> _cards = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> _potions = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> _relics = new(StringComparer.OrdinalIgnoreCase);

    private static bool _combatStarted;
    private static int _currentRound = -1;
    private static int _playerCardsThisRound;

    private static bool _d3DiscardedThisTurn;
    private static bool _d3MisdiscardArmor;
    private static bool _d3ActiveCunningArmor;
    private static bool _d3HadCunningFreeThisTurn;
    private static bool _d3SharpDiceUsedThisTurn;
    private static int _d3CunningFreeThisCombat;
    private static int _d3ConsecutiveCunningTurns;

    private static int _d4Burns;
    private static bool _d4GhostActive;
    private static bool _d4StatusEnteredThisCombat;
    private static int _d4FireBreathing;
    private static int _d4Evolve;

    private static int _d5VoidAvailable;
    private static int _d5VoidDrawnTotal;
    private static int _d5AllowedKillTurn = 5;
    private static int _d5AttackDamageEventsThisTurn;
    private static bool _d5VoidCollapseTriggered;
    private static bool _d5VoidLensUsedThisTurn;
    private static bool _d5AnchorGranted;

    private static string _d6Stance = "normal";
    private static bool _d6ChangedStanceThisTurn;
    private static bool _d6RageCharmUsedThisTurn;
    private static bool _d6StanceSealArmed;
    private static bool _d6StanceSealUsedThisTurn;
    private static CardModel? _d6StanceSealSourceCard;
    private static int _d6CalmExits;
    private static bool _d6CalmBreachTriggered;

    private static bool _d7NoxiousFumes;
    private static bool _d7Caltrops;
    private static bool _d7RingUsedThisTurn;
    private static bool _d7GhostActive;
    private static decimal _d7PoisonFunnelPoisonAtPlayerEnd;

    private static int _d8Focus;
    private static int _d8Loop;
    private static List<Orb> _d8Orbs = [];

    private static int _d9Mantra;
    private static bool _d9Divinity;
    private static bool _d9SundialArmed;
    private static bool _d9SundialConsumed;

    private static int _d10Charge;
    private static int _d10Mark;
    private static int _d10Echo;
    private static int _d10Overheat;
    private static int _d10DelayedDamage;
    private static int _d10AllowedKillTurn = 4;
    private static bool _d10ResonatorUsed;

    public static int CurrentStage => _stage;
    public static bool IsD8Active => _stage == 8;

    private static readonly FieldInfo? DarkOrbEvokeValueField =
        typeof(DarkOrb).GetField("_evokeVal", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Configure(Sts2PuzzleConfig config, ChallengeSelection selection)
    {
        _stage = config.StageIndex;
        _cards = selection.CardIds.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _potions = selection.PotionIds.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _relics = selection.RelicIds.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ResetCombatState();

        if (HasPotion("D5_BarrierPotion"))
        {
            _d5AllowedKillTurn = 3;
        }
        else if (HasPotion("D2_FirePotion") || HasPotion("FirePotion"))
        {
            _d5AllowedKillTurn = 4;
        }

        if (HasPotion("D10_TimePotion"))
        {
            _d10AllowedKillTurn = 1;
        }
        else if (HasPotion("D10_EchoPotion"))
        {
            _d10AllowedKillTurn = 2;
        }
        else if (HasPotion("D10_ShatterArmorPotion"))
        {
            _d10AllowedKillTurn = 3;
        }
    }

    public static async Task OnCombatStart(Creature enemy)
    {
        if (_combatStarted)
        {
            return;
        }

        _combatStarted = true;
        var player = FindPlayer(enemy.CombatState);
        if (player == null)
        {
            return;
        }

        await ApplyKeywordPowers(player.Creature, enemy);

        if (_stage == 8 && HasRelic("D8_DataDisk"))
        {
            _d8Focus++;
        }

        if (_stage == 8)
        {
            await EnsureD8OrbSlots(player);
            var startupContext = new BlockingPlayerChoiceContext();
            await D8ChannelOrb<LightningOrb>(startupContext, player);
            await D8ChannelOrb<FrostOrb>(startupContext, player);
        }
    }

    public static async Task<bool> TryPlayCardAsync(CardModel card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_stage < 2)
        {
            return false;
        }

        var id = Normalize(card.Id.Entry);
        var target = ResolveEnemyTarget(card, cardPlay.Target);
        var handled = true;

        switch (id)
        {
            case "d3prepared":
                await D3DiscardByPriority(choiceContext, card, target);
                break;
            case "d3survivor":
                await GainBlock(card.Owner.Creature, 5m);
                await D3DiscardByPriority(choiceContext, card, target);
                break;
            case "d3finisher":
                await DealAttack(choiceContext, card, target, 8m + _d3CunningFreeThisCombat * 4m);
                break;
            case "d3backstabcunning":
                if (cardPlay.IsAutoPlay)
                {
                    await D3TriggerCunning(choiceContext, card, card.Owner, target, id);
                    break;
                }

                _d3ActiveCunningArmor = true;
                await DealAttack(choiceContext, card, target, 6m);
                break;
            case "d3shadowstep":
                if (cardPlay.IsAutoPlay)
                {
                    await D3TriggerCunning(choiceContext, card, card.Owner, target, id);
                    break;
                }

                _d3ActiveCunningArmor = true;
                await DealAttack(choiceContext, card, target, 5m);
                await GainBlock(card.Owner.Creature, 3m);
                break;
            case "d3feint":
                await DealAttack(choiceContext, card, target, _d3HadCunningFreeThisTurn ? 11m : 7m);
                break;
            case "d3voidblade":
                await DealAttack(choiceContext, card, target, 4m);
                break;

            case "strikeironclad":
                await DealAttack(choiceContext, card, target, 6m);
                break;
            case "defendironclad":
                await GainBlock(card.Owner.Creature, 5m);
                break;
            case "bash":
                await DealAttack(choiceContext, card, target, 8m);
                await ApplyVulnerable(target, 2m, card.Owner.Creature, card);
                break;
            case "neutralize":
                await DealAttack(choiceContext, card, target, 3m);
                await ApplyWeak(target, 1m, card.Owner.Creature, card);
                break;
            case "uppercut":
                await DealAttack(choiceContext, card, target, 13m);
                await ApplyVulnerable(target, 1m, card.Owner.Creature, card);
                await ApplyWeak(target, 1m, card.Owner.Creature, card);
                break;
            case "clothesline":
            case "d2clothesline":
            case "d5clothesline":
                await DealAttack(choiceContext, card, target, 12m);
                await ApplyWeak(target, 2m, card.Owner.Creature, card);
                break;
            case "ironwave":
                await DealAttack(choiceContext, card, target, 5m);
                await GainBlock(card.Owner.Creature, 5m);
                break;
            case "survivor":
                await GainBlock(card.Owner.Creature, 8m);
                break;
            case "quickslash":
            case "d2quickslash":
            case "d5quickslash":
                var quickSlashDamage = _stage switch
                {
                    2 => (target.GetPower<VulnerablePower>()?.Amount ?? 0m) > 0m ? 11m : 7m,
                    5 => HasArtifact(target) ? 7m : 11m,
                    _ => 8m
                };
                await DealAttack(choiceContext, card, target, quickSlashDamage);
                break;
            case "daggerthrow":
                await DealAttack(choiceContext, card, target, 9m);
                break;
            case "balllightning":
            case "d2balllightning":
            case "d5balllightning":
                var ballLightningDamage = _stage switch
                {
                    2 => target.Block > 0m ? 11m : 5m,
                    5 => FindD5VoidResource(card.Owner) != null ? 12m : 5m,
                    _ => 7m
                };
                await DealAttack(choiceContext, card, target, ballLightningDamage);
                break;
            case "flyingsword":
                await DealAttack(choiceContext, card, target, 3m, hits: 3);
                break;

            case "d4carnage":
                await DealAttack(choiceContext, card, target, 18m);
                break;
            case "d4burningpact":
                await DealAttack(choiceContext, card, target, _d4Burns > 0 ? 14m : 9m);
                if (_d4Burns > 0) _d4Burns--;
                break;
            case "d4truegrit":
            case "d4survivor":
                await GainBlock(card.Owner.Creature, 7m);
                if (_d4Burns > 0) _d4Burns--;
                break;
            case "d4firebreathing":
                _d4FireBreathing++;
                await PowerCmd.Apply<GongdouD4FireBreathingPower>(card.Owner.Creature, 1m, card.Owner.Creature, card);
                break;
            case "d4evolve":
                _d4Evolve++;
                await PowerCmd.Apply<GongdouD4EvolvePower>(card.Owner.Creature, 1m, card.Owner.Creature, card);
                break;
            case "d4wildstrike":
                await DealAttack(choiceContext, card, target, 12m);
                await AddD4StatusToDrawPile<Wound>(choiceContext, card.Owner, 1);
                break;
            case "d4recklesscharge":
                await DealAttack(choiceContext, card, target, 7m);
                await AddD4StatusToDrawPile<Dazed>(choiceContext, card.Owner, 1);
                break;
            case "d4cleave":
                await DealAttack(choiceContext, card, target, _d4StatusEnteredThisCombat ? 12m : 8m);
                break;

            case "d5voidrend":
                var voidRendConsumedVoid = await TryConsumeD5Void(choiceContext, card.Owner);
                var voidRendDamage = voidRendConsumedVoid ? 22m : 8m;
                if (voidRendConsumedVoid && HasRelic("D5_VoidLens") && !_d5VoidLensUsedThisTurn)
                {
                    _d5VoidLensUsedThisTurn = true;
                    voidRendDamage += 24m;
                }
                await DealAttack(choiceContext, card, target, voidRendDamage);
                break;
            case "d5coldsnap":
                await DealAttack(choiceContext, card, target, 6m);
                await GainBlock(card.Owner.Creature, 4m);
                break;
            case "d5recycle":
                var recycleConsumedVoid = await TryConsumeD5Void(choiceContext, card.Owner);
                await GainBlock(card.Owner.Creature, recycleConsumedVoid ? 13m : 5m);
                if (recycleConsumedVoid)
                {
                    await PlayerCmd.GainEnergy(1m, card.Owner);
                    await ApplyWeak(target, 1m, card.Owner.Creature, card);
                }
                break;
            case "d5feint":
                await DealAttack(choiceContext, card, target, 4m);
                break;

            case "d6eruption":
                await DealAttack(choiceContext, card, target, 9m);
                await D6EnterStance(card.Owner, "wrath", card);
                break;
            case "d6vigilance":
                await GainBlock(card.Owner.Creature, 8m);
                await D6EnterStance(card.Owner, "calm", card);
                break;
            case "d6emptyfist":
                await DealAttack(choiceContext, card, target, 9m);
                await D6EnterStance(card.Owner, "normal", card);
                break;
            case "d6emptybody":
                await GainBlock(card.Owner.Creature, 8m);
                await D6EnterStance(card.Owner, "normal", card);
                break;
            case "d6followup":
                await DealAttack(choiceContext, card, target, _d6ChangedStanceThisTurn ? 8m : 4m);
                break;
            case "d6wheelkick":
                await DealAttack(choiceContext, card, target, 15m);
                break;
            case "d6offering":
                await DealAttack(choiceContext, card, target, 5m);
                break;
            case "d6cutthroughfate":
                await DealAttack(choiceContext, card, target, _d6Stance == "calm" ? 11m : 6m);
                break;
            case "d6bowlingbash":
                await DealAttack(choiceContext, card, target, target.Block > 0m ? 12m : 8m);
                break;
            case "d6protect":
                await GainBlock(card.Owner.Creature, 11m);
                break;
            case "d6halt":
                await GainBlock(card.Owner.Creature, _d6Stance == "wrath" ? 9m : 4m);
                break;

            case "d7deadlypoison":
                await ApplyPoison(target, 5m, card);
                break;
            case "d7bouncingflask":
                await ApplyPoison(target, 8m, card);
                break;
            case "d7poisonedstab":
                await DealAttack(choiceContext, card, target, 6m);
                await ApplyPoison(target, 3m, card);
                break;
            case "d7catalyst":
                await D7Catalyst(target, card);
                break;
            case "d7noxiousfumes":
                _d7NoxiousFumes = true;
                if (card.Owner.Creature.GetPower<GongdouD7PoisonFogKeywordPower>() == null)
                {
                    await PowerCmd.Apply<GongdouD7PoisonFogKeywordPower>(card.Owner.Creature, 1m, card.Owner.Creature, card, silent: true);
                }
                break;
            case "d7bane":
                await DealAttack(choiceContext, card, target, target.GetPowerAmount<PoisonPower>() > 0 ? 14m : 7m);
                break;
            case "d7predator":
                await DealAttack(choiceContext, card, target, 15m);
                break;
            case "d7legsweep":
                await GainBlock(card.Owner.Creature, 11m);
                await ApplyWeak(target, 2m, card.Owner.Creature, card);
                break;
            case "d7cloakanddagger":
                await GainBlock(card.Owner.Creature, 6m);
                await DealAttack(choiceContext, card, target, 4m);
                break;
            case "d7backflip":
                await GainBlock(card.Owner.Creature, 7m);
                break;
            case "d7caltrops":
                _d7Caltrops = true;
                if (card.Owner.Creature.GetPower<GongdouD7CaltropsKeywordPower>() == null)
                {
                    await PowerCmd.Apply<GongdouD7CaltropsKeywordPower>(card.Owner.Creature, 1m, card.Owner.Creature, card, silent: true);
                }
                break;

            case "d8zap":
                var hadOrbBeforeZap = card.Owner.PlayerCombatState?.OrbQueue.Orbs.Any() == true;
                await D8ChannelOrb<LightningOrb>(choiceContext, card.Owner);
                if (!hadOrbBeforeZap)
                {
                    await D8ChannelOrb<LightningOrb>(choiceContext, card.Owner);
                }
                break;
            case "d8dualcast":
                await D8EvokeLeft(choiceContext, card.Owner);
                await D8EvokeLeft(choiceContext, card.Owner);
                break;
            case "d8darkness":
                await D8ChannelDarkOrb(choiceContext, card.Owner, HasRelic("D8_DarkCore") ? 10m : null);
                break;
            case "d8recursion":
                await D8Recursion(choiceContext, card.Owner);
                break;
            case "d8loop":
                _d8Loop++;
                if (card.Owner.Creature.GetPower<GongdouD8LoopKeywordPower>() == null)
                {
                    await PowerCmd.Apply<GongdouD8LoopKeywordPower>(card.Owner.Creature, 1m, card.Owner.Creature, card, silent: true);
                }
                break;
            case "d8chill":
                await D8ChannelOrb<FrostOrb>(choiceContext, card.Owner);
                break;
            case "d8coldsnap":
                await DealAttack(choiceContext, card, target, 6m);
                await D8ChannelOrb<FrostOrb>(choiceContext, card.Owner);
                break;
            case "d8balllightningorb":
                await DealAttack(choiceContext, card, target, 7m);
                await D8ChannelOrb<LightningOrb>(choiceContext, card.Owner);
                break;
            case "d8melter":
                await DealUnblockedAttack(choiceContext, card, target, 10m);
                break;
            case "d8streamline":
                await DealAttack(choiceContext, card, target, 15m);
                break;
            case "d8leap":
                await GainBlock(card.Owner.Creature, 9m);
                break;
            case "d8coolheaded":
                await GainBlock(card.Owner.Creature, 5m);
                await D8ChannelOrb<FrostOrb>(choiceContext, card.Owner);
                break;

            case "d9devotion":
                await D9AddMantra(card.Owner, 4);
                break;
            case "d9prostrate":
                await GainBlock(card.Owner.Creature, 4m);
                await D9AddMantra(card.Owner, 2);
                break;
            case "d9prayer":
                await GainBlock(card.Owner.Creature, 5m);
                await D9AddMantra(card.Owner, 3);
                break;
            case "d9worship":
                await D9AddMantra(card.Owner, 5);
                break;
            case "d9brilliance":
                await DealAttack(choiceContext, card, target, 8m + _d9Mantra * 2m);
                break;
            case "d9ragnarok":
                await DealAttack(choiceContext, card, target, 5m, hits: 4);
                break;
            case "d9judgment":
                if (target.CurrentHp <= 30)
                {
                    await DealLifeLoss(choiceContext, card.Owner.Creature, target, target.CurrentHp);
                }
                else
                {
                    await DealAttack(choiceContext, card, target, 5m);
                    await GainBlock(target, 16m);
                }
                break;
            case "d9carvereality":
                await DealAttack(choiceContext, card, target, 6m);
                break;
            case "d9smite":
                await DealAttack(choiceContext, card, target, 12m);
                break;
            case "d9offering":
                await DealAttack(choiceContext, card, target, 5m);
                break;
            case "d9sanctity":
                await GainBlock(card.Owner.Creature, _d9Mantra >= 5 ? 12m : 8m);
                break;
            case "d9wallop":
                await GainBlock(card.Owner.Creature, 13m);
                break;
            case "d9emptybody":
                await GainBlock(card.Owner.Creature, 7m);
                if (_d9Divinity)
                {
                    await PlayerCmd.GainEnergy(1m, card.Owner);
                    _d9Divinity = false;
                    await RefreshD9DynamicKeywordPowers(card.Owner.Creature);
                }
                break;

            case "d10timeseal":
                _d10Charge += 3;
                break;
            case "d10chargestance":
                _d10Charge += 1;
                await GainBlock(card.Owner.Creature, 3m);
                break;
            case "d10riftmark":
                D10AddMark(2);
                break;
            case "d10echostrike":
                await DealAttack(choiceContext, card, target, 7m + _d10Charge * 2m + _d10Mark);
                break;
            case "d10echoform":
                _d10Echo += 1;
                break;
            case "d10delayedblast":
                _d10DelayedDamage += 18 + _d10Mark * 4 + (!HasRelic("D10_Resonator") || _d10ResonatorUsed ? 0 : 12);
                _d10ResonatorUsed = _d10ResonatorUsed || HasRelic("D10_Resonator");
                break;
            case "d10overloadray":
                await DealAttack(choiceContext, card, target, 12m + _d10Charge * 3m);
                _d10Overheat += 2;
                break;
            case "d10ventheat":
                _d10Overheat = Math.Max(0, _d10Overheat - 2);
                await GainBlock(card.Owner.Creature, 6m);
                break;
            case "d10phasebarrier":
                await GainBlock(card.Owner.Creature, 8m + Math.Min(6, _d10Charge));
                break;
            case "d10focuscalibrate":
                _d10Charge += 2;
                D10AddMark(1);
                break;
            case "d10finalcommand":
                if (target.CurrentHp <= 32 + _d10Mark * 4)
                {
                    await DealLifeLoss(choiceContext, card.Owner.Creature, target, ClampByGate(target, target.CurrentHp, unblockable: true, allowedTurn: _d10AllowedKillTurn));
                }
                else
                {
                    await DealAttack(choiceContext, card, target, 8m + _d10Mark);
                }
                break;
            case "d10mirrorpreview":
                _d10Echo += 1;
                _d10Charge += 1;
                break;
            case "d10burningshot":
                await DealAttack(choiceContext, card, target, 6m + _d10Mark);
                _d10Overheat += 1;
                break;
            case "d10coolingloop":
                await GainBlock(card.Owner.Creature, 5m);
                _d10Charge += 1;
                _d10Overheat = Math.Max(0, _d10Overheat - 1);
                break;
            case "d10idleprogram":
                await GainBlock(card.Owner.Creature, 2m);
                if (_d10Overheat > 0)
                {
                    _d10Mark = Math.Max(0, _d10Mark - 1);
                }
                break;
            case "d10spikemark":
                D10AddMark(1);
                await DealAttack(choiceContext, card, target, 5m);
                break;
            default:
                handled = false;
                break;
        }

        if (handled && _stage == 10 && card.Owner?.Creature is { } d10Player)
        {
            var d10Enemy = d10Player.CombatState?.Enemies.FirstOrDefault(c => c.IsAlive)
                ?? (target.IsAlive ? target : null);
            if (d10Enemy != null)
            {
                await RefreshD10DynamicKeywordPowers(d10Player, d10Enemy);
            }
        }

        return handled;
    }

    public static bool ShouldPlayCard(CardModel card, Creature enemyOwner, AutoPlayType autoPlayType)
    {
        if (!HasActiveCardLimit() || card.Owner?.Creature == null || card.Owner.Creature.Side == enemyOwner.Side)
        {
            return true;
        }

        if (autoPlayType == AutoPlayType.SlyDiscard)
        {
            return true;
        }

        var round = enemyOwner.CombatState?.RoundNumber ?? _currentRound;
        if (round != _currentRound)
        {
            ResetPerTurn(round);
        }

        return _playerCardsThisRound < 2;
    }

    public static async Task<bool> TryUsePotionAsync(PotionModel potion, PlayerChoiceContext choiceContext, Creature? target)
    {
        if (_stage < 3)
        {
            return false;
        }

        var id = Normalize(potion.Id.Entry);
        var player = potion.Owner;
        var enemy = PotionNeedsEnemyTarget(id)
            ? ValidatePotionEnemyTarget(id, target, player)
            : null;

        switch (id)
        {
            case "d3cunningpotion":
                await D3DiscardByPriority(choiceContext, null, ResolveEnemyTarget(player.Creature.CombatState), player);
                return true;
            case "d4firepotion":
                await DealLifeLoss(choiceContext, player.Creature, enemy!, 20m, unblockable: true);
                return true;
            case "d4claritypotion":
                var consumed = Math.Min(2, _d4Burns);
                _d4Burns -= consumed;
                await DealLifeLoss(choiceContext, player.Creature, enemy!, Math.Max(1, consumed) * 13m);
                return true;
            case "d4ghostpotion":
                if (_stage == 7)
                {
                    _d7GhostActive = true;
                }
                else
                {
                    _d4GhostActive = true;
                }
                return true;
            case "d2firepotion" when _stage == 5:
                await DealLifeLoss(choiceContext, player.Creature, enemy!, ClampByGate(enemy!, 20m, unblockable: true, allowedTurn: _d5AllowedKillTurn), unblockable: true);
                return true;
            case "d5barrierpotion":
                await RemoveD5Artifact(enemy!, decimal.MaxValue);
                await ApplyD5BarrierVulnerable(enemy!, 2m, player.Creature);
                return true;
            case "d5energypotion":
                await PlayerCmd.GainEnergy(3m, player);
                await GainBlock(player.Creature, 20m);
                return true;
            case "d6ragepotion":
                await PlayerCmd.GainEnergy(1m, player);
                await D6EnterStance(player, "wrath");
                return true;
            case "d6calmpotion":
                await GainBlock(player.Creature, 6m);
                await D6EnterStance(player, "calm");
                return true;
            case "d6firepotion":
                await DealLifeLoss(choiceContext, player.Creature, enemy!, 18m, unblockable: true);
                return true;
            case "d7poisonpotion":
                await ApplyPoison(enemy!, 10m, null, player.Creature);
                return true;
            case "d7catalystpotion":
                await D7Catalyst(enemy!, null, player.Creature);
                return true;
            case "d8focuspotion":
                _d8Focus += 2;
                return true;
            case "d8darkpotion":
                await D8ChannelDarkOrb(choiceContext, player, HasRelic("D8_DarkCore") ? 18m : 14m);
                return true;
            case "d8evokepotion":
                await D8EvokeLeft(choiceContext, player);
                await D8EvokeLeft(choiceContext, player);
                return true;
            case "d9divinitypotion":
                D9EnterDivinity();
                await RefreshD9DynamicKeywordPowers(player.Creature);
                return true;
            case "d9mantrapotion":
                await D9AddMantra(player, 6);
                return true;
            case "d9mirrorbreakpotion":
                var d9MirrorBreakTarget = ResolveEnemyTarget(player.Creature.CombatState);
                d9MirrorBreakTarget.LoseBlockInternal(Math.Min(d9MirrorBreakTarget.Block, 20m));
                return true;
            case "d10timepotion":
                _d10Charge += 6;
                _d10Echo += 1;
                await RefreshD10DynamicKeywordPowers(player.Creature, ResolveEnemyTarget(player.Creature.CombatState));
                return true;
            case "d10echopotion":
                _d10Echo += 2;
                await RefreshD10DynamicKeywordPowers(player.Creature, ResolveEnemyTarget(player.Creature.CombatState));
                return true;
            case "d10shatterarmorpotion":
                enemy!.LoseBlockInternal(Math.Min(enemy.Block, 30m));
                D10AddMark(2);
                await RefreshD10DynamicKeywordPowers(player.Creature, enemy);
                return true;
            default:
                return false;
        }
    }

    private static bool PotionNeedsEnemyTarget(string normalizedPotionId)
    {
        return normalizedPotionId is
            "d4firepotion" or
            "d4claritypotion" or
            "d2firepotion" or
            "d5barrierpotion" or
            "d6firepotion" or
            "d7poisonpotion" or
            "d7catalystpotion" or
            "d10shatterarmorpotion";
    }

    private static Creature ValidatePotionEnemyTarget(string normalizedPotionId, Creature? target, Player player)
    {
        if (target == null || target.Side == player.Creature.Side)
        {
            throw new InvalidOperationException($"Challenge potion {normalizedPotionId} requires the normal enemy target selection flow.");
        }

        return target;
    }

    public static async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay, Creature enemyOwner)
    {
        var card = cardPlay.Card;
        if (card.Owner?.Creature.Side == enemyOwner.Side)
        {
            return;
        }

        var round = enemyOwner.CombatState?.RoundNumber ?? _currentRound;
        if (round != _currentRound)
        {
            ResetPerTurn(round);
        }

        if (HasActiveCardLimit() && !cardPlay.IsAutoPlay)
        {
            _playerCardsThisRound++;
        }

        if (_stage == 6 &&
            card.Type == CardType.Attack &&
            _d6StanceSealArmed &&
            !ReferenceEquals(card, _d6StanceSealSourceCard))
        {
            _d6StanceSealArmed = false;
            _d6StanceSealSourceCard = null;
        }
    }

    private static async Task ApplyKeywordPowers(Creature player, Creature enemy)
    {
        async Task PlayerKeyword<T>() where T : PowerModel
        {
            await PowerCmd.Apply<T>(player, 1m, player, null, silent: true);
        }

        async Task EnemyKeyword<T>() where T : PowerModel
        {
            await PowerCmd.Apply<T>(enemy, 1m, enemy, null, silent: true);
        }

        if (_stage >= 2 && _stage != 4 && _stage != 10)
        {
            await EnemyKeyword<GongdouPersistentArmorKeywordPower>();
        }

        switch (_stage)
        {
            case 3:
                if (player.GetPower<GongdouD3DexterityKeywordPower>() is { } obsoleteCunningKeyword)
                {
                    obsoleteCunningKeyword.RemoveInternal();
                }

                await PlayerKeyword<GongdouD3BellLimitKeywordPower>();
                await PlayerKeyword<GongdouD3StickyHandKeywordPower>();
                await PlayerKeyword<GongdouD3FalseBladeKeywordPower>();
                await EnemyKeyword<GongdouD3ReadKeywordPower>();
                await EnemyKeyword<GongdouD3WrongDiscardArmorKeywordPower>();
                await PlayerKeyword<GongdouD3ChainBreachKeywordPower>();
                await EnemyKeyword<GongdouD3NoDexterityArmorKeywordPower>();
                break;
            case 4:
                await EnemyKeyword<GongdouD4BurnKeywordPower>();
                break;
            case 5:
                await PlayerKeyword<GongdouD5OverloadLockKeywordPower>();
                await PlayerKeyword<GongdouD5VoidCollapseKeywordPower>();
                await PowerCmd.Apply<ArtifactPower>(enemy, 3m, enemy, null, silent: true);
                await ApplyD5PotionPhasePower(enemy);
                break;
            case 6:
                await RefreshD6StancePower(player);
                await PlayerKeyword<GongdouD6CalmBreachKeywordPower>();
                break;
            case 7:
                await RefreshD7DynamicKeywordPowers(player, enemy);
                break;
            case 8:
                await PlayerKeyword<GongdouD8OrbSlotKeywordPower>();
                await PlayerKeyword<GongdouD8InsulationKeywordPower>();
                break;
            case 9:
                await RefreshD9DynamicKeywordPowers(player);
                await EnemyKeyword<GongdouD9ArmorReflectKeywordPower>();
                break;
            case 10:
                await RefreshD10DynamicKeywordPowers(player, enemy);
                break;
        }
    }

    public static async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
    {
        var enemy = combatState.Enemies.FirstOrDefault(c => c.IsAlive);
        if (enemy == null)
        {
            return;
        }

        if (side == CombatSide.Player)
        {
            ResetPerTurn(combatState.RoundNumber);
            await OnPlayerTurnStart(combatState, enemy);
        }
        else
        {
            await OnEnemyTurnStart(enemy);
        }
    }

    public static async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, Creature enemyOwner)
    {
        var player = FindPlayer(enemyOwner.CombatState);
        if (player == null)
        {
            return;
        }

        if (side == CombatSide.Player)
        {
            await OnPlayerTurnEnd(choiceContext, player, enemyOwner);
        }
        else if (side == enemyOwner.Side)
        {
            await OnEnemyTurnEnd(enemyOwner);
        }
    }

    public static async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, Creature enemyOwner)
    {
        if (card.Owner?.Creature == null)
        {
            return;
        }

        if (_stage == 5)
        {
            if (IsD5VoidCard(card))
            {
                _d5VoidAvailable++;
                _d5VoidDrawnTotal++;
                await PlayerCmd.LoseEnergy(1m, card.Owner);
            }
            return;
        }

        if (_stage != 4)
        {
            return;
        }

        var isD4StatusOrCurse = IsD4FireBreathingTrigger(card);
        if (_d4FireBreathing > 0 && isD4StatusOrCurse)
        {
            var enemies = card.Owner.Creature.CombatState?.Enemies.Where(enemy => enemy.IsAlive).ToList() ?? [];
            foreach (var enemy in enemies)
            {
                await DealLifeLoss(choiceContext, card.Owner.Creature, enemy, 6m * _d4FireBreathing, unblockable: false);
            }
        }

        if (_d4Evolve > 0 && card.Type == CardType.Status)
        {
            await CardPileCmd.Draw(choiceContext, _d4Evolve, card.Owner);
        }
    }

    public static int ModifyEnemyAttackDamage(int damage)
    {
        var result = damage;
        if (_stage == 4 && _d4GhostActive)
        {
            result = (int)Math.Ceiling(result / 2m);
            _d4GhostActive = false;
        }
        if (_stage == 7 && _d7GhostActive)
        {
            result = (int)Math.Floor(result / 2m);
            _d7GhostActive = false;
        }

        if (_stage == 6 && _d6Stance == "wrath")
        {
            result *= 2;
        }

        if (_stage == 10)
        {
            result += _d10Overheat * 4;
        }

        return Math.Max(0, result);
    }

    public static decimal ModifyDamageMultiplicative(Creature ruleOwner, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (IsD6PlayerAttack(ruleOwner, target, dealer, cardSource) && _d6Stance == "wrath")
        {
            return 2m;
        }

        return 1m;
    }

    public static decimal ModifyDamageAdditive(Creature ruleOwner, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!IsD6PlayerAttack(ruleOwner, target, dealer, cardSource))
        {
            return 0m;
        }

        var bonus = 0m;
        if (_d6Stance == "calm" && HasRelic("D6_VioletLotus"))
        {
            bonus += 8m;
        }

        if (_d6StanceSealArmed && !ReferenceEquals(cardSource, _d6StanceSealSourceCard))
        {
            bonus += 5m;
        }

        return bonus;
    }

    public static decimal ModifyDamageCap(Creature ruleOwner, Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if ((_stage != 5 && _stage != 10) ||
            target != ruleOwner ||
            dealer?.Side == ruleOwner.Side ||
            cardSource?.Type != CardType.Attack)
        {
            return decimal.MaxValue;
        }

        var gateTurn = _stage == 5 ? _d5AllowedKillTurn : _d10AllowedKillTurn;
        var round = target.CombatState?.RoundNumber ?? 1;
        if (gateTurn <= 0 || round >= gateTurn)
        {
            return decimal.MaxValue;
        }

        return Math.Max(0m, target.Block + target.CurrentHp - 1m);
    }

    private static bool IsD6PlayerAttack(Creature ruleOwner, Creature? target, Creature? dealer, CardModel? cardSource)
    {
        return _stage == 6 &&
               target == ruleOwner &&
               dealer != null &&
               dealer.Side != ruleOwner.Side &&
               cardSource?.Type == CardType.Attack;
    }

    public static async Task AfterEnemyAttack(PlayerChoiceContext? choiceContext, Creature enemy)
    {
        if (_stage == 7 && _d7Caltrops && enemy.IsAlive)
        {
            await DealLifeLoss(choiceContext, FindPlayer(enemy.CombatState)?.Creature, enemy, 6m);
        }
    }

    private static async Task OnPlayerTurnStart(CombatState combatState, Creature enemy)
    {
        var player = FindPlayer(combatState);
        if (player == null)
        {
            return;
        }

        if (_stage == 3 && combatState.RoundNumber >= 4 && _d3ConsecutiveCunningTurns >= 3)
        {
            await DealLifeLoss(null, player.Creature, enemy, 40m);
        }

        await TryGrantD5Anchor(player.Creature, combatState);

        if (_stage == 5 && combatState.RoundNumber == 5 && _d5VoidDrawnTotal >= 2 && !_d5VoidCollapseTriggered)
        {
            _d5VoidCollapseTriggered = true;
            await DealLifeLoss(null, player.Creature, enemy, 36m);
        }

        if (_stage == 6 && combatState.RoundNumber == 4 && _d6CalmExits >= 2 && !_d6CalmBreachTriggered)
        {
            _d6CalmBreachTriggered = true;
            await DealLifeLoss(null, player.Creature, enemy, 28m);
        }

        if (_stage == 7 && _d7NoxiousFumes)
        {
            await ApplyPoison(enemy, 3m, null, player.Creature);
        }

        if (_stage == 7)
        {
            await RefreshD7DynamicKeywordPowers(player.Creature, enemy);
        }

        if (_stage == 9)
        {
            await D9AddMantra(player, 1);
        }

        if (_stage == 9 && HasRelic("D9_Damaru"))
        {
            await D9AddMantra(player, 2);
        }

        if (_stage == 10)
        {
            if (_d10DelayedDamage > 0)
            {
                var damage = ClampByGate(enemy, _d10DelayedDamage, unblockable: false, allowedTurn: _d10AllowedKillTurn);
                _d10DelayedDamage = 0;
                await DealLifeLoss(null, player.Creature, enemy, damage, unblockable: false);
            }

            if (HasRelic("D10_WatchCore"))
            {
                _d10Charge++;
            }

            _d10Charge++;
            if (combatState.RoundNumber > 0 && combatState.RoundNumber % 2 == 0)
            {
                D10AddMark(1);
            }

            if (enemy.IsAlive)
            {
                await RefreshD10DynamicKeywordPowers(player.Creature, enemy);
            }
        }
    }

    private static Task OnEnemyTurnStart(Creature enemy)
    {
        return Task.CompletedTask;
    }

    public static async Task AfterSideTurnStartLate(CombatSide side, CombatState combatState)
    {
        var enemy = combatState.Enemies.FirstOrDefault(c => c.IsAlive);
        if (enemy == null || side != enemy.Side)
        {
            return;
        }

        if (_stage == 7)
        {
            await RestoreD7PoisonFunnelNaturalLoss(enemy);
            await D7Antidote(enemy);
            if (FindPlayer(combatState) is { } player)
            {
                await RefreshD7DynamicKeywordPowers(player.Creature, enemy);
            }
        }
    }

    private static async Task OnPlayerTurnEnd(PlayerChoiceContext choiceContext, Player player, Creature enemy)
    {
        if (_stage == 3)
        {
            if (_d3HadCunningFreeThisTurn)
            {
                _d3ConsecutiveCunningTurns++;
            }
            else
            {
                _d3ConsecutiveCunningTurns = 0;
            }

            await ResolveD3PlayerEndArmor(enemy);
        }

        if (_stage == 9)
        {
            _d9Divinity = false;
            await RefreshD9DynamicKeywordPowers(player.Creature);
        }

        if (_stage == 7 && HasRelic("D7_PoisonFunnel"))
        {
            _d7PoisonFunnelPoisonAtPlayerEnd = enemy.GetPowerAmount<PoisonPower>();
        }
        else if (_stage == 7)
        {
            _d7PoisonFunnelPoisonAtPlayerEnd = 0m;
        }

        if (_stage == 10 && enemy.CombatState?.RoundNumber == 3)
        {
            _d10Mark = Math.Max(0, _d10Mark - 3);
            await RefreshD10DynamicKeywordPowers(player.Creature, enemy);
        }
    }

    private static async Task OnEnemyTurnEnd(Creature enemy)
    {
        if (_stage == 4)
        {
            var d4Player = FindPlayer(enemy.CombatState);
            var burnCount = GetD4BurnCountForRound(enemy.CombatState?.RoundNumber ?? 0);
            if (d4Player != null && burnCount > 0)
            {
                await CardPileCmd.AddToCombatAndPreview<Burn>(d4Player.Creature, PileType.Discard, burnCount, creator: null);
                MarkD4StatusEntered(burnCount);
            }
        }

        if (_stage == 5)
        {
            var d5Player = FindPlayer(enemy.CombatState);
            var round = enemy.CombatState?.RoundNumber ?? 0;
            if (d5Player != null && round is 2 or 3 or 4)
            {
                await AddD5VoidToDiscardPile(d5Player);
            }
        }

        if (_stage == 10 && enemy.IsAlive && FindPlayer(enemy.CombatState) is { } d10Player)
        {
            await RefreshD10DynamicKeywordPowers(d10Player.Creature, enemy);
        }
    }

    private static async Task ResolveD3PlayerEndArmor(Creature enemy)
    {
        if (_stage != 3 || !enemy.IsAlive)
        {
            return;
        }

        if (!_d3HadCunningFreeThisTurn)
        {
            var round = enemy.CombatState?.RoundNumber ?? 0;
            var armor = round == 1 ? 16m : round == 2 ? 24m : round == 3 ? 10m : 0m;
            if (armor > 0m)
            {
                await GainBlock(enemy, armor);
            }
        }

        if (_d3ActiveCunningArmor && enemy.IsAlive)
        {
            await GainBlock(enemy, 10m);
        }

        if (_d3MisdiscardArmor && enemy.IsAlive)
        {
            await GainBlock(enemy, 8m);
        }
    }

    private static async Task D3DiscardByPriority(PlayerChoiceContext choiceContext, CardModel? sourceCard, Creature target, Player? explicitPlayer = null)
    {
        if (_d3DiscardedThisTurn)
        {
            // 缠手是“本回合不能再弃牌”，不是失败条件；药水或改牌再次尝试弃牌时直接锁住效果。
            return;
        }

        var player = explicitPlayer ?? sourceCard?.Owner;
        var hand = player?.PlayerCombatState?.Hand.Cards;
        if (player == null || hand == null)
        {
            return;
        }

        var card = D3PickDiscard(hand, sourceCard);
        if (card == null)
        {
            return;
        }

        await CardCmd.Discard(choiceContext, card);
        _d3DiscardedThisTurn = true;

        var id = Normalize(card.Id.Entry);
        if (IsFreeCunning(id))
        {
            if (!card.IsSlyThisTurn)
            {
                await D3TriggerCunning(choiceContext, card, player, target, id);
            }
        }
        else
        {
            _d3MisdiscardArmor = true;
        }

        if (HasRelic("D3_ReturnHolster"))
        {
            await PlayerCmd.GainEnergy(1m, player);
        }
        if (HasRelic("D3_HollowCharm"))
        {
            await GainBlock(player.Creature, 6m);
        }
    }

    private static CardModel? D3PickDiscard(IEnumerable<CardModel> hand, CardModel? sourceCard)
    {
        string[] priorities =
        [
            "d3backstabcunning", "d3shadowstep", "d3voidblade", "d3feint", "daggerthrow",
            "strikeironclad", "defendironcclad", "defendironclad", "neutralize", "bash", "d3finisher"
        ];
        var cards = hand.Where(card => sourceCard == null || !ReferenceEquals(card, sourceCard)).ToList();
        return cards
            .OrderBy(card =>
            {
                var id = Normalize(card.Id.Entry);
                var index = Array.IndexOf(priorities, id);
                return index < 0 ? priorities.Length : index;
            })
            .FirstOrDefault();
    }

    public static string GetD3DiscardTargetTitle(CardModel sourceCard)
    {
        var hand = sourceCard.Owner?.PlayerCombatState?.Hand.Cards;
        if (_stage != 3 || hand == null)
        {
            return "按优先级决定";
        }

        return D3PickDiscard(hand, sourceCard)?.Title ?? "无可弃牌";
    }

    public static string GetD3DiscardTargetLine(CardModel sourceCard)
    {
        var hand = sourceCard.Owner?.PlayerCombatState?.Hand.Cards;
        if (_stage != 3 || hand == null)
        {
            return "";
        }

        var target = D3PickDiscard(hand, sourceCard);
        return target == null ? "" : $"\n即将弃掉手牌：{target.Title}";
    }

    private static async Task D3TriggerCunning(PlayerChoiceContext choiceContext, CardModel? sourceCard, Player player, Creature target, string id)
    {
        if (_stage != 3)
        {
            return;
        }

        _d3HadCunningFreeThisTurn = true;
        _d3CunningFreeThisCombat++;
        var extra = HasRelic("D3_SharpDice") && !_d3SharpDiceUsedThisTurn && player.Creature.Block <= 0 ? 2m : 0m;
        _d3SharpDiceUsedThisTurn = true;
        if (id == "d3backstabcunning")
        {
            await DealAttack(choiceContext, sourceCard, player.Creature, target, 10m + extra);
        }
        else if (id == "d3shadowstep")
        {
            await DealAttack(choiceContext, sourceCard, player.Creature, target, 7m + extra);
            await GainBlock(player.Creature, 4m);
        }
    }

    private static async Task AddD4StatusToDrawPile<T>(PlayerChoiceContext choiceContext, Player player, int count)
        where T : CardModel
    {
        if (count <= 0 || player.Creature.CombatState == null)
        {
            return;
        }

        await CardPileCmd.AddToCombatAndPreview<T>(
            player.Creature,
            PileType.Draw,
            count,
            creator: player,
            position: CardPilePosition.Random);
        MarkD4StatusEntered(count);
    }

    private static void MarkD4StatusEntered(int count)
    {
        if (_stage != 4 || count <= 0)
        {
            return;
        }

        _d4StatusEnteredThisCombat = true;
        _d4Burns += count;
    }

    private static bool IsD4FireBreathingTrigger(CardModel card)
    {
        return card.Type is CardType.Status or CardType.Curse;
    }

    private static bool IsD5VoidCard(CardModel card)
    {
        return Normalize(card.Id.Entry) is "d5void" or "void";
    }

    private static async Task AddD5VoidToDiscardPile(Player player)
    {
        await CardPileCmd.AddToCombatAndPreview<GongdouD5Void>(
            player.Creature,
            PileType.Discard,
            1,
            creator: null);
    }

    private static Task ApplyD5PotionPhasePower(Creature enemy)
    {
        return _d5AllowedKillTurn switch
        {
            3 => PowerCmd.Apply<GongdouD5PotionPhaseTurn3KeywordPower>(enemy, 1m, enemy, null, silent: true),
            4 => PowerCmd.Apply<GongdouD5PotionPhaseTurn4KeywordPower>(enemy, 1m, enemy, null, silent: true),
            _ => PowerCmd.Apply<GongdouD5PotionPhaseTurn5KeywordPower>(enemy, 1m, enemy, null, silent: true)
        };
    }

    private static CardModel? FindD5VoidResource(Player player)
    {
        var combatState = player.PlayerCombatState;
        if (combatState == null)
        {
            return null;
        }

        return combatState.Hand.Cards.FirstOrDefault(IsD5VoidCard)
            ?? combatState.DiscardPile.Cards.FirstOrDefault(IsD5VoidCard);
    }

    private static async Task<bool> TryConsumeD5Void(PlayerChoiceContext choiceContext, Player player)
    {
        var voidCard = FindD5VoidResource(player);
        if (voidCard == null)
        {
            return false;
        }

        await CardCmd.Exhaust(choiceContext, voidCard);
        _d5VoidAvailable = Math.Max(0, _d5VoidAvailable - 1);
        return true;
    }

    private static async Task D6EnterStance(Player player, string stance, CardModel? sourceCard = null)
    {
        if (_d6Stance == stance)
        {
            return;
        }

        var wasCalm = _d6Stance == "calm";
        GD.Print($"[GongDou STS2] D6 stance change {_d6Stance} -> {stance}; source={sourceCard?.Id.Entry ?? "potion"}.");
        _d6Stance = stance;
        _d6ChangedStanceThisTurn = true;
        await RefreshD6StancePower(player.Creature);

        if (wasCalm)
        {
            _d6CalmExits++;
            await PlayerCmd.GainEnergy(HasRelic("D6_VioletLotus") ? 4m : 2m, player);
        }

        if (stance == "wrath" && HasRelic("D6_RageCharm") && !_d6RageCharmUsedThisTurn)
        {
            _d6RageCharmUsedThisTurn = true;
            await GainBlock(player.Creature, 7m);
        }

        if (HasRelic("D6_StanceSeal") && !_d6StanceSealUsedThisTurn)
        {
            _d6StanceSealUsedThisTurn = true;
            _d6StanceSealArmed = true;
            _d6StanceSealSourceCard = sourceCard;
        }
    }

    private static async Task RefreshD6StancePower(Creature player)
    {
        GD.Print($"[GongDou STS2] D6 refreshing stance power: {_d6Stance}.");

        var desiredAmount = _d6Stance switch
        {
            "calm" => 2,
            "wrath" => 3,
            _ => 1
        };

        if (player.GetPower<GongdouD6StanceKeywordPower>() is { } stancePower)
        {
            stancePower.SetAmount(desiredAmount, silent: true);
            return;
        }

        await PowerCmd.Apply<GongdouD6StanceKeywordPower>(player, desiredAmount, player, null, silent: true);
    }

    private static async Task RefreshD9DynamicKeywordPowers(Creature player)
    {
        await RemovePowerIfPresent<GongdouD9MirrorDrawKeywordPower>(player);
        await RemovePowerIfPresent<GongdouD9ExhaustKeywordPower>(player);

        if (_d9Divinity)
        {
            await RemovePowerIfPresent<GongdouD9MantraKeywordPower>(player);
            if (player.GetPower<GongdouD9DivinityKeywordPower>() == null)
            {
                await PowerCmd.Apply<GongdouD9DivinityKeywordPower>(player, 1m, player, null, silent: true);
            }
            return;
        }

        await RemovePowerIfPresent<GongdouD9DivinityKeywordPower>(player);
        if (_d9Mantra > 0)
        {
            if (player.GetPower<GongdouD9MantraKeywordPower>() is { } mantraPower)
            {
                var delta = _d9Mantra - mantraPower.Amount;
                if (delta > 0)
                {
                    await PowerCmd.Apply<GongdouD9MantraKeywordPower>(player, delta, player, null, silent: true);
                }
                else if (delta < 0)
                {
                    mantraPower.RemoveInternal();
                    await PowerCmd.Apply<GongdouD9MantraKeywordPower>(player, _d9Mantra, player, null, silent: true);
                }
            }
            else
            {
                await PowerCmd.Apply<GongdouD9MantraKeywordPower>(player, _d9Mantra, player, null, silent: true);
            }
        }
        else
        {
            await RemovePowerIfPresent<GongdouD9MantraKeywordPower>(player);
        }
    }

    private static async Task RefreshD10DynamicKeywordPowers(Creature player, Creature enemy)
    {
        var round = enemy.CombatState?.RoundNumber ?? _currentRound;

        await SyncKeywordPower<GongdouD10RiftDrawKeywordPower>(player, 4m, true);
        await SyncKeywordPower<GongdouD10ChargeKeywordPower>(player, _d10Charge, _d10Charge > 0);
        await SyncKeywordPower<GongdouD10EchoKeywordPower>(player, _d10Echo, _d10Echo > 0);
        await SyncKeywordPower<GongdouD10OverheatKeywordPower>(player, _d10Overheat, _d10Overheat > 0);
        await SyncKeywordPower<GongdouD10ResonatorKeywordPower>(player, 1m, HasRelic("D10_Resonator") && !_d10ResonatorUsed);
        await SyncKeywordPower<GongdouD10PrismShardKeywordPower>(player, 1m, HasRelic("D10_PrismShard"));
        await SyncKeywordPower<GongdouD10MarkResonanceKeywordPower>(player, 1m, HasRelic("D10_PrismShard") && _d10Mark >= 4);

        await SyncKeywordPower<GongdouD10PhaseGateKeywordPower>(enemy, _d10AllowedKillTurn, _d10AllowedKillTurn > 1 && round > 0 && round < _d10AllowedKillTurn);
        await SyncKeywordPower<GongdouD10MarkKeywordPower>(enemy, _d10Mark, _d10Mark > 0);
        await SyncKeywordPower<GongdouD10MarkDecayKeywordPower>(enemy, Math.Min(3, _d10Mark), round == 3 && _d10Mark > 0);
        await SyncKeywordPower<GongdouD10ArmorChargeKeywordPower>(enemy, 32m, round == 2);
        await SyncKeywordPower<GongdouD10EnemyArmorKeywordPower>(enemy, enemy.Block, enemy.Block > 0);
        await SyncKeywordPower<GongdouD10DelayedDamageKeywordPower>(enemy, _d10DelayedDamage, _d10DelayedDamage > 0);
    }

    private static Task RemovePowerIfPresent<T>(Creature owner) where T : PowerModel
    {
        if (owner.GetPower<T>() is { } power)
        {
            power.RemoveInternal();
        }

        return Task.CompletedTask;
    }

    private static async Task SyncKeywordPower<T>(Creature owner, decimal amount, bool visible) where T : PowerModel
    {
        if (!visible || amount <= 0 || !owner.IsAlive)
        {
            await RemovePowerIfPresent<T>(owner);
            return;
        }

        if (owner.GetPower<T>() is { } power)
        {
            var delta = amount - power.Amount;
            if (delta > 0)
            {
                await PowerCmd.Apply<T>(owner, delta, owner, null, silent: true);
            }
            else if (delta < 0)
            {
                power.RemoveInternal();
                await PowerCmd.Apply<T>(owner, amount, owner, null, silent: true);
            }
        }
        else
        {
            await PowerCmd.Apply<T>(owner, amount, owner, null, silent: true);
        }
    }

    private static async Task ApplyPoison(Creature target, decimal amount, CardModel? sourceCard, Creature? sourceCreature = null)
    {
        var adjusted = amount + (HasRelic("D7_SnakeSkull") ? 1m : 0m);
        await PowerCmd.Apply<PoisonPower>(target, adjusted, sourceCreature ?? sourceCard?.Owner.Creature, sourceCard);
    }

    private static async Task ApplyVulnerable(Creature target, decimal amount, Creature source, CardModel? sourceCard)
    {
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<VulnerablePower>(target, amount, source, sourceCard);
    }

    private static async Task ApplyD5BarrierVulnerable(Creature target, decimal amount, Creature source)
    {
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<VulnerablePower>(target, amount, source, null);
    }

    private static async Task ApplyWeak(Creature target, decimal amount, Creature source, CardModel? sourceCard)
    {
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<WeakPower>(target, amount, source, sourceCard);
    }

    private static async Task RemoveD5Artifact(Creature enemy, decimal amount)
    {
        var artifact = enemy.GetPower<ArtifactPower>();
        if (artifact == null || artifact.Amount <= 0 || amount <= 0)
        {
            return;
        }

        var remaining = Math.Max(0m, artifact.Amount - amount);
        await PowerCmd.Remove(artifact);
        if (remaining > 0)
        {
            await PowerCmd.Apply<ArtifactPower>(enemy, remaining, enemy, null, silent: true);
        }
    }

    private static bool HasArtifact(Creature target)
    {
        return (target.GetPower<ArtifactPower>()?.Amount ?? 0m) > 0m;
    }

    private static async Task TryGrantD5Anchor(Creature player, ICombatState? combatState)
    {
        if (_stage != 5 || _d5AnchorGranted || !HasRelic("D5_Anchor"))
        {
            return;
        }

        if ((combatState?.RoundNumber ?? 1) > 1)
        {
            return;
        }

        _d5AnchorGranted = true;
        var missingBlock = Math.Max(0m, 10m - player.Block);
        if (missingBlock > 0m)
        {
            await GainBlock(player, missingBlock);
            GD.Print($"[GongDou STS2] D5 anchor topped up {missingBlock} block at round {combatState?.RoundNumber ?? -1}.");
        }
    }

    private static async Task D7Catalyst(Creature target, CardModel? sourceCard, Creature? sourceCreature = null)
    {
        var amount = target.GetPowerAmount<PoisonPower>();
        if (amount > 0)
        {
            await PowerCmd.Apply<PoisonPower>(target, amount, sourceCreature ?? sourceCard?.Owner.Creature, sourceCard);
        }
    }

    private static async Task D7Antidote(Creature enemy)
    {
        var power = enemy.GetPower<PoisonPower>();
        if (power == null || power.Amount <= 0)
        {
            return;
        }

        var round = enemy.CombatState?.RoundNumber ?? 0;
        var baseRemove = round == 2 ? 6 : round == 4 ? 9 : 0;
        if (baseRemove <= 0)
        {
            return;
        }

        var removed = Math.Min(power.Amount, GetD7AntidoteRemoveAmountForRound(round));
        if (removed <= 0)
        {
            return;
        }

        await PowerCmd.ModifyAmount(power, -removed, FindPlayer(enemy.CombatState)?.Creature, null, silent: true);
    }

    private static async Task RestoreD7PoisonFunnelNaturalLoss(Creature enemy)
    {
        if (!HasRelic("D7_PoisonFunnel") || _d7PoisonFunnelPoisonAtPlayerEnd <= 0 || !enemy.IsAlive)
        {
            _d7PoisonFunnelPoisonAtPlayerEnd = 0m;
            return;
        }

        var currentPoison = enemy.GetPowerAmount<PoisonPower>();
        var restoreAmount = Math.Max(0m, _d7PoisonFunnelPoisonAtPlayerEnd - currentPoison);
        _d7PoisonFunnelPoisonAtPlayerEnd = 0m;
        if (restoreAmount > 0)
        {
            await PowerCmd.Apply<PoisonPower>(enemy, restoreAmount, FindPlayer(enemy.CombatState)?.Creature, null, silent: true);
        }
    }

    private static async Task RefreshD7DynamicKeywordPowers(Creature player, Creature enemy)
    {
        if (enemy.GetPower<GongdouD7AntidoteKeywordPower>() is { } antidote)
        {
            antidote.RemoveInternal();
        }

        if (_stage != 7)
        {
            return;
        }

        var round = enemy.CombatState?.RoundNumber ?? _currentRound;
        if (round is 2 or 4)
        {
            var removeAmount = GetD7AntidoteRemoveAmountForRound(round);
            if (removeAmount > 0)
            {
                await PowerCmd.Apply<GongdouD7AntidoteKeywordPower>(enemy, removeAmount, enemy, null, silent: true);
            }
        }

        if (_d7NoxiousFumes && player.GetPower<GongdouD7PoisonFogKeywordPower>() == null)
        {
            await PowerCmd.Apply<GongdouD7PoisonFogKeywordPower>(player, 1m, player, null, silent: true);
        }

        if (_d7Caltrops && player.GetPower<GongdouD7CaltropsKeywordPower>() == null)
        {
            await PowerCmd.Apply<GongdouD7CaltropsKeywordPower>(player, 1m, player, null, silent: true);
        }
    }

    private static decimal GetD7AntidoteRemoveAmountForRound(int round)
    {
        var baseRemove = round == 2 ? 6m : round == 4 ? 9m : 0m;
        return Math.Max(0m, baseRemove - (HasRelic("D7_PoisonFunnel") ? 4m : 0m));
    }

    private static async Task D8GenerateOrb(PlayerChoiceContext? context, CardModel? card, Creature target, string type, decimal value, Creature? source = null)
    {
        if (_d8Orbs.Count >= 3)
        {
            await D8EvokeLeft(context, card, target, source);
        }
        _d8Orbs.Add(new Orb(type, value));
    }

    private static async Task D8EvokeLeft(PlayerChoiceContext? context, CardModel? card, Creature target, Creature? source = null)
    {
        if (_d8Orbs.Count == 0)
        {
            return;
        }
        var orb = _d8Orbs[0];
        _d8Orbs.RemoveAt(0);
        await D8EvokeOrb(context, card, source ?? card?.Owner.Creature, target, orb);
    }

    private static async Task D8Recursion(PlayerChoiceContext context, CardModel card, Creature target)
    {
        if (_d8Orbs.Count == 0)
        {
            return;
        }
        var orb = _d8Orbs[0];
        await D8EvokeLeft(context, card, target);
        await D8GenerateOrb(context, card, target, orb.Type, orb.Value);
    }

    private static async Task D8EvokeOrb(PlayerChoiceContext? context, CardModel? card, Creature? source, Creature target, Orb orb)
    {
        switch (orb.Type)
        {
            case "lightning":
                await DealLifeLoss(context, source, target, 8m + _d8Focus, unblockable: false);
                break;
            case "frost":
                var block = 7m + _d8Focus;
                var player = source?.Player ?? FindPlayer(target.CombatState);
                if (player != null)
                {
                    await GainBlock(player.Creature, block);
                    await DealLifeLoss(context, player.Creature, target, block);
                }
                break;
            case "dark":
                await DealLifeLoss(context, source, target, orb.Value, unblockable: false);
                break;
        }
    }

    private static async Task D8EndTurnPassives(PlayerChoiceContext context, Creature source, Creature target)
    {
        foreach (var orb in _d8Orbs.ToList())
        {
            await D8Passive(context, source, target, orb);
        }

        var extra = _d8Loop + (HasRelic("D8_GoldCable") ? 1 : 0);
        for (var index = 0; index < extra && _d8Orbs.Count > 0; index++)
        {
            await D8Passive(context, source, target, _d8Orbs[0]);
        }
    }

    private static async Task D8Passive(PlayerChoiceContext? context, Creature source, Creature target, Orb orb)
    {
        var index = _d8Orbs.IndexOf(orb);
        switch (orb.Type)
        {
            case "lightning":
                await DealLifeLoss(context, source, target, 3m + _d8Focus, unblockable: false);
                break;
            case "frost":
                var block = 2m + _d8Focus;
                await GainBlock(source, block);
                await DealLifeLoss(context, source, target, block);
                break;
            case "dark":
                if (index >= 0)
                {
                    _d8Orbs[index] = orb with { Value = orb.Value + 6m + _d8Focus + (HasRelic("D8_DarkCore") ? 2m : 0m) };
                }
                break;
        }
    }

    private static async Task EnsureD8OrbSlots(Player player)
    {
        if (_stage != 8)
        {
            return;
        }

        var queue = player.PlayerCombatState?.OrbQueue;
        if (queue == null)
        {
            return;
        }

        var missing = 3 - queue.Capacity;
        if (missing > 0)
        {
            await OrbCmd.AddSlots(player, missing);
        }
    }

    private static async Task D8ChannelOrb<T>(PlayerChoiceContext context, Player player)
        where T : OrbModel
    {
        await EnsureD8OrbSlots(player);
        await OrbCmd.Channel<T>(context, player);
    }

    private static async Task D8ChannelDarkOrb(PlayerChoiceContext context, Player player, decimal? initialValue)
    {
        await EnsureD8OrbSlots(player);
        var orb = (DarkOrb)ModelDb.Orb<DarkOrb>().ToMutable();
        if (initialValue.HasValue)
        {
            SetD8DarkOrbValue(orb, initialValue.Value);
        }

        await OrbCmd.Channel(context, orb, player);
    }

    private static async Task D8EvokeLeft(PlayerChoiceContext context, Player player)
    {
        await EnsureD8OrbSlots(player);
        await OrbCmd.EvokeNext(context, player);
    }

    private static async Task D8Recursion(PlayerChoiceContext context, Player player)
    {
        await EnsureD8OrbSlots(player);
        var current = player.PlayerCombatState?.OrbQueue.Orbs.FirstOrDefault();
        if (current == null)
        {
            return;
        }

        var replacement = CreateD8OrbReplacement(current);
        await OrbCmd.EvokeNext(context, player);
        await OrbCmd.Channel(context, replacement, player);
    }

    private static OrbModel CreateD8OrbReplacement(OrbModel source)
    {
        var replacement = ModelDb.GetById<OrbModel>(source.Id).ToMutable();
        if (source is DarkOrb darkSource && replacement is DarkOrb darkReplacement)
        {
            SetD8DarkOrbValue(darkReplacement, darkSource.EvokeVal);
        }

        return replacement;
    }

    private static void SetD8DarkOrbValue(DarkOrb orb, decimal value)
    {
        DarkOrbEvokeValueField?.SetValue(orb, value);
    }

    public static decimal ModifyD8OrbValue(Player player, decimal value)
    {
        return _stage == 8 ? value + _d8Focus : value;
    }

    public static int ModifyD8OrbPassiveTriggerCounts(OrbModel orb, int triggerCount)
    {
        if (_stage != 8 || orb.Owner.PlayerCombatState?.OrbQueue.Orbs.FirstOrDefault() != orb)
        {
            return triggerCount;
        }

        return triggerCount + _d8Loop + (HasRelic("D8_GoldCable") ? 1 : 0);
    }

    public static async Task D8FrostPassive(PlayerChoiceContext choiceContext, FrostOrb orb, Creature? target)
    {
        if (target != null)
        {
            throw new InvalidOperationException("Frost orbs cannot target creatures.");
        }

        orb.Trigger();
        await GainBlock(orb.Owner.Creature, orb.PassiveVal);
        await D8TriggerInsulation(choiceContext, orb.Owner.Creature, orb.PassiveVal);
    }

    public static async Task<IEnumerable<Creature>> D8FrostEvoke(PlayerChoiceContext choiceContext, FrostOrb orb)
    {
        await GainBlock(orb.Owner.Creature, orb.EvokeVal);
        await D8TriggerInsulation(choiceContext, orb.Owner.Creature, orb.EvokeVal);
        return [orb.Owner.Creature];
    }

    public static Task D8DarkPassive(PlayerChoiceContext choiceContext, DarkOrb orb, Creature? target)
    {
        if (target != null)
        {
            throw new InvalidOperationException("Dark orbs cannot target creatures.");
        }

        orb.Trigger();
        SetD8DarkOrbValue(orb, orb.EvokeVal + orb.PassiveVal + (HasRelic("D8_DarkCore") ? 2m : 0m));
        NCombatRoom.Instance?.GetCreatureNode(orb.Owner.Creature)?.OrbManager?.UpdateVisuals(OrbEvokeType.None);
        return Task.CompletedTask;
    }

    private static async Task D8TriggerInsulation(PlayerChoiceContext choiceContext, Creature player, decimal amount)
    {
        var enemy = ResolveEnemyTarget(player.CombatState);
        if (enemy.IsAlive)
        {
            await DealLifeLoss(choiceContext, player, enemy, amount);
        }
    }

    private static async Task D9AddMantra(Player player, int amount)
    {
        _d9Mantra += amount + (HasRelic("D9_Scripture") ? 1 : 0);
        if (_d9Mantra >= 10)
        {
            _d9Mantra -= 10;
            D9EnterDivinity();
        }
        await RefreshD9DynamicKeywordPowers(player.Creature);
    }

    private static void D9EnterDivinity()
    {
        _d9Divinity = true;
        if (HasRelic("D9_SundialShard") && !_d9SundialConsumed)
        {
            _d9SundialConsumed = true;
            _d9SundialArmed = true;
        }
    }

    private static void D10AddMark(int amount)
    {
        _d10Mark += amount + (HasRelic("D10_PrismShard") ? 1 : 0);
    }

    private static async Task DealAttack(PlayerChoiceContext context, CardModel card, Creature target, decimal amount, int hits = 1)
    {
        await DealAttack(context, card, card.Owner.Creature, target, amount, hits);
    }

    private static async Task DealAttack(PlayerChoiceContext? context, CardModel? card, Creature source, Creature target, decimal amount, int hits = 1)
    {
        if (amount <= 0 || target.IsDead)
        {
            return;
        }

        var damage = ModifyPlayerAttackDamage(source, target, amount);
        damage = ClampByGate(target, damage * Math.Max(1, hits), unblockable: false) / Math.Max(1, hits);
        if (damage <= 0)
        {
            return;
        }

        if (card != null)
        {
            await DamageCmd.Attack(damage)
                .WithHitCount(Math.Max(1, hits))
                .FromCard(card)
                .Targeting(target)
                .Execute(context);
            await AfterPlayerAttackDamage(source, Math.Max(1, hits));
        }
        else
        {
            await DealLifeLoss(context, source, target, damage * Math.Max(1, hits), unblockable: false);
        }
    }

    private static async Task DealUnblockedAttack(PlayerChoiceContext context, CardModel card, Creature target, decimal amount)
    {
        if (amount <= 0 || target.IsDead)
        {
            return;
        }

        var savedBlock = target.Block;
        if (savedBlock > 0)
        {
            target.LoseBlockInternal(savedBlock);
        }

        try
        {
            await DealAttack(context, card, target, amount);
        }
        finally
        {
            if (savedBlock > 0 && target.IsAlive)
            {
                target.GainBlockInternal(savedBlock);
            }
        }
    }

    private static decimal ModifyPlayerAttackDamage(Creature source, Creature target, decimal amount)
    {
        var damage = amount;
        if (_stage == 7 && target.GetPowerAmount<PoisonPower>() > 0 && HasRelic("D7_RingOfNeedles") && !_d7RingUsedThisTurn)
        {
            damage += 6m;
            _d7RingUsedThisTurn = true;
        }

        if (_stage == 9)
        {
            if (!_d9Divinity && amount >= 12m)
            {
                target.GainBlockInternal(10m);
            }
            if (_d9Divinity)
            {
                damage *= 3m;
            }
            if (_d9SundialArmed)
            {
                damage += 15m;
                _d9SundialArmed = false;
            }
        }

        if (_stage == 10)
        {
            if (_d10Echo > 0)
            {
                damage *= 2m;
                _d10Echo--;
            }
            if (HasRelic("D10_PrismShard") && _d10Mark >= 4)
            {
                damage += 4m;
            }
        }

        return damage;
    }

    private static async Task AfterPlayerAttackDamage(Creature source, int hits)
    {
        if (_stage != 5 || !HasRelic("D5_Shuriken") || hits <= 0)
        {
            return;
        }

        _d5AttackDamageEventsThisTurn += hits;
        while (_d5AttackDamageEventsThisTurn >= 3)
        {
            _d5AttackDamageEventsThisTurn -= 3;
            await PowerCmd.Apply<StrengthPower>(source, 1m, source, null);
        }
    }

    private static async Task DealLifeLoss(PlayerChoiceContext? context, Creature? source, Creature target, decimal amount, bool unblockable = true)
    {
        if (amount <= 0 || target.IsDead)
        {
            return;
        }

        await CreatureCmd.Damage(
            context ?? new BlockingPlayerChoiceContext(),
            target,
            amount,
            unblockable
                ? ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move
                : ValueProp.Unpowered | ValueProp.Move,
            source ?? target);
    }

    private static decimal ClampByGate(Creature target, decimal amount, bool unblockable, int? allowedTurn = null)
    {
        var gateTurn = allowedTurn ?? (_stage == 5 ? _d5AllowedKillTurn : _stage == 10 ? _d10AllowedKillTurn : 0);
        if (gateTurn <= 0)
        {
            return amount;
        }

        var round = target.CombatState?.RoundNumber ?? 1;
        var protectedHp = round < gateTurn ? 1 : 0;
        if (protectedHp <= 0)
        {
            return amount;
        }

        var absorb = unblockable ? 0m : target.Block;
        var lethalBeforeGate = amount > absorb + target.CurrentHp - protectedHp;
        if (lethalBeforeGate && target.Block > 0)
        {
            target.LoseBlockInternal(target.Block);
            absorb = 0m;
        }

        var maxAmount = Math.Max(0m, absorb + target.CurrentHp - protectedHp);
        return Math.Min(amount, maxAmount);
    }

    private static async Task GainBlock(Creature creature, decimal amount)
    {
        if (amount > 0 && creature.IsAlive)
        {
            await CreatureCmd.GainBlock(creature, amount, ValueProp.Unpowered, null);
        }
    }

    public static decimal GetD3NoCunningArmorDisplayAmount()
    {
        return _currentRound switch
        {
            1 => 16m,
            2 => 24m,
            3 => 10m,
            >= 4 => 0m,
            _ => 16m
        };
    }

    public static decimal GetD4BurnShuffleDisplayAmount()
    {
        return GetD4BurnCountForRound(_currentRound > 0 ? _currentRound : 1);
    }

    public static decimal GetD7AntidoteDisplayAmount()
    {
        return GetD7AntidoteRemoveAmountForRound(_currentRound > 0 ? _currentRound : 1);
    }

    public static decimal GetD7AntidoteDisplayBlock()
    {
        var round = _currentRound > 0 ? _currentRound : 1;
        return round == 2 ? 14m : round == 4 ? 22m : 0m;
    }

    private static int GetD4BurnCountForRound(int round)
    {
        return round switch
        {
            1 => 1,
            2 => 1,
            3 => 2,
            _ => 0
        };
    }

    private static void ResetCombatState()
    {
        _combatStarted = false;
        _currentRound = -1;
        _playerCardsThisRound = 0;
        _d3DiscardedThisTurn = false;
        _d3MisdiscardArmor = false;
        _d3ActiveCunningArmor = false;
        _d3HadCunningFreeThisTurn = false;
        _d3SharpDiceUsedThisTurn = false;
        _d3CunningFreeThisCombat = 0;
        _d3ConsecutiveCunningTurns = 0;
        _d4Burns = 0;
        _d4GhostActive = false;
        _d4StatusEnteredThisCombat = false;
        _d4FireBreathing = 0;
        _d4Evolve = 0;
        _d5VoidAvailable = 0;
        _d5VoidDrawnTotal = 0;
        _d5AllowedKillTurn = 5;
        _d5AttackDamageEventsThisTurn = 0;
        _d5VoidCollapseTriggered = false;
        _d5VoidLensUsedThisTurn = false;
        _d5AnchorGranted = false;
        _d6Stance = "normal";
        _d6ChangedStanceThisTurn = false;
        _d6RageCharmUsedThisTurn = false;
        _d6StanceSealArmed = false;
        _d6StanceSealUsedThisTurn = false;
        _d6StanceSealSourceCard = null;
        _d6CalmExits = 0;
        _d6CalmBreachTriggered = false;
        _d7NoxiousFumes = false;
        _d7Caltrops = false;
        _d7RingUsedThisTurn = false;
        _d7GhostActive = false;
        _d7PoisonFunnelPoisonAtPlayerEnd = 0m;
        _d8Focus = 0;
        _d8Loop = 0;
        _d8Orbs = [];
        _d9Mantra = 0;
        _d9Divinity = false;
        _d9SundialArmed = false;
        _d9SundialConsumed = false;
        _d10Charge = 0;
        _d10Mark = 0;
        _d10Echo = 0;
        _d10Overheat = 0;
        _d10DelayedDamage = 0;
        _d10AllowedKillTurn = 4;
        _d10ResonatorUsed = false;
    }

    private static void ResetPerTurn(int round)
    {
        _currentRound = round;
        _playerCardsThisRound = 0;
        _d3DiscardedThisTurn = false;
        _d3MisdiscardArmor = false;
        _d3ActiveCunningArmor = false;
        _d3HadCunningFreeThisTurn = false;
        _d3SharpDiceUsedThisTurn = false;
        _d5AttackDamageEventsThisTurn = 0;
        _d5VoidLensUsedThisTurn = false;
        _d6ChangedStanceThisTurn = false;
        _d6RageCharmUsedThisTurn = false;
        _d6StanceSealArmed = false;
        _d6StanceSealUsedThisTurn = false;
        _d6StanceSealSourceCard = null;
        _d7RingUsedThisTurn = false;
    }

    private static Creature ResolveEnemyTarget(CardModel card, Creature? explicitTarget)
    {
        return explicitTarget ?? ResolveEnemyTarget(card.Owner.Creature.CombatState);
    }

    private static Creature ResolveEnemyTarget(ICombatState? combatState)
    {
        var target = combatState?.HittableEnemies.FirstOrDefault() ?? combatState?.Enemies.FirstOrDefault(c => c.IsAlive);
        return target ?? throw new InvalidOperationException("No enemy target is available for GongDou puzzle effect.");
    }

    private static Player? FindPlayer(ICombatState? combatState)
    {
        return combatState?.Players.FirstOrDefault();
    }

    private static bool HasActiveCardLimit()
    {
        return _stage == 3 || _stage == 5;
    }

    private static bool HasRelic(string id) => _relics.Contains(Normalize(id));
    private static bool HasPotion(string id) => _potions.Contains(Normalize(id));

    private static bool IsFreeCunning(string normalizedId)
    {
        return normalizedId is "d3backstabcunning" or "d3shadowstep";
    }

    private static string Normalize(string id)
    {
        return id.Replace("Gongdou", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }
}
