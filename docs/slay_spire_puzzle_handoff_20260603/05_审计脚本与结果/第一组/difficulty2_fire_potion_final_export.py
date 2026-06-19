import json

from difficulty2_fire_potion_subset_audit import run


summary = run(102, 14, (0, 11, 15, 18))
with open("difficulty2_fire_potion_final_audit.json", "w", encoding="utf-8") as f:
    json.dump(summary, f, ensure_ascii=False, indent=2)

print("legal_deck_count", summary["legal_deck_count"])
print("perfect_success_count", summary["perfect_success_count"])
for row in summary["top20"][:10]:
    print(
        f"{row['success'] * 100:.4f}%",
        "first",
        row["first_turn"],
        [round(x * 100, 4) for x in row["result"]],
        row["deck"],
    )
