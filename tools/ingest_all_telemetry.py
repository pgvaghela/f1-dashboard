#!/usr/bin/env python3
"""
Batch-ingest FastF1 race telemetry for every available season/round (2018+).

Skips rounds that have not been run yet (no lap data). By default re-ingests
races that only have a partial driver set (fewer than --min-drivers).

Usage:
  python tools/ingest_all_telemetry.py
  python tools/ingest_all_telemetry.py --seasons 2024,2025,2026 --force
  python tools/ingest_all_telemetry.py --from-season 2018 --to-season 2026
"""
from __future__ import annotations

import argparse
import os
import sys
import time
from datetime import datetime

import fastf1

from ingest_telemetry import (
    IngestConfig,
    IngestResult,
    SCHEMA_SQL,
    connect,
    existing_driver_count,
    ingest_race,
)


def discover_rounds(season: int) -> list[int]:
    """Return round numbers that exist in the FastF1 calendar for this season."""
    rounds: list[int] = []
    for rnd in range(1, 30):
        try:
            fastf1.get_session(season, rnd, "R")
            rounds.append(rnd)
        except Exception:
            break
    return rounds


def parse_seasons(arg: str | None, from_season: int, to_season: int) -> list[int]:
    if arg:
        return sorted({int(s.strip()) for s in arg.split(",") if s.strip()})
    return list(range(from_season, to_season + 1))


def main():
    parser = argparse.ArgumentParser(description="Batch ingest FastF1 telemetry (2018+).")
    parser.add_argument("--seasons", default=None, help="Comma-separated years, e.g. 2024,2025")
    parser.add_argument("--from-season", type=int, default=2018)
    parser.add_argument("--to-season", type=int, default=datetime.now().year)
    parser.add_argument("--min-drivers", type=int, default=15,
                        help="Skip races that already have at least this many drivers.")
    parser.add_argument("--force", action="store_true",
                        help="Re-ingest even when the race already has enough drivers.")
    parser.add_argument("--samples", type=int, default=160)
    parser.add_argument("--cache", default=os.path.join(os.path.dirname(__file__), ".fastf1cache"))
    parser.add_argument("--dsn", default=None)
    parser.add_argument("--password", default=None)
    args = parser.parse_args()

    seasons = parse_seasons(args.seasons, args.from_season, args.to_season)
    seasons = [s for s in seasons if s >= 2018]
    if not seasons:
        print("No seasons to ingest (FastF1 covers 2018+).", file=sys.stderr)
        sys.exit(1)

    cfg = IngestConfig(
        samples=args.samples,
        cache_dir=args.cache,
        dsn=args.dsn,
        password=args.password,
        all_drivers=True,
        prune_extra_drivers=True,
    )

    os.makedirs(cfg.cache_dir, exist_ok=True)
    fastf1.Cache.enable_cache(cfg.cache_dir)

    conn = connect(cfg)
    cur = conn.cursor()
    cur.execute(SCHEMA_SQL)
    cur.close()
    conn.close()

    started = time.time()
    ok = 0
    skipped = 0
    failed = 0
    total_laps = 0
    total_samples = 0

    print(f"Batch telemetry ingest: seasons {seasons[0]}–{seasons[-1]} (all drivers)")
    print(f"Skip threshold: {args.min_drivers} drivers (use --force to override)\n")

    for season in seasons:
        rounds = discover_rounds(season)
        print(f"=== Season {season} ({len(rounds)} rounds on calendar) ===")

        for rnd in rounds:
            label = f"{season} R{rnd}"
            if not args.force:
                try:
                    check_conn = connect(cfg)
                    check_cur = check_conn.cursor()
                    have = existing_driver_count(check_cur, season, rnd)
                    check_cur.close()
                    check_conn.close()
                except Exception as ex:  # noqa: BLE001
                    print(f"  {label}: DB check failed ({ex}), ingesting anyway.")
                    have = 0
                if have >= args.min_drivers:
                    print(f"  {label}: skip ({have} drivers already ingested)")
                    skipped += 1
                    continue

            try:
                result: IngestResult = ingest_race(season, rnd, cfg)
            except Exception as ex:  # noqa: BLE001
                print(f"  {label}: FAILED — {ex}")
                failed += 1
                continue

            if result.skipped:
                print(f"  {label}: skip — {result.reason}")
                skipped += 1
            else:
                print(f"  {label}: {result.event_name} — {result.drivers} drivers, "
                      f"{result.laps} laps, {result.samples:,} samples")
                ok += 1
                total_laps += result.laps
                total_samples += result.samples

    elapsed = time.time() - started
    print(f"\nFinished in {elapsed / 60:.1f} min.")
    print(f"  Ingested: {ok} races ({total_laps:,} laps, {total_samples:,} samples)")
    print(f"  Skipped:  {skipped}")
    print(f"  Failed:   {failed}")

    if failed > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
