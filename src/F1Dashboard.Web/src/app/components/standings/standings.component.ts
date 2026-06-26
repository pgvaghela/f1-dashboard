import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { StandingsService } from '../../services/standings.service';
import { teamMeta, flagUrl, driverHeadshot, constructorLogo } from '../../models/standings-meta';
import { RevealDirective } from '../../shared/reveal.directive';
import { CountUpDirective } from '../../shared/count-up.directive';

type StandingType = 'drivers' | 'constructors';

interface StandingRow {
  position: number;
  name: string;
  code: string;
  nationality: string;
  flagUrl: string | null;
  headshotUrl: string | null;
  logoUrl: string | null;
  teamName: string;
  teamColor: string;
  teamAbbr: string;
  totalPoints: number;
}

@Component({
  selector: 'app-standings',
  imports: [RevealDirective, CountUpDirective],
  templateUrl: './standings.component.html',
  styleUrl: './standings.component.css'
})
export class StandingsComponent implements OnInit {
  private standingsService = inject(StandingsService);
  private route = inject(ActivatedRoute);

  readonly seasons = [2026, 2025, 2024, 2023];
  selectedSeason = this.seasons[0];
  type: StandingType = 'drivers';

  rows: StandingRow[] = [];
  loading = true;
  error: string | null = null;

  // Image URLs that failed to load, so we fall back to the flag/badge.
  readonly failedImages = new Set<string>();

  onImageError(url: string | null): void {
    if (url) {
      this.failedImages.add(url);
    }
  }

  /** The points leader (rows are sorted descending), shown in the hero band. */
  get leader(): StandingRow | null {
    return this.rows[0] ?? null;
  }

  /** Half-points exist historically, so keep a decimal only when needed. */
  pointsDecimals(value: number): number {
    return Number.isInteger(value) ? 0 : 1;
  }

  ngOnInit(): void {
    const seasonParam = Number(this.route.snapshot.paramMap.get('season'));
    if (this.seasons.includes(seasonParam)) {
      this.selectedSeason = seasonParam;
    }
    this.load();
  }

  onSeasonChange(value: string): void {
    this.selectedSeason = Number(value);
    this.load();
  }

  setType(type: StandingType): void {
    if (this.type === type) {
      return;
    }
    this.type = type;
    this.load();
  }

  private load(): void {
    this.loading = true;
    this.error = null;

    if (this.type === 'drivers') {
      this.standingsService.getDriverStandings(this.selectedSeason).subscribe({
        next: (data) => {
          this.rows = data.map((s) => {
            const team = teamMeta(s.teamName);
            return {
              position: s.position,
              name: `${s.firstName} ${s.lastName}`,
              code: s.code,
              nationality: s.nationality,
              flagUrl: flagUrl(s.nationality),
              headshotUrl: driverHeadshot(s.code),
              logoUrl: constructorLogo(s.teamName),
              teamName: s.teamName,
              teamColor: team.color,
              teamAbbr: team.abbr,
              totalPoints: s.totalPoints
            };
          });
          this.loading = false;
        },
        error: (err) => this.handleError(err)
      });
    } else {
      this.standingsService.getConstructorStandings(this.selectedSeason).subscribe({
        next: (data) => {
          this.rows = data.map((s) => {
            const team = teamMeta(s.teamName);
            return {
              position: s.position,
              name: s.teamName,
              code: team.abbr,
              nationality: s.nationality,
              flagUrl: flagUrl(s.nationality),
              headshotUrl: null,
              logoUrl: constructorLogo(s.teamName),
              teamName: s.teamName,
              teamColor: team.color,
              teamAbbr: team.abbr,
              totalPoints: s.totalPoints
            };
          });
          this.loading = false;
        },
        error: (err) => this.handleError(err)
      });
    }
  }

  private handleError(err: unknown): void {
    this.error = 'Failed to load standings';
    this.loading = false;
    console.error(err);
  }
}
