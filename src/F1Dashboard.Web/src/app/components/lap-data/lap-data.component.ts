import { Component, HostListener, NgZone, OnDestroy, OnInit, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { LapDataService } from '../../services/lap-data.service';
import { LapDetail, LapDriver, LapListItem, LapRace, LapSample } from '../../models/lap-data';
import { CountUpDirective } from '../../shared/count-up.directive';

/** Selections captured from the URL on load, consumed as each cascading list arrives. */
interface PendingUrlState {
  raceId?: number;
  driverId?: number;
  lapId?: number;
  cmpDriverId?: number;
  cmpLapId?: number;
}

interface SectorDelta {
  sector: number;
  primary: number | null;
  compare: number | null;
  delta: number | null;
}

@Component({
  selector: 'app-lap-data',
  imports: [DecimalPipe, CountUpDirective],
  templateUrl: './lap-data.component.html',
  styleUrl: './lap-data.component.css'
})
export class LapDataComponent implements OnInit, OnDestroy {
  private service = inject(LapDataService);
  private zone = inject(NgZone);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  // Full lap plays back in this many milliseconds regardless of real lap length.
  private static readonly PLAYBACK_MS = 14000;

  // Speed-trace SVG dimensions (view-box units).
  static readonly TRACE_W = 1000;
  static readonly TRACE_H = 220;
  private static readonly TRACE_PAD = 6;

  // Cascading selection state.
  seasons: number[] = [];
  races: LapRace[] = [];
  drivers: LapDriver[] = [];
  laps: LapListItem[] = [];

  selectedSeason: number | null = null;
  selectedRaceId: number | null = null;
  selectedDriverId: number | null = null;
  selectedLapId: number | null = null;

  lap: LapDetail | null = null;

  // Compare ("ghost") lap state — always within the same race as the primary.
  compareOn = false;
  compareDriverId: number | null = null;
  compareLapId: number | null = null;
  compareLaps: LapListItem[] = [];
  compareLap: LapDetail | null = null;
  loadingCompare = false;

  loading = false;
  loadingLap = false;
  error: string | null = null;

  // Playback / scrubbing.
  currentIndex = 0;
  isPlaying = false;
  private rafId: number | null = null;
  private playStartTs = 0;
  private playFromIndex = 0;

  // Hover tooltip (track map).
  hoverSample: LapSample | null = null;
  hoverX = 0;
  hoverY = 0;

  // Cached speed-trace paths (rebuilt when either lap loads).
  primarySpeedPath = '';
  compareSpeedPath = '';

  // Headshot URLs that 404'd, so we fall back to the team-colour dot.
  photoFailed = new Set<string>();

  private pendingUrl: PendingUrlState = {};

  // Maps an F1 sector-timing colour to its stroke; index aligns with sectorPaths.
  private static readonly SECTOR_STROKE: Record<string, string> = {
    purple: '#b14bff',
    green: '#33d17a',
    yellow: '#f4c542'
  };

  get traceW(): number {
    return LapDataComponent.TRACE_W;
  }

  get traceH(): number {
    return LapDataComponent.TRACE_H;
  }

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    this.pendingUrl = {
      raceId: this.numParam(q.get('race')),
      driverId: this.numParam(q.get('driver')),
      lapId: this.numParam(q.get('lap')),
      cmpDriverId: this.numParam(q.get('cmpDriver')),
      cmpLapId: this.numParam(q.get('cmpLap'))
    };
    const seasonParam = this.numParam(q.get('season'));
    this.loadSeasons(seasonParam);
  }

  ngOnDestroy(): void {
    this.stopRaf();
  }

  private numParam(v: string | null): number | undefined {
    if (v == null || v === '') {
      return undefined;
    }
    const n = Number(v);
    return Number.isFinite(n) ? n : undefined;
  }

  // ---- Cascading loaders -------------------------------------------------

  private loadSeasons(desiredSeason?: number): void {
    this.loading = true;
    this.service.getSeasons().subscribe({
      next: (seasons) => {
        this.seasons = seasons;
        this.loading = false;
        if (seasons.length > 0) {
          this.selectedSeason =
            desiredSeason != null && seasons.includes(desiredSeason) ? desiredSeason : seasons[0];
          this.loadRaces();
        }
      },
      error: (err) => this.fail(err, 'Failed to load seasons')
    });
  }

  onSeasonChange(value: string): void {
    this.selectedSeason = Number(value);
    this.disableCompare();
    this.resetFrom('race');
    this.loadRaces();
  }

  private loadRaces(): void {
    if (this.selectedSeason === null) {
      return;
    }
    this.loading = true;
    this.service.getRaces(this.selectedSeason).subscribe({
      next: (races) => {
        this.races = races;
        this.loading = false;
        if (races.length > 0) {
          this.selectedRaceId = this.pick(races.map((r) => r.raceId), this.pendingUrl.raceId, races[0].raceId);
          this.pendingUrl.raceId = undefined;
          this.loadDrivers();
        }
      },
      error: (err) => this.fail(err, 'Failed to load races')
    });
  }

  onRaceChange(value: string): void {
    this.selectedRaceId = Number(value);
    this.disableCompare();
    this.resetFrom('driver');
    this.loadDrivers();
  }

  private loadDrivers(): void {
    if (this.selectedRaceId === null) {
      return;
    }
    this.loading = true;
    this.service.getDrivers(this.selectedRaceId).subscribe({
      next: (drivers) => {
        this.drivers = drivers;
        this.loading = false;
        if (drivers.length > 0) {
          this.selectedDriverId = this.pick(
            drivers.map((d) => d.driverId),
            this.pendingUrl.driverId,
            drivers[0].driverId
          );
          this.pendingUrl.driverId = undefined;
          this.loadLaps();
        }
      },
      error: (err) => this.fail(err, 'Failed to load drivers')
    });
  }

  onDriverChange(value: string): void {
    this.selectedDriverId = Number(value);
    this.resetFrom('lap');
    this.loadLaps();
  }

  private loadLaps(): void {
    if (this.selectedRaceId === null || this.selectedDriverId === null) {
      return;
    }
    this.loading = true;
    this.service.getLaps(this.selectedRaceId, this.selectedDriverId).subscribe({
      next: (laps) => {
        this.laps = laps;
        this.loading = false;
        if (laps.length > 0) {
          const fastestId = this.fastestLapId(laps);
          this.selectedLapId = this.pick(laps.map((l) => l.lapId), this.pendingUrl.lapId, fastestId);
          this.pendingUrl.lapId = undefined;
          this.loadLap();
        }
      },
      error: (err) => this.fail(err, 'Failed to load laps')
    });
  }

  onLapChange(value: string): void {
    this.selectedLapId = Number(value);
    this.loadLap();
  }

  /** Jump the primary selection straight to this driver's fastest lap. */
  selectFastestLap(): void {
    const id = this.fastestLapId(this.laps);
    if (id != null && id !== this.selectedLapId) {
      this.selectedLapId = id;
      this.loadLap();
    }
  }

  get isOnFastestLap(): boolean {
    return this.selectedLapId != null && this.selectedLapId === this.fastestLapId(this.laps);
  }

  private fastestLapId(laps: LapListItem[]): number | null {
    const fastest = laps
      .filter((l) => l.lapTimeSeconds != null)
      .sort((a, b) => (a.lapTimeSeconds ?? 0) - (b.lapTimeSeconds ?? 0))[0];
    return (fastest ?? laps[0])?.lapId ?? null;
  }

  private loadLap(): void {
    if (this.selectedLapId === null) {
      return;
    }
    this.stopRaf();
    this.isPlaying = false;
    this.loadingLap = true;
    this.error = null;
    this.hoverSample = null;

    this.service.getLapDetail(this.selectedLapId).subscribe({
      next: (lap) => {
        this.lap = lap;
        this.currentIndex = 0;
        this.loadingLap = false;
        this.rebuildTraces();
        this.syncUrl();
        // Restore a compare selection that arrived via the URL.
        if (this.pendingUrl.cmpDriverId != null && !this.compareOn) {
          this.enableCompare(this.pendingUrl.cmpDriverId, this.pendingUrl.cmpLapId);
          this.pendingUrl.cmpDriverId = undefined;
          this.pendingUrl.cmpLapId = undefined;
        }
      },
      error: (err) => {
        this.lap = null;
        this.loadingLap = false;
        this.fail(err, 'Failed to load lap telemetry');
      }
    });
  }

  /** Clears state downstream of a changed selector so stale data never shows. */
  private resetFrom(level: 'race' | 'driver' | 'lap'): void {
    this.stopRaf();
    this.isPlaying = false;
    this.lap = null;
    this.hoverSample = null;
    this.primarySpeedPath = '';
    if (level === 'race') {
      this.races = [];
      this.selectedRaceId = null;
    }
    if (level === 'race' || level === 'driver') {
      this.drivers = [];
      this.selectedDriverId = null;
    }
    this.laps = [];
    this.selectedLapId = null;
  }

  // ---- Compare mode ------------------------------------------------------

  toggleCompare(): void {
    if (this.compareOn) {
      this.disableCompare();
      this.syncUrl();
    } else {
      // Default to the first driver that isn't the primary one.
      const other = this.drivers.find((d) => d.driverId !== this.selectedDriverId) ?? this.drivers[0];
      this.enableCompare(other?.driverId ?? null);
    }
  }

  private enableCompare(driverId: number | null, desiredLapId?: number): void {
    if (driverId == null || this.selectedRaceId == null) {
      return;
    }
    this.compareOn = true;
    this.compareDriverId = driverId;
    this.loadCompareLaps(desiredLapId);
  }

  private disableCompare(): void {
    this.compareOn = false;
    this.compareDriverId = null;
    this.compareLapId = null;
    this.compareLaps = [];
    this.compareLap = null;
    this.compareSpeedPath = '';
  }

  onCompareDriverChange(value: string): void {
    this.compareDriverId = Number(value);
    this.loadCompareLaps();
  }

  onCompareLapChange(value: string): void {
    this.compareLapId = Number(value);
    this.loadCompareLap();
  }

  private loadCompareLaps(desiredLapId?: number): void {
    if (this.selectedRaceId == null || this.compareDriverId == null) {
      return;
    }
    this.loadingCompare = true;
    this.service.getLaps(this.selectedRaceId, this.compareDriverId).subscribe({
      next: (laps) => {
        this.compareLaps = laps;
        if (laps.length > 0) {
          this.compareLapId = this.pick(laps.map((l) => l.lapId), desiredLapId, this.fastestLapId(laps));
          this.loadCompareLap();
        } else {
          this.loadingCompare = false;
          this.compareLap = null;
        }
      },
      error: () => {
        this.loadingCompare = false;
        this.compareLaps = [];
      }
    });
  }

  private loadCompareLap(): void {
    if (this.compareLapId == null) {
      return;
    }
    this.loadingCompare = true;
    this.service.getLapDetail(this.compareLapId).subscribe({
      next: (lap) => {
        this.compareLap = lap;
        this.loadingCompare = false;
        this.rebuildTraces();
        this.syncUrl();
      },
      error: () => {
        this.compareLap = null;
        this.loadingCompare = false;
      }
    });
  }

  get compareDriver(): LapDriver | null {
    return this.drivers.find((d) => d.driverId === this.compareDriverId) ?? null;
  }

  get compareSamples(): LapSample[] {
    return this.compareLap?.samples ?? [];
  }

  get compareMaxIndex(): number {
    return Math.max(0, this.compareSamples.length - 1);
  }

  /** Compare lap is driven by the same lap-fraction as the primary lap. */
  get compareIndex(): number {
    return Math.round(this.progress * this.compareMaxIndex);
  }

  get compareCurrentSample(): LapSample | null {
    return this.compareSamples[this.compareIndex] ?? null;
  }

  // ---- Derived getters ---------------------------------------------------

  get samples(): LapSample[] {
    return this.lap?.samples ?? [];
  }

  get hasSamples(): boolean {
    return this.samples.length > 0;
  }

  get currentSample(): LapSample | null {
    return this.samples[this.currentIndex] ?? null;
  }

  /** A few fading samples behind the car for a motion trail. */
  get carTrail(): { x: number; y: number; opacity: number; r: number }[] {
    const out: { x: number; y: number; opacity: number; r: number }[] = [];
    const offsets = [3, 6, 9, 12, 15];
    offsets.forEach((off, k) => {
      const s = this.samples[this.currentIndex - off];
      if (s) {
        out.push({ x: s.x, y: s.y, opacity: Math.max(0.06, 0.42 - k * 0.08), r: Math.max(3, 9 - k * 1.3) });
      }
    });
    return out;
  }

  get maxIndex(): number {
    return Math.max(0, this.samples.length - 1);
  }

  get progress(): number {
    return this.maxIndex > 0 ? this.currentIndex / this.maxIndex : 0;
  }

  get cursorX(): number {
    return this.progress * LapDataComponent.TRACE_W;
  }

  get selectedDriver(): LapDriver | null {
    return this.drivers.find((d) => d.driverId === this.selectedDriverId) ?? null;
  }

  get topSpeed(): number {
    return this.samples.reduce((m, s) => Math.max(m, s.speed), 0);
  }

  get avgSpeed(): number {
    if (this.samples.length === 0) {
      return 0;
    }
    return this.samples.reduce((sum, s) => sum + s.speed, 0) / this.samples.length;
  }

  /** Per-sector primary/compare times and delta, for the compare panel. */
  get sectorDeltas(): SectorDelta[] {
    const prim = this.lap?.sectors ?? [];
    const comp = this.compareLap?.sectors ?? [];
    return prim.map((p, i) => {
      const c = comp[i]?.timeSeconds ?? null;
      const delta = p.timeSeconds != null && c != null ? c - p.timeSeconds : null;
      return { sector: p.sector, primary: p.timeSeconds, compare: c, delta };
    });
  }

  get lapTimeDelta(): number | null {
    const p = this.lap?.lapTimeSeconds;
    const c = this.compareLap?.lapTimeSeconds;
    return p != null && c != null ? c - p : null;
  }

  // ---- Speed trace -------------------------------------------------------

  private get speedMax(): number {
    const all = [...this.samples, ...this.compareSamples];
    const max = all.reduce((m, s) => Math.max(m, s.speed), 0);
    return max > 0 ? max * 1.05 : 1;
  }

  private rebuildTraces(): void {
    const max = this.speedMax;
    this.primarySpeedPath = this.buildSpeedPath(this.samples, max);
    this.compareSpeedPath = this.compareOn ? this.buildSpeedPath(this.compareSamples, max) : '';
  }

  private buildSpeedPath(samples: LapSample[], speedMax: number): string {
    if (samples.length < 2) {
      return '';
    }
    const w = LapDataComponent.TRACE_W;
    const h = LapDataComponent.TRACE_H;
    const pad = LapDataComponent.TRACE_PAD;
    const usableH = h - pad * 2;
    const maxI = samples.length - 1;
    const pts = samples.map((s, i) => {
      const x = (i / maxI) * w;
      const y = h - pad - (s.speed / speedMax) * usableH;
      return `${x.toFixed(1)} ${y.toFixed(1)}`;
    });
    return 'M ' + pts.join(' L ');
  }

  /** Hovering/clicking the speed trace scrubs the lap. */
  onTraceMove(event: MouseEvent): void {
    if (!this.lap || !this.hasSamples) {
      return;
    }
    const svg = event.currentTarget as SVGSVGElement;
    const rect = svg.getBoundingClientRect();
    if (rect.width === 0) {
      return;
    }
    this.pause();
    const frac = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
    this.currentIndex = Math.round(frac * this.maxIndex);
  }

  // ---- Playback ----------------------------------------------------------

  togglePlay(): void {
    if (!this.hasSamples) {
      return;
    }
    this.isPlaying ? this.pause() : this.play();
  }

  stepBack(): void {
    this.pause();
    this.currentIndex = Math.max(0, this.currentIndex - 1);
  }

  stepForward(): void {
    this.pause();
    this.currentIndex = Math.min(this.maxIndex, this.currentIndex + 1);
  }

  private play(): void {
    if (this.currentIndex >= this.maxIndex) {
      this.currentIndex = 0;
    }
    this.isPlaying = true;
    this.playFromIndex = this.currentIndex;
    this.playStartTs = performance.now();

    // Run the rAF loop outside Angular to avoid a change-detection cycle every
    // frame; we re-enter the zone only when the index actually changes.
    this.zone.runOutsideAngular(() => {
      const step = (now: number) => {
        const elapsed = now - this.playStartTs;
        const progress = this.playFromIndex / this.maxIndex + elapsed / LapDataComponent.PLAYBACK_MS;
        const idx = Math.min(this.maxIndex, Math.round(progress * this.maxIndex));

        if (idx !== this.currentIndex) {
          this.zone.run(() => (this.currentIndex = idx));
        }
        if (idx >= this.maxIndex) {
          this.zone.run(() => this.pause());
          return;
        }
        this.rafId = requestAnimationFrame(step);
      };
      this.rafId = requestAnimationFrame(step);
    });
  }

  private pause(): void {
    this.isPlaying = false;
    this.stopRaf();
  }

  private stopRaf(): void {
    if (this.rafId !== null) {
      cancelAnimationFrame(this.rafId);
      this.rafId = null;
    }
  }

  onScrub(value: string): void {
    this.pause();
    this.currentIndex = Number(value);
  }

  // ---- Keyboard shortcuts ------------------------------------------------

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (!this.lap || !this.hasSamples) {
      return;
    }
    // Don't hijack typing in the dropdowns.
    const tag = (event.target as HTMLElement)?.tagName;
    if (tag === 'SELECT' || tag === 'INPUT' || tag === 'TEXTAREA') {
      return;
    }
    switch (event.key) {
      case ' ':
        event.preventDefault();
        this.togglePlay();
        break;
      case 'ArrowLeft':
        event.preventDefault();
        this.stepBack();
        break;
      case 'ArrowRight':
        event.preventDefault();
        this.stepForward();
        break;
      case 'Home':
        event.preventDefault();
        this.pause();
        this.currentIndex = 0;
        break;
      case 'End':
        event.preventDefault();
        this.pause();
        this.currentIndex = this.maxIndex;
        break;
    }
  }

  // ---- Hover hit-testing (track map) -------------------------------------

  onTrackMove(event: MouseEvent): void {
    if (!this.lap || !this.hasSamples) {
      return;
    }
    const svg = event.currentTarget as SVGSVGElement;
    const rect = svg.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) {
      return;
    }

    // Map cursor to the SVG's view-box coordinate space, then find the closest sample.
    const vx = ((event.clientX - rect.left) / rect.width) * this.lap.outline.width;
    const vy = ((event.clientY - rect.top) / rect.height) * this.lap.outline.height;

    let best: LapSample | null = null;
    let bestDist = Infinity;
    for (const s of this.samples) {
      const dx = s.x - vx;
      const dy = s.y - vy;
      const d = dx * dx + dy * dy;
      if (d < bestDist) {
        bestDist = d;
        best = s;
      }
    }

    this.hoverSample = best;
    const wrapper = svg.parentElement?.getBoundingClientRect();
    this.hoverX = event.clientX - (wrapper?.left ?? 0);
    this.hoverY = event.clientY - (wrapper?.top ?? 0);
  }

  onTrackLeave(): void {
    this.hoverSample = null;
  }

  // ---- URL persistence ---------------------------------------------------

  private syncUrl(): void {
    const queryParams: Record<string, number | null> = {
      season: this.selectedSeason,
      race: this.selectedRaceId,
      driver: this.selectedDriverId,
      lap: this.selectedLapId,
      cmpDriver: this.compareOn ? this.compareDriverId : null,
      cmpLap: this.compareOn ? this.compareLapId : null
    };
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      replaceUrl: true
    });
  }

  // ---- Formatting helpers ------------------------------------------------

  formatLapTime(seconds: number | null | undefined): string {
    if (seconds == null) {
      return '—';
    }
    const m = Math.floor(seconds / 60);
    const s = seconds - m * 60;
    return m > 0 ? `${m}:${s.toFixed(3).padStart(6, '0')}` : s.toFixed(3);
  }

  formatDelta(seconds: number | null): string {
    if (seconds == null) {
      return '—';
    }
    const sign = seconds > 0 ? '+' : seconds < 0 ? '−' : '';
    return `${sign}${Math.abs(seconds).toFixed(3)}`;
  }

  deltaClass(seconds: number | null): string {
    if (seconds == null || seconds === 0) {
      return 'delta--neutral';
    }
    // Negative means the compare lap is faster than the primary.
    return seconds < 0 ? 'delta--faster' : 'delta--slower';
  }

  sectorClass(color: string): string {
    return `sector--${color}`;
  }

  /** Stroke colour for sector segment at `index`, matching its timing colour. */
  sectorStroke(index: number): string {
    const color = this.lap?.sectors?.[index]?.color ?? '';
    return LapDataComponent.SECTOR_STROKE[color] ?? '#8b95a8';
  }

  onPhotoError(url: string): void {
    this.photoFailed.add(url);
  }

  private pick(available: number[], desired: number | null | undefined, fallback: number | null): number | null {
    if (desired != null && available.includes(desired)) {
      return desired;
    }
    return fallback;
  }

  private fail(err: unknown, message: string): void {
    this.error = message;
    this.loading = false;
    this.loadingLap = false;
    console.error(err);
  }
}
