import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { StandingsService } from '../../services/standings.service';

type StandingType = 'drivers' | 'constructors';

interface StandingRow {
  position: number;
  name: string;
  totalPoints: number;
}

@Component({
  selector: 'app-standings',
  imports: [],
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
          this.rows = data.map((s) => ({
            position: s.position,
            name: `${s.firstName} ${s.lastName}`,
            totalPoints: s.totalPoints
          }));
          this.loading = false;
        },
        error: (err) => this.handleError(err)
      });
    } else {
      this.standingsService.getConstructorStandings(this.selectedSeason).subscribe({
        next: (data) => {
          this.rows = data.map((s) => ({
            position: s.position,
            name: s.teamName,
            totalPoints: s.totalPoints
          }));
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
