import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  ViewChild,
  inject
} from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css'
})
export class HomeComponent implements AfterViewInit, OnDestroy {
  private host = inject<ElementRef<HTMLElement>>(ElementRef);
  private observer?: IntersectionObserver;
  private ticking = false;

  /** Tall wrapper that provides the scroll distance the hero video scrubs over. */
  @ViewChild('heroScroll') heroScroll?: ElementRef<HTMLElement>;
  /** The hero video whose currentTime is driven by scroll position. */
  @ViewChild('heroVideo') heroVideo?: ElementRef<HTMLVideoElement>;

  /** When true, skip the scroll-scrub video entirely and show the static poster. */
  readonly reduceMotion =
    typeof window !== 'undefined' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  ngAfterViewInit(): void {
    this.setupReveal();

    // Prime the scrub once the video knows its duration, and set the initial frame.
    const video = this.heroVideo?.nativeElement;
    if (video) {
      video.addEventListener('loadedmetadata', () => this.updateScrub());
      this.updateScrub();
    }
  }

  // Scroll drives the hero video forward; rAF keeps it to one update per frame.
  @HostListener('window:scroll')
  onScroll(): void {
    if (this.reduceMotion || this.ticking) {
      return;
    }
    this.ticking = true;
    requestAnimationFrame(() => {
      this.updateScrub();
      this.ticking = false;
    });
  }

  private updateScrub(): void {
    const scroll = this.heroScroll?.nativeElement;
    const video = this.heroVideo?.nativeElement;
    if (!scroll || !video || !video.duration || Number.isNaN(video.duration)) {
      return;
    }

    // How far we've scrolled into the pinned region, as 0..1.
    const distance = scroll.offsetHeight - window.innerHeight;
    const passed = Math.min(Math.max(-scroll.getBoundingClientRect().top, 0), distance);
    const progress = distance > 0 ? passed / distance : 0;

    const target = progress * video.duration;
    // fastSeek (where supported) is smoother for scrubbing than setting currentTime.
    if (typeof video.fastSeek === 'function') {
      video.fastSeek(target);
    } else {
      video.currentTime = target;
    }
  }

  private setupReveal(): void {
    const targets = Array.from(
      this.host.nativeElement.querySelectorAll<HTMLElement>('.reveal')
    );

    // Reduced motion (or no IntersectionObserver support): show everything at once.
    if (this.reduceMotion || !('IntersectionObserver' in window)) {
      targets.forEach((el) => el.classList.add('is-visible'));
      return;
    }

    this.observer = new IntersectionObserver(
      (entries, obs) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            entry.target.classList.add('is-visible');
            obs.unobserve(entry.target);
          }
        }
      },
      { threshold: 0.15, rootMargin: '0px 0px -10% 0px' }
    );

    targets.forEach((el) => this.observer!.observe(el));
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }
}
