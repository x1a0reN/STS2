#!/usr/bin/env python3
"""
Static audit for the GongDou STS2 Frieren character mod.

The check intentionally reads current source files and the local art pack instead
of trusting hand-written counts. It fails when any Frieren card/relic/potion art
key is missing, unused, duplicated, or when the core model counts drift.
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


def fail(message: str) -> None:
    raise SystemExit(message)


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def find_art_root(repo_root: Path) -> Path:
    docs_dir = repo_root / "docs"
    candidates = [
        path
        for path in docs_dir.iterdir()
        if path.is_dir() and "800" in path.name and "20260527" in path.name
    ]
    if len(candidates) != 1:
        fail(f"Expected exactly one Frieren 800 art pack, found {len(candidates)}.")
    return candidates[0]


def classify_art_dirs(art_root: Path) -> tuple[Path, Path, Path]:
    counts = {
        child: len(list(child.rglob("*.png")))
        for child in art_root.iterdir()
        if child.is_dir()
    }

    def pick(expected_count: int, label: str) -> Path:
        matches = [path for path, count in counts.items() if count == expected_count]
        if len(matches) != 1:
            fail(f"Expected one {label} dir with {expected_count} png files, found {len(matches)}.")
        return matches[0]

    return pick(91, "card"), pick(10, "relic"), pick(5, "potion")


def extract_string_arrows(source: str, property_name: str) -> list[str]:
    return re.findall(rf"{re.escape(property_name)}\s*=>\s*\"([^\"]+)\"", source)


def extract_array_types(source: str, array_name: str) -> list[str]:
    match = re.search(
        rf"{re.escape(array_name)}\s*=\s*\[(.*?)\];",
        source,
        flags=re.DOTALL,
    )
    if not match:
        fail(f"Could not find array: {array_name}")
    return re.findall(r"typeof\(([A-Za-z0-9_]+)\)", match.group(1))


def png_keys(root: Path) -> set[str]:
    return {
        str(path.relative_to(root)).replace("\\", "/").removesuffix(".png")
        for path in root.rglob("*.png")
    }


def audit(repo_root: Path) -> dict[str, object]:
    mod_root = repo_root / "src" / "GongdouSts2FrierenMod"
    art_root = find_art_root(repo_root)
    card_root, relic_root, potion_root = classify_art_dirs(art_root)

    card_sources = [
        mod_root / "Cards" / "FrierenCards.cs",
        mod_root / "Cards" / "FrierenExpandedCards.cs",
    ]
    card_keys: list[str] = []
    for source_path in card_sources:
        source = read_text(source_path)
        for property_name in ("PortraitKey", "ArtKey"):
            for key in extract_string_arrows(source, property_name):
                if "/" in key or "\\" in key:
                    card_keys.append(key)

    relic_source = read_text(mod_root / "Relics" / "FrierenRelics.cs")
    potion_source = read_text(mod_root / "Potions" / "FrierenPotions.cs")
    expanded_source = read_text(mod_root / "Cards" / "FrierenExpandedCards.cs")
    character_source = read_text(mod_root / "Characters" / "FrierenCharacter.cs")
    manifest = json.loads(read_text(mod_root / "mod_manifest.json"))
    initializer_source = read_text(mod_root / "GongdouSts2FrierenMod.cs")

    relic_keys = extract_string_arrows(relic_source, "FrierenIconKey")
    potion_keys = extract_string_arrows(potion_source, "FrierenImageKey")

    expected_card_keys = png_keys(card_root)
    expected_relic_keys = png_keys(relic_root)
    expected_potion_keys = png_keys(potion_root)

    card_key_set = set(card_keys)
    relic_key_set = set(relic_keys)
    potion_key_set = set(potion_keys)

    standard_reward = extract_array_types(expanded_source, "StandardRewardCardTypes")
    ancient = extract_array_types(expanded_source, "AncientCardTypes")
    all_cards = extract_array_types(expanded_source, "AllCardTypes")
    standard_relics = extract_array_types(relic_source, "StandardRelicTypes")
    boss_relics = extract_array_types(relic_source, "BossReplacementRelicTypes")
    all_potions = extract_array_types(potion_source, "AllPotionTypes")

    starting_deck_match = re.search(
        r"StartingDeck\s*=>\s*\[(.*?)\];",
        character_source,
        flags=re.DOTALL,
    )
    if not starting_deck_match:
        fail("Could not find Frieren starting deck.")
    starting_deck = re.findall(r"ModelDb\.Card<([A-Za-z0-9_]+)>\(\)", starting_deck_match.group(1))
    starting_relics = re.findall(r"ModelDb\.Relic<([A-Za-z0-9_]+)>\(\)", character_source)

    version_match = re.search(r'Version\s*=\s*"([^"]+)"', initializer_source)
    if not version_match:
        fail("Could not find Frieren initializer version.")

    result = {
        "artRoot": str(art_root),
        "cardPngCount": len(expected_card_keys),
        "relicPngCount": len(expected_relic_keys),
        "potionPngCount": len(expected_potion_keys),
        "cardKeyCount": len(card_keys),
        "uniqueCardKeyCount": len(card_key_set),
        "relicKeyCount": len(relic_keys),
        "uniqueRelicKeyCount": len(relic_key_set),
        "potionKeyCount": len(potion_keys),
        "uniquePotionKeyCount": len(potion_key_set),
        "missingCardArt": sorted(card_key_set - expected_card_keys),
        "unusedCardArt": sorted(expected_card_keys - card_key_set),
        "missingRelicArt": sorted(relic_key_set - expected_relic_keys),
        "unusedRelicArt": sorted(expected_relic_keys - relic_key_set),
        "missingPotionArt": sorted(potion_key_set - expected_potion_keys),
        "unusedPotionArt": sorted(expected_potion_keys - potion_key_set),
        "standardRewardCardTypeCount": len(standard_reward),
        "ancientCardTypeCount": len(ancient),
        "allCardTypeCount": len(all_cards),
        "standardRelicTypeCount": len(standard_relics),
        "bossReplacementRelicTypeCount": len(boss_relics),
        "allPotionTypeCount": len(all_potions),
        "startingDeck": starting_deck,
        "startingDeckCount": len(starting_deck),
        "startingRelics": starting_relics,
        "manifestVersion": manifest["version"],
        "initializerVersion": version_match.group(1),
    }

    expectations = {
        "cardPngCount": 91,
        "relicPngCount": 10,
        "potionPngCount": 5,
        "cardKeyCount": 91,
        "uniqueCardKeyCount": 91,
        "relicKeyCount": 10,
        "uniqueRelicKeyCount": 10,
        "potionKeyCount": 5,
        "uniquePotionKeyCount": 5,
        "standardRewardCardTypeCount": 80,
        "ancientCardTypeCount": 2,
        "allCardTypeCount": 91,
        "standardRelicTypeCount": 9,
        "bossReplacementRelicTypeCount": 1,
        "allPotionTypeCount": 5,
        "startingDeckCount": 10,
    }
    for key, expected in expectations.items():
        actual = result[key]
        if actual != expected:
            fail(f"{key} mismatch: expected {expected}, got {actual}")

    expected_deck = [
        "FrierenStrike",
        "FrierenStrike",
        "FrierenStrike",
        "FrierenStrike",
        "FrierenDefend",
        "FrierenDefend",
        "FrierenDefend",
        "FrierenDefend",
        "BasicKillingMagic",
        "ManaSuppression",
    ]
    if starting_deck != expected_deck:
        fail(f"Starting deck mismatch: {starting_deck}")
    if starting_relics != ["BlueMoonGrassBookmark"]:
        fail(f"Starting relic mismatch: {starting_relics}")
    if result["manifestVersion"] != result["initializerVersion"]:
        fail("Frieren version mismatch between manifest and initializer.")
    for key in (
        "missingCardArt",
        "unusedCardArt",
        "missingRelicArt",
        "unusedRelicArt",
        "missingPotionArt",
        "unusedPotionArt",
    ):
        if result[key]:
            fail(f"{key} is not empty.")

    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--repo-root",
        default=str(Path(__file__).resolve().parents[1]),
        help="Repository root.",
    )
    parser.add_argument("--json", default="", help="Optional output JSON path.")
    args = parser.parse_args()

    result = audit(Path(args.repo_root))
    text = json.dumps(result, ensure_ascii=False, indent=2)
    if args.json:
        Path(args.json).write_text(text + "\n", encoding="utf-8")
    print(text)


if __name__ == "__main__":
    main()
