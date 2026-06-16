export interface PredictableRace {
  raceId: number;
  season: number;
  round: number;
  date: string;
  name: string;
  circuit: string;
  /** False for upcoming races that haven't been run yet. */
  hasResult: boolean;
}

export interface DriverPrediction {
  driverId: number;
  driverName: string;
  code: string;
  constructorName: string;
  /** Null for upcoming races — qualifying hasn't happened. */
  grid: number | null;
  winProbability: number;
  isPredictedWinner: boolean;
  actualFinish: number | null;
  isActualWinner: boolean;
}

export interface RacePredictionResult {
  race: PredictableRace;
  modelAccuracy: number;
  /** 'model' (trained, post-qualifying), 'grid-prior' (cold-start), or 'upcoming' (no-grid). */
  basis: string;
  /** True for a race that hasn't run yet (no grid, no result, form-based estimate). */
  isUpcoming: boolean;
  drivers: DriverPrediction[];
}
