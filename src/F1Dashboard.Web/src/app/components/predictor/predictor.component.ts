import { Component, OnInit, inject } from '@angular/core';
import { DecimalPipe, PercentPipe } from '@angular/common';
import { PredictionService } from '../../services/prediction.service';
import { DriverPrediction, PredictableRace, RacePredictionResult } from '../../models/prediction';

@Component({
  selector: 'app-predictor',
  imports: [PercentPipe, DecimalPipe],
  templateUrl: './predictor.component.html',
  styleUrl: './predictor.component.css'
})
export class PredictorComponent implements OnInit {
  private predictionService = inject(PredictionService);

  readonly seasons = [2026, 2025, 2024, 2023];
  selectedSeason = this.seasons[0]; // current season — lands on the next upcoming race

  races: PredictableRace[] = [];
  selectedRaceId: number | null = null;
  result: RacePredictionResult | null = null;

  loadingRaces = true;
  loadingPrediction = false;
  error: string | null = null;

  /** Bumped on every new prediction so the finish-line animation replays. */
  animateKey = 0;

  /** Team primary colors, matched loosely against the constructor name. */
  private static readonly TEAM_COLORS: ReadonlyArray<[string, string]> = [
    ['red bull', '#3671c6'],
    ['ferrari', '#e8002d'],
    ['mercedes', '#27f4d2'],
    ['mclaren', '#ff8000'],
    ['aston', '#229971'],
    ['alpine', '#0093cc'],
    ['williams', '#64c4ff'],
    ['haas', '#b6babd'],
    ['sauber', '#52e252'],
    ['alfa', '#c92d4b'],
    ['rb', '#6692ff'],
    ['alphatauri', '#6692ff']
  ];

  ngOnInit(): void {
    this.loadRaces();
  }

  onSeasonChange(value: string): void {
    this.selectedSeason = Number(value);
    this.loadRaces();
  }

  onRaceChange(value: string): void {
    this.selectedRaceId = Number(value);
    this.loadPrediction();
  }

  /** The model's top pick (drivers come back sorted by win probability). */
  get topPick(): DriverPrediction | null {
    return this.result?.drivers[0] ?? null;
  }

  get actualWinner(): DriverPrediction | null {
    return this.result?.drivers.find((d) => d.isActualWinner) ?? null;
  }

  /** True once the race has been run (we have a real winner to compare against). */
  get hasResult(): boolean {
    return this.actualWinner !== null;
  }

  get calledIt(): boolean {
    return this.topPick?.isActualWinner ?? false;
  }

  /** True when viewing a race that hasn't been run yet. */
  get isUpcoming(): boolean {
    return this.result?.isUpcoming ?? false;
  }

  /** The three drivers shown crossing the finish line. */
  get topThree(): DriverPrediction[] {
    return this.result?.drivers.slice(0, 3) ?? [];
  }

  /** Resolves a constructor name to its team color (falls back to the accent blue). */
  teamColor(constructorName: string): string {
    const name = constructorName.toLowerCase();
    const match = PredictorComponent.TEAM_COLORS.find(([key]) => name.includes(key));
    return match ? match[1] : '#2997ff';
  }

  /** Bar width scaled to the leader so the field stays visually readable. */
  barWidth(driver: DriverPrediction): number {
    const top = this.topPick?.winProbability ?? 0;
    return top > 0 ? (driver.winProbability / top) * 100 : 0;
  }

  private loadRaces(): void {
    this.loadingRaces = true;
    this.error = null;
    this.result = null;
    this.selectedRaceId = null;

    this.predictionService.getRaces(this.selectedSeason).subscribe({
      next: (races) => {
        this.races = races;
        this.loadingRaces = false;
        if (races.length > 0) {
          this.selectedRaceId = this.defaultRaceId(races);
          this.loadPrediction();
        }
      },
      error: (err) => {
        this.races = [];
        this.loadingRaces = false;
        this.handleError(err, 'Failed to load races for this season');
      }
    });
  }

  /** Default to the soonest upcoming race; otherwise the most recent completed one. */
  private defaultRaceId(races: PredictableRace[]): number {
    const upcoming = races.filter((r) => !r.hasResult).sort((a, b) => a.round - b.round);
    return upcoming.length > 0 ? upcoming[0].raceId : races[0].raceId;
  }

  private loadPrediction(): void {
    if (this.selectedRaceId === null) {
      return;
    }

    this.loadingPrediction = true;
    this.error = null;

    this.predictionService.getRacePrediction(this.selectedRaceId).subscribe({
      next: (result) => {
        this.result = result;
        this.loadingPrediction = false;
        this.animateKey++; // re-trigger the finish-line animation
      },
      error: (err) => {
        this.result = null;
        this.loadingPrediction = false;
        this.handleError(err, 'Failed to load the prediction');
      }
    });
  }

  private handleError(err: unknown, message: string): void {
    this.error = message;
    console.error(err);
  }
}
