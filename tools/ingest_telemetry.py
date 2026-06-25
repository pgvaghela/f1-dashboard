#!/usr/bin/env python3
"""
FastF1 -> Postgres telemetry ingestion for the F1 Dashboard "Lap Data" tab.

For a given season/round it loads a race session via FastF1 (cached), then for the
selected drivers writes, per lap:
  - ordered { t, x, y, speed, compound, position } samples (downsampled),
  - the three sector times, and
  - a per-sector colour classification.

It also derives the circuit outline from a reference (fastest) lap's X/Y so the
outline and the trace share the same coordinate space. All coordinates are
normalized into a fixed-aspect SVG view box during ingestion, so the frontend just
plots them directly.

Sector colour baseline (standard F1 timing):
  purple = fastest of that sector across the WHOLE session (every driver/lap)
  green  = the driver's PERSONAL best sector (their own fastest), not overall best
  yellow = slower than the driver's personal best (or missing)

This is a one-off / on-demand step, mirroring the C# Jolpica importer — not a live
call from the app. FastF1 only covers 2018+.

Usage:
  python tools/ingest_telemetry.py --season 2024 --round 1 --all-drivers
  python tools/ingest_telemetry.py --season 2024 --round 1 \\
      --drivers VER,NOR,LEC,RUS,HAM --samples 160

DB connection comes from env (PGHOST/PGPORT/PGDATABASE/PGUSER/PGPASSWORD) with
local-dev defaults, or --dsn.
"""
from __future__ import annotations

import argparse
import os
import sys
from dataclasses import dataclass
from typing import Iterable

import numpy as np
import psycopg2
from psycopg2.extras import execute_values

import fastf1


SCHEMA_SQL = """
CREATE TABLE IF NOT EXISTS telemetry_races (
    id                 integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    season             integer NOT NULL,
    round              integer NOT NULL,
    event_name         text    NOT NULL,
    circuit_short_name text    NOT NULL,
    outline_path       text    NOT NULL,
    view_width         double precision NOT NULL,
    view_height        double precision NOT NULL,
    UNIQUE (season, round)
);

CREATE TABLE IF NOT EXISTS telemetry_drivers (
    id                integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    telemetry_race_id integer NOT NULL REFERENCES telemetry_races(id) ON DELETE CASCADE,
    code              text NOT NULL,
    full_name         text NOT NULL,
    team_name         text NOT NULL,
    team_color        text NOT NULL,
    UNIQUE (telemetry_race_id, code)
);

CREATE TABLE IF NOT EXISTS telemetry_laps (
    id                  integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    telemetry_driver_id integer NOT NULL REFERENCES telemetry_drivers(id) ON DELETE CASCADE,
    lap_number          integer NOT NULL,
    lap_time_seconds    double precision,
    compound            text NOT NULL DEFAULT '',
    sector1_seconds     double precision,
    sector2_seconds     double precision,
    sector3_seconds     double precision,
    sector1_color       text NOT NULL DEFAULT 'yellow',
    sector2_color       text NOT NULL DEFAULT 'yellow',
    sector3_color       text NOT NULL DEFAULT 'yellow',
    UNIQUE (telemetry_driver_id, lap_number)
);

CREATE TABLE IF NOT EXISTS telemetry_samples (
    id               bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    telemetry_lap_id integer NOT NULL REFERENCES telemetry_laps(id) ON DELETE CASCADE,
    t                double precision NOT NULL,
    x                double precision NOT NULL,
    y                double precision NOT NULL,
    speed            double precision NOT NULL,
    compound         text NOT NULL DEFAULT '',
    position         integer
);

CREATE INDEX IF NOT EXISTS ix_telemetry_samples_lap ON telemetry_samples(telemetry_lap_id);

-- Added later: the outline split into its three physical sectors, and driver headshots.
ALTER TABLE telemetry_races ADD COLUMN IF NOT EXISTS sector1_path text NOT NULL DEFAULT '';
ALTER TABLE telemetry_races ADD COLUMN IF NOT EXISTS sector2_path text NOT NULL DEFAULT '';
ALTER TABLE telemetry_races ADD COLUMN IF NOT EXISTS sector3_path text NOT NULL DEFAULT '';
ALTER TABLE telemetry_drivers ADD COLUMN IF NOT EXISTS headshot_url text NOT NULL DEFAULT '';
"""

