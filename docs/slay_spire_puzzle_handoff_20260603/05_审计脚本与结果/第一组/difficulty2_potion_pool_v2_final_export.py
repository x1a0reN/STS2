import json

from difficulty2_potion_pool_v2_audit import run


summary = run(72, 15, (6, 20, 26, 30), (14, 0, 0, 0), (0, 0, 0, 0), True)
with open("difficulty2_potion_pool_v2_final_audit.json", "w", encoding="utf-8") as f:
    json.dump(summary, f, ensure_ascii=False, indent=2)

print("legal_deck_count", summary["legal_deck_count"])
print("legal_build_count", summary["legal_build_count"])
print("perfect_success_count", summary["perfect_success_count"])
print("top10")
for row in summary["top20"][:10]:
    print(
        f"{row['success'] * 100:.4f}%",
        "first",
        row["first_turn"],
        row["potion"],
        row["family"],
        [round(p * 100, 4) for p in row["result"]],
        row["build_display"],
    )
print("best_by_turn")
for turn in sorted(summary["best_by_turn"], key=lambda x: 99 if x == "None" else int(x)):
    row = summary["best_by_turn"][turn][0]
    print(
        turn,
        f"{row['success'] * 100:.4f}%",
        row["potion"],
        row["family"],
        [round(p * 100, 4) for p in row["result"]],
        row["build_display"],
    )
print("best_by_potion")
for potion, vals in summary["best_by_potion"].items():
    row = vals[0]
    print(
        potion,
        f"{row['success'] * 100:.4f}%",
        "first",
        row["first_turn"],
        row["family"],
        row["build_display"],
    )
