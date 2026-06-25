export interface LapRace {
  raceId: number;
  season: number;
  round: number;
  name: string;
  circuitName: string;
}

export interface LapDriver {
  driverId: number;
  code: string;
  name: string;
  teamName: string;
  teamColor: string;
  headshotUrl: string;
}

export interface LapListItem {
  lapId: number;
  lapNumber: number;
  lapTimeSeconds: number | null;
  compound: string;
}

export interface TrackOutline {
  path: string;
  width: number;
  height: number;
  /** Three open sub-paths, one per physical sector, aligned with the sectors list. */
  sectorPaths: string[];
}

export interface LapSample {
  t: number;
  x: number;
  y: number;
  speed: number;
  compound: string;
  position: number | null;
}

/** color is 'purple' | 'green' | 'yellow' per F1 timing convention. */
export interface SectorTime {
  sector: number;
  timeSeconds: number | null;
  color: string;
}

export interface LapDetail {
  lapId: number;
  lapNumber: number;
  lapTimeSeconds: number | null;
  teamColor: string;
  compound: string;
  outline: TrackOutline;
  sectors: SectorTime[];
  samples: LapSample[];
}