# Target outline size: the longer axis maps to this many units, aspect preserved.
VIEW_MAX = 1000.0
VIEW_PAD = 40.0
EPS = 1e-3


@dataclass
class IngestResult:
    season: int
    round: int
    event_name: str
    race_id: int
    drivers: int
    laps: int
    samples: int
    skipped: bool = False
    reason: str = ""


@dataclass
class IngestConfig:
    samples: int = 160
    max_laps: int = 0
    cache_dir: str = ""
    dsn: str | None = None
    password: str | None = None
    all_drivers: bool = False
    drivers: str = "VER,NOR,LEC,RUS,HAM"
    prune_extra_drivers: bool = False


def normalizer(xs, ys):
    """Build an affine transform that fits (xs, ys) into a padded, aspect-correct box."""
    min_x, max_x = float(np.min(xs)), float(np.max(xs))
    min_y, max_y = float(np.min(ys)), float(np.max(ys))
    span_x = max(max_x - min_x, 1e-6)
    span_y = max(max_y - min_y, 1e-6)
    scale = (VIEW_MAX - 2 * VIEW_PAD) / max(span_x, span_y)
    view_w = span_x * scale + 2 * VIEW_PAD
    view_h = span_y * scale + 2 * VIEW_PAD

    def transform(x, y):
        nx = (x - min_x) * scale + VIEW_PAD
        ny = view_h - ((y - min_y) * scale + VIEW_PAD)
        return nx, ny

    return transform, view_w, view_h


def downsample_indices(n, target):
    if n <= target:
        return list(range(n))
    return list(np.linspace(0, n - 1, target).astype(int))


def td_seconds(value):
    """A pandas Timedelta/NaT -> float seconds or None."""
    try:
        if value is None or (hasattr(value, "value") and np.isnan(value.value)):
            return None
        secs = value.total_seconds()
        return None if (secs is None or np.isnan(secs)) else float(secs)
    except (AttributeError, ValueError, TypeError):
        return None


def points_to_path(points):
    if not points:
        return ""
    return "M " + " L ".join(f"{x:.2f} {y:.2f}" for x, y in points)


def build_paths(ref_tel, ref_lap, transform, n):
    """Build the full closed outline plus three open paths, one per physical sector."""
    t0 = ref_tel["Time"].iloc[0]
    s1 = td_seconds(ref_lap["Sector1Time"])
    s2 = td_seconds(ref_lap["Sector2Time"])
    b1 = s1
    b2 = (s1 + s2) if (s1 is not None and s2 is not None) else None

    idxs = downsample_indices(len(ref_tel), max(n * 2, 300))
    all_pts = []
    segs = {1: [], 2: [], 3: []}
    for i in idxs:
        t = td_seconds(ref_tel["Time"].iloc[i] - t0) or 0.0
        pt = transform(ref_tel["X"].iloc[i], ref_tel["Y"].iloc[i])
        all_pts.append(pt)
        if b1 is not None and t <= b1:
            sec = 1
        elif b2 is not None and t <= b2:
            sec = 2
        else:
            sec = 3
        segs[sec].append(pt)

    if segs[1] and segs[2]:
        segs[2].insert(0, segs[1][-1])
    if segs[2] and segs[3]:
        segs[3].insert(0, segs[2][-1])

    outline_path = points_to_path(all_pts) + " Z"
    sector_paths = [points_to_path(segs[1]), points_to_path(segs[2]), points_to_path(segs[3])]
    return outline_path, sector_paths


