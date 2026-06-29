import {
  afterNextRender,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
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
export class HomeComponent implements OnDestroy, OnInit {
  private ticking = false;
  private scrubRaf: number | null = null;
  private targetTime = 0;
  private videoPrimed = false;
  private videoPriming = false;
  private removeTouchPrime?: () => void;

  @ViewChild('heroVideo') heroVideo?: ElementRef<HTMLVideoElement>;

  readonly reduceMotion =
    typeof window !== 'undefined' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /** Touch devices scrub on scroll directly — iOS throttles rAF currentTime easing. */
  private readonly directScrub =
    typeof window !== 'undefined' &&
    window.matchMedia('(pointer: coarse)').matches;

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
  ) {
    afterNextRender(() => this.initHeroVideo());
  }

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

  ngOnDestroy(): void {
    this.removeTouchPrime?.();
    if (this.scrubRaf !== null) {
      cancelAnimationFrame(this.scrubRaf);
      this.scrubRaf = null;
    }
  }

  @HostListener('window:scroll')
  onScroll(): void {
    if (this.reduceMotion || this.ticking) {
      return;
    }

    if (!this.videoPrimed) {
      const video = this.heroVideo?.nativeElement;
      if (video) {
        void this.primeVideoForScrubbing(video);
      }
    }

    this.ticking = true;
    requestAnimationFrame(() => {
      this.updateScrollProgress();
      this.syncTargetTime();
      this.applyVideoTime();
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

  private initHeroVideo(): void {
    if (this.reduceMotion) {
      return;
    }

    const video = this.heroVideo?.nativeElement;
    if (!video) {
      return;
    }

    video.muted = true;
    video.playsInline = true;
    video.setAttribute('playsinline', '');
    video.setAttribute('webkit-playsinline', '');

    const onReady = () => {
      this.syncTargetTime();
      this.applyVideoTime();
      void this.primeVideoForScrubbing(video);
    };

    video.addEventListener('loadedmetadata', onReady, { once: true });
    if (video.readyState >= 1) {
      onReady();
    }

  }

  private primeVideoForScrubbing(video: HTMLVideoElement): void {
    if (this.videoPrimed || this.videoPriming) {
      return;
    }

    this.videoPriming = true;

    const markPrimed = () => {
      this.videoPriming = false;
      this.videoPrimed = true;
      this.removeTouchPrime?.();
      this.removeTouchPrime = undefined;
      video.pause();
      this.syncTargetTime();
      this.applyVideoTime();
    };

    const attempt = video.play();
    if (attempt === undefined) {
      markPrimed();
      return;
    }

    attempt.then(markPrimed).catch(() => {
      this.videoPriming = false;
      const onTouch = () => {
        void video.play().then(markPrimed).catch(() => undefined);
      };
      document.addEventListener('touchstart', onTouch, { once: true, passive: true });
      document.addEventListener('click', onTouch, { once: true });
      this.removeTouchPrime = () => {
        document.removeEventListener('touchstart', onTouch);
        document.removeEventListener('click', onTouch);
      };
    });
  }

  private syncTargetTime(): void {
    const video = this.heroVideo?.nativeElement;
    if (!video || !video.duration || Number.isNaN(video.duration)) {
      return;
    }

    this.targetTime = this.scrollProgress * video.duration;
  }

  private applyVideoTime(): void {
    const video = this.heroVideo?.nativeElement;
    if (!video || !video.duration || Number.isNaN(video.duration)) {
      return;
    }

    if (this.directScrub) {
      video.currentTime = this.targetTime;
      return;
    }

    this.startScrubLoop();
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
