import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
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

  /** When true, show the static poster instead of autoplaying the hero video. */
  readonly reduceMotion =
    typeof window !== 'undefined' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  ngAfterViewInit(): void {
    const reduceMotion = this.reduceMotion;

    const targets = Array.from(
      this.host.nativeElement.querySelectorAll<HTMLElement>('.reveal')
    );

    // Reduced motion (or no IntersectionObserver support): show everything at once.
    if (reduceMotion || !('IntersectionObserver' in window)) {
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
