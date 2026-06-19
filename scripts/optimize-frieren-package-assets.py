import argparse
import json
from io import BytesIO
from pathlib import Path

from PIL import Image


EXPECTED_CATEGORY_COUNTS = {
    91: "cards",
    10: "relics",
    5: "potions",
}


def png_files(path: Path):
    return sorted(path.rglob("*.png"), key=lambda item: str(item).lower())


def optimize_png(source: Path, max_dimension: int, colors: int) -> bytes:
    with Image.open(source) as image:
        if image.mode not in ("RGB", "RGBA"):
            image = image.convert("RGBA" if "A" in image.getbands() else "RGB")
        else:
            image = image.copy()

        if max(image.size) > max_dimension:
            image.thumbnail((max_dimension, max_dimension), Image.Resampling.LANCZOS)

        if image.mode == "RGBA":
            optimized = image.quantize(
                colors=colors,
                method=Image.Quantize.FASTOCTREE,
                dither=Image.Dither.FLOYDSTEINBERG,
            )
        else:
            optimized = image.quantize(
                colors=colors,
                method=Image.Quantize.MEDIANCUT,
                dither=Image.Dither.FLOYDSTEINBERG,
            )

        buffer = BytesIO()
        optimized.save(buffer, format="PNG", optimize=True)
        return buffer.getvalue()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", required=True)
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--max-dimension", type=int, default=512)
    parser.add_argument("--colors", type=int, default=256)
    args = parser.parse_args()

    source_root = Path(args.source_root)
    output_root = Path(args.output_root)
    if not source_root.exists():
        raise FileNotFoundError(f"Frieren art root missing: {source_root}")

    output_root.mkdir(parents=True, exist_ok=True)
    stats = {
        "sourceRoot": str(source_root),
        "outputRoot": str(output_root),
        "maxDimension": args.max_dimension,
        "colors": args.colors,
        "originalBytes": 0,
        "optimizedBytes": 0,
        "files": 0,
        "categories": {},
    }

    source_dirs = [item for item in source_root.iterdir() if item.is_dir()]
    matched_counts = set()
    for source_dir in source_dirs:
        files = png_files(source_dir)
        target_category = EXPECTED_CATEGORY_COUNTS.get(len(files))
        if target_category is None:
            continue
        if len(files) in matched_counts:
            raise RuntimeError(f"Duplicate Frieren asset category count: {len(files)}")
        matched_counts.add(len(files))

        category_original = 0
        category_optimized = 0
        for source_file in files:
            relative_path = source_file.relative_to(source_dir)
            output_file = output_root / target_category / relative_path
            output_file.parent.mkdir(parents=True, exist_ok=True)

            original_bytes = source_file.read_bytes()
            optimized_bytes = optimize_png(source_file, args.max_dimension, args.colors)
            if len(optimized_bytes) >= len(original_bytes):
                optimized_bytes = original_bytes

            output_file.write_bytes(optimized_bytes)
            category_original += len(original_bytes)
            category_optimized += len(optimized_bytes)
            stats["files"] += 1

        stats["originalBytes"] += category_original
        stats["optimizedBytes"] += category_optimized
        stats["categories"][target_category] = {
            "files": len(files),
            "originalBytes": category_original,
            "optimizedBytes": category_optimized,
        }

    missing = [
        category
        for count, category in EXPECTED_CATEGORY_COUNTS.items()
        if count not in matched_counts
    ]
    if missing:
        raise RuntimeError(f"Missing Frieren asset categories: {', '.join(missing)}")

    print(json.dumps(stats, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
