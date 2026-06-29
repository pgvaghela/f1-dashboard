import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  OnInit,
  ViewChild
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { PredictionService } from '../../services/prediction.service';
import { PredictableRace } from '../../models/prediction';
import { Headline } from '../../models/headline';
import { HeadlinesService } from '../../services/headlines.service';

@Component({
  selector: 'app-home',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css'
})
export class HomeComponent implements AfterViewInit, OnInit {
  private ticking = false;
  private scrubRaf: number | null = null;
  private targetTime = 0;

  @ViewChild('heroVideo') heroVideo?: ElementRef<HTMLVideoElement>;

  readonly reduceMotion =
    typeof window !== 'undefined' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  menuOpen = false;
  scrollProgress = 0;

  readonly navLinks = [
    { label: 'Drivers', href: '/drivers' },
    { label: 'Constructors', href: '/constructors' },
    { label: 'Standings', href: '/standings' },
    { label: 'Predictor', href: '/predictor' },
    { label: 'Lap Data', href: '/lap-data' }
  ];

  readonly meta = ['2026 Season', '24 Races', 'Live Telemetry'];
  upcomingRaces: PredictableRace[] = [];
  headlinesLoading = true;

  newsItems: Headline[] = [];

  constructor(
    private readonly predictions: PredictionService,
    private readonly headlines: HeadlinesService
  ) {}

  ngOnInit(): void {
    this.predictions.getRaces(2026).subscribe({
      next: (races) => {
        this.upcomingRaces = races
          .filter((race) => !race.hasResult)
          .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())
          .slice(0, 6);
      },
      error: (err) => {
        this.upcomingRaces = [];
        console.error('Failed to load upcoming races for home page', err);
      }
    });

    this.headlines.getLatest(6).subscribe({
      next: (items) => {
        this.newsItems = items;
        this.headlinesLoading = false;
      },
      error: (err) => {
        this.newsItems = [];
        this.headlinesLoading = false;
        console.error('Failed to load live headlines for home page', err);
      }
    });

    if (!this.reduceMotion) {
      this.updateScrollProgress();
    }
  }

  ngAfterViewInit(): void {
    const video = this.heroVideo?.nativeElement;
    if (video && !this.reduceMotion) {
      video.addEventListener('loadedmetadata', () => {
        this.syncTargetTime();
        video.currentTime = this.targetTime;
      });
      this.syncTargetTime();
      this.startScrubLoop();
    }
  }

  @HostListener('window:scroll')
  onScroll(): void {
    if (this.reduceMotion || this.ticking) {
      return;
    }

    this.ticking = true;
    requestAnimationFrame(() => {
      this.updateScrollProgress();
      this.syncTargetTime();
      this.startScrubLoop();
      this.ticking = false;
    });
  }

  toggleMenu(): void {
    this.menuOpen = !this.menuOpen;
  }

  closeMenu(): void {
    this.menuOpen = false;
  }

  formatRaceDate(dateValue: string): string {
    const date = new Date(dateValue);
    return new Intl.DateTimeFormat('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric'
    }).format(date);
  }

  private updateScrollProgress(): void {
    const distance = window.innerHeight * 2.2;
    this.scrollProgress = Math.min(Math.max(window.scrollY / distance, 0), 1);
  }

  private syncTargetTime(): void {
    const video = this.heroVideo?.nativeElement;
    if (!video || !video.duration || Number.isNaN(video.duration)) {
      return;
    }

    this.targetTime = this.scrollProgress * video.duration;
  }

  private startScrubLoop(): void {
    if (this.scrubRaf !== null) {
      return;
    }

    const tick = () => {
      const video = this.heroVideo?.nativeElement;
      if (!video || !video.duration || Number.isNaN(video.duration)) {
        this.scrubRaf = null;
        return;
      }

      const delta = this.targetTime - video.currentTime;
      if (Math.abs(delta) <= 1 / 120) {
        video.currentTime = this.targetTime;
        this.scrubRaf = null;
        return;
      }

      // Smoothly ease toward target time to avoid keyframe-jump teleports.
      video.currentTime += delta * 0.22;
      this.scrubRaf = requestAnimationFrame(tick);
    };

    this.scrubRaf = requestAnimationFrame(tick);
  }
}
