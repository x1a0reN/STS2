from difficulty2_fire_potion_subset_audit import run


for hp in (78, 84, 90, 96, 102):
    summary = run(hp, 12, (0, 11, 15, 18))
    print("HP", hp, "legal", summary["legal_deck_count"], "perfect", summary["perfect_success_count"])
    for row in summary["top20"][:5]:
        vec = [round(x * 100, 4) for x in row["result"]]
        print(" ", f"{row['success'] * 100:.4f}%", "first", row["first_turn"], vec, row["deck"])
    print(" best_by_turn")
    for turn in sorted(summary["best_by_turn"], key=lambda x: 99 if x == "None" else int(x)):
        row = summary["best_by_turn"][turn][0]
        print(" ", turn, f"{row['success'] * 100:.4f}%", [round(x * 100, 4) for x in row["result"]], row["deck"])