def fetch_headshots(codes: Iterable[str], session_key: str | int | None = None) -> dict[str, str]:
    """Map driver code -> headshot URL via OpenF1 (best-effort; returns {} on failure)."""
    try:
        import requests

        params: dict[str, str] = {}
        if session_key is not None:
            params["session_key"] = str(session_key)
        else:
            params["session_key"] = "latest"

        resp = requests.get("https://api.openf1.org/v1/drivers", params=params, timeout=15)
        resp.raise_for_status()
        out: dict[str, str] = {}
        for d in resp.json():
            code = (d.get("name_acronym") or "").upper()
            url = d.get("headshot_url")
            if code and url:
                out[code] = url.replace("1col", "3col") if "1col" in url else url
        return {c: out[c] for c in codes if c in out}
    except Exception as ex:  # noqa: BLE001
        print(f"  (headshots unavailable: {ex})")
        return {}


def classify(sector_time, session_best, driver_best):
    if sector_time is None:
        return "yellow"
    if session_best is not None and abs(sector_time - session_best) < EPS:
        return "purple"
    if driver_best is not None and abs(sector_time - driver_best) < EPS:
        return "green"
    return "yellow"


def connect(cfg: IngestConfig):
    if cfg.dsn:
        return psycopg2.connect(cfg.dsn)
    return psycopg2.connect(
        host=os.environ.get("PGHOST", "localhost"),
        port=os.environ.get("PGPORT", "5432"),
        dbname=os.environ.get("PGDATABASE", "f1_dashboard"),
        user=os.environ.get("PGUSER", "postgres"),
        password=cfg.password or os.environ.get("PGPASSWORD", ""),
    )


def resolve_driver_codes(session, cfg: IngestConfig) -> list[str]:
    """Every driver in the session who has at least one lap, or an explicit code list."""
    if cfg.all_drivers:
        laps = session.laps
        results = session.results
        codes: list[str] = []
        for _, row in results.iterrows():
            code = str(row.get("Abbreviation") or "").strip().upper()
            if not code or code == "NAN":
                continue
            if len(laps.pick_drivers(code)) > 0:
                codes.append(code)
        return sorted(set(codes))

    return [c.strip().upper() for c in cfg.drivers.split(",") if c.strip()]


def existing_driver_count(cur, season: int, round_: int) -> int:
    cur.execute(
        """
        SELECT COUNT(td.id)
        FROM telemetry_races tr
        JOIN telemetry_drivers td ON td.telemetry_race_id = tr.id
        WHERE tr.season = %s AND tr.round = %s
        """,
        (season, round_),
    )
    return int(cur.fetchone()[0])


def prune_stale_drivers(cur, race_id: int, active_codes: set[str]) -> None:
    """Remove drivers from a race that are no longer part of the ingest set."""
    if not active_codes:
        return
    cur.execute(
        """
        DELETE FROM telemetry_drivers
        WHERE telemetry_race_id = %s AND code <> ALL(%s)
        """,
        (race_id, list(active_codes)),
    )


