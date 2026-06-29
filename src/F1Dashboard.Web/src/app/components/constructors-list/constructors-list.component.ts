import { Component, OnInit, inject } from '@angular/core';
import { Constructor } from '../../models/constructor';
import { ConstructorService } from '../../services/constructor.service';
import { constructorLogo, teamMeta } from '../../models/standings-meta';
import { RevealDirective } from '../../shared/reveal.directive';

@Component({
  selector: 'app-constructors-list',
  imports: [RevealDirective],
  templateUrl: './constructors-list.component.html',
  styles: [`
    .entity { display: inline-flex; align-items: center; gap: 14px; }
    .entity__name { font-weight: 600; }
    .logo {
      display: inline-flex; align-items: center; justify-content: center;
      width: 54px; height: 38px; flex-shrink: 0;
      transition: transform 0.2s var(--ease-out-quint), filter 0.2s ease;
    }
    .logo__img { max-width: 100%; max-height: 100%; object-fit: contain; }
    .logo__badge {
      display: inline-flex; align-items: center; justify-content: center;
      min-width: 38px; height: 26px; padding: 0 6px; border-radius: 6px;
      color: #fff; font-size: 11px; font-weight: 800; letter-spacing: 0.4px;
      text-shadow: 0 1px 1px rgba(0,0,0,0.35);
    }
    .data-table tbody tr {
      transition: background-color 0.2s ease, box-shadow 0.2s ease, transform 0.2s var(--ease-out-quint);
    }
    .data-table tbody tr:hover {
      transform: translateX(3px);
      box-shadow:
        inset 4px 0 0 var(--team-color, var(--color-accent)),
        0 0 22px color-mix(in srgb, var(--team-color, var(--color-accent)) 28%, transparent);
    }
    .data-table tbody tr:hover .logo {
      transform: scale(1.08);
      filter: drop-shadow(0 0 10px color-mix(in srgb, var(--team-color, var(--color-accent)) 55%, transparent));
    }
  `]
})
export class ConstructorsListComponent implements OnInit {
  private constructorService = inject(ConstructorService);
  constructors: Constructor[] = [];
  loading = true;
  error: string | null = null;

  readonly failedImages = new Set<string>();

  logo(teamName: string): string | null {
    return constructorLogo(teamName);
  }

  teamColor(teamName: string): string {
    return teamMeta(teamName).color;
  }

  teamAbbr(teamName: string): string {
    return teamMeta(teamName).abbr;
  }

  onImageError(url: string | null): void {
    if (url) {
      this.failedImages.add(url);
    }
  }

  ngOnInit(): void {
    this.constructorService.getConstructors().subscribe({
      next: (data) => {
        this.constructors = data;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Failed to load constructors';
        this.loading = false;
        console.error(err);
      }
    });
  }
}