def ingest_race(season: int, round_: int, cfg: IngestConfig, conn=None) -> IngestResult:
    """Load one FastF1 race session and write telemetry for the configured drivers."""
    if season < 2018:
        raise ValueError("FastF1 only covers 2018+.")

    cache_dir = cfg.cache_dir or os.path.join(os.path.dirname(__file__), ".fastf1cache")
    os.makedirs(cache_dir, exist_ok=True)
    fastf1.Cache.enable_cache(cache_dir)

    print(f"Loading {season} round {round_} (Race)...")
    try:
        session = fastf1.get_session(season, round_, "R")
        session.load(telemetry=True, laps=True, weather=False, messages=False)
    except Exception as ex:  # noqa: BLE001
        return IngestResult(
            season, round_, "", 0, 0, 0, 0,
            skipped=True, reason=f"session unavailable: {ex}",
        )

    laps = session.laps
    if laps is None or len(laps) == 0:
        return IngestResult(
            season, round_, str(session.event.get("EventName", "")), 0, 0, 0, 0,
            skipped=True, reason="no laps (race not run yet?)",
        )

    event_name = str(session.event["EventName"])
    circuit_short = str(session.event.get("Location", event_name))
    codes = resolve_driver_codes(session, cfg)
    if not codes:
        return IngestResult(
            season, round_, event_name, 0, 0, 0, 0,
            skipped=True, reason="no drivers with laps",
        )

    session_best = {
        1: td_seconds(laps["Sector1Time"].min()),
        2: td_seconds(laps["Sector2Time"].min()),
        3: td_seconds(laps["Sector3Time"].min()),
    }

    ref_lap = laps.pick_fastest()
    ref_tel = ref_lap.get_telemetry()
    transform, view_w, view_h = normalizer(ref_tel["X"].to_numpy(), ref_tel["Y"].to_numpy())
    outline_path, sector_paths = build_paths(ref_tel, ref_lap, transform, cfg.samples)

    results = session.results
    session_key = getattr(session, "session_identifier", None)
    headshots = fetch_headshots(codes, session_key)

    owns_conn = conn is None
    if owns_conn:
        conn = connect(cfg)
    conn.autocommit = False
    cur = conn.cursor()
    cur.execute(SCHEMA_SQL)

    cur.execute(
        """
        INSERT INTO telemetry_races
            (season, round, event_name, circuit_short_name, outline_path, view_width, view_height,
             sector1_path, sector2_path, sector3_path)
        VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
        ON CONFLICT (season, round) DO UPDATE SET
            event_name = EXCLUDED.event_name,
            circuit_short_name = EXCLUDED.circuit_short_name,
            outline_path = EXCLUDED.outline_path,
            view_width = EXCLUDED.view_width,
            view_height = EXCLUDED.view_height,
            sector1_path = EXCLUDED.sector1_path,
            sector2_path = EXCLUDED.sector2_path,
            sector3_path = EXCLUDED.sector3_path
        RETURNING id
        """,
        (season, round_, event_name, circuit_short, outline_path, view_w, view_h,
         sector_paths[0], sector_paths[1], sector_paths[2]),
    )
    race_id = cur.fetchone()[0]

    total_laps = 0
    total_samples = 0
    ingested_codes: set[str] = set()

    for code in codes:
        drv_laps = laps.pick_drivers(code)
        if len(drv_laps) == 0:
            print(f"  {code}: no laps, skipping.")
            continue

        ingested_codes.add(code)

        try:
            row = results.loc[results["Abbreviation"] == code].iloc[0]
            full_name = str(row["FullName"])
            team_name = str(row["TeamName"])
            color = str(row["TeamColor"]) or "808080"
        except (IndexError, KeyError):
            full_name, team_name, color = code, "", "808080"
        team_color = color if color.startswith("#") else f"#{color}"
        headshot_url = headshots.get(code, "")

        driver_best = {
            1: td_seconds(drv_laps["Sector1Time"].min()),
            2: td_seconds(drv_laps["Sector2Time"].min()),
            3: td_seconds(drv_laps["Sector3Time"].min()),
        }

        cur.execute(
            """
            INSERT INTO telemetry_drivers (telemetry_race_id, code, full_name, team_name, team_color, headshot_url)
            VALUES (%s, %s, %s, %s, %s, %s)
            ON CONFLICT (telemetry_race_id, code) DO UPDATE SET
                full_name = EXCLUDED.full_name,
                team_name = EXCLUDED.team_name,
                team_color = EXCLUDED.team_color,
                headshot_url = CASE
                    WHEN EXCLUDED.headshot_url <> '' THEN EXCLUDED.headshot_url
                    ELSE telemetry_drivers.headshot_url
                END
            RETURNING id
            """,
            (race_id, code, full_name, team_name, team_color, headshot_url),
        )
        driver_id = cur.fetchone()[0]
        cur.execute("DELETE FROM telemetry_laps WHERE telemetry_driver_id = %s", (driver_id,))

        lap_rows = list(drv_laps.iterlaps())
        if cfg.max_laps > 0:
            lap_rows = lap_rows[: cfg.max_laps]

        for _, lap in lap_rows:
            try:
                lap_number = int(lap["LapNumber"])
            except (ValueError, TypeError):
                continue

            compound = str(lap["Compound"]) if lap["Compound"] is not None else ""
            if compound in ("nan", "None"):
                compound = ""
            lap_time = td_seconds(lap["LapTime"])
            s1, s2, s3 = (td_seconds(lap["Sector1Time"]), td_seconds(lap["Sector2Time"]), td_seconds(lap["Sector3Time"]))

            try:
                position = int(lap["Position"]) if not np.isnan(lap["Position"]) else None
            except (ValueError, TypeError):
                position = None

            cur.execute(
                """
                INSERT INTO telemetry_laps
                    (telemetry_driver_id, lap_number, lap_time_seconds, compound,
                     sector1_seconds, sector2_seconds, sector3_seconds,
                     sector1_color, sector2_color, sector3_color)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                RETURNING id
                """,
                (
                    driver_id, lap_number, lap_time, compound, s1, s2, s3,
                    classify(s1, session_best[1], driver_best[1]),
                    classify(s2, session_best[2], driver_best[2]),
                    classify(s3, session_best[3], driver_best[3]),
                ),
            )
            lap_id = cur.fetchone()[0]

            try:
                tel = lap.get_telemetry()
            except Exception as ex:  # noqa: BLE001
                print(f"  {code} lap {lap_number}: no telemetry ({ex}).")
                continue
            if len(tel) == 0:
                continue

            t0 = tel["Time"].iloc[0]
            idxs = downsample_indices(len(tel), cfg.samples)
            sample_values = []
            for i in idxs:
                t = td_seconds(tel["Time"].iloc[i] - t0) or 0.0
                nx, ny = transform(tel["X"].iloc[i], tel["Y"].iloc[i])
                speed = float(tel["Speed"].iloc[i]) if not np.isnan(tel["Speed"].iloc[i]) else 0.0
                sample_values.append((lap_id, float(t), float(nx), float(ny), float(speed), compound, position))

            execute_values(
                cur,
                "INSERT INTO telemetry_samples (telemetry_lap_id, t, x, y, speed, compound, position) VALUES %s",
                sample_values,
            )
            total_laps += 1
            total_samples += len(sample_values)

        print(f"  {code}: {len(lap_rows)} laps -> ingested.")

    if cfg.prune_extra_drivers or cfg.all_drivers:
        prune_stale_drivers(cur, race_id, ingested_codes)

    conn.commit()
    cur.close()
    if owns_conn:
        conn.close()

    print(f"Done. Race id {race_id}: {total_laps} laps, {total_samples} samples for {len(ingested_codes)} drivers.")
    return IngestResult(
        season, round_, event_name, race_id, len(ingested_codes), total_laps, total_samples,
    )


def main():
    parser = argparse.ArgumentParser(description="Ingest FastF1 lap telemetry into Postgres.")
    parser.add_argument("--season", type=int, required=True)
    parser.add_argument("--round", type=int, required=True)
    parser.add_argument("--drivers", default="VER,NOR,LEC,RUS,HAM",
                        help="Comma-separated 3-letter driver codes (ignored with --all-drivers).")
    parser.add_argument("--all-drivers", action="store_true",
                        help="Ingest every driver in the session who has lap data.")
    parser.add_argument("--samples", type=int, default=160,
                        help="Downsampled points per lap.")
    parser.add_argument("--max-laps", type=int, default=0,
                        help="Cap laps per driver (0 = all).")
    parser.add_argument("--cache", default=os.path.join(os.path.dirname(__file__), ".fastf1cache"))
    parser.add_argument("--dsn", default=None)
    parser.add_argument("--password", default=None)
    args = parser.parse_args()

    cfg = IngestConfig(
        samples=args.samples,
        max_laps=args.max_laps,
        cache_dir=args.cache,
        dsn=args.dsn,
        password=args.password,
        all_drivers=args.all_drivers,
        drivers=args.drivers,
        prune_extra_drivers=args.all_drivers,
    )

    try:
        result = ingest_race(args.season, args.round, cfg)
    except Exception as ex:  # noqa: BLE001
        print(f"Failed: {ex}", file=sys.stderr)
        sys.exit(1)

    if result.skipped:
        print(f"Skipped: {result.reason}", file=sys.stderr)
        sys.exit(2)


if __name__ == "__main__":
    main()
