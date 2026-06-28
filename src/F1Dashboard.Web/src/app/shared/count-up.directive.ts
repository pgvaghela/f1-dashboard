import { Directive, ElementRef, Input, NgZone, OnChanges, OnDestroy, inject } from '@angular/core';

/**
 * Animates a number from 0 up to `[countUp]` when it changes, writing the
 * formatted value into the host element. Honours prefers-reduced-motion by
 * jumping straight to the final value.
 *
 * Usage: `<span [countUp]="points"></span>`
 *        `<span [countUp]="pct" [countUpDecimals]="1" countUpSuffix="%"></span>`
 */
@Directive({
  selector: '[countUp]',
  standalone: true
})
export class CountUpDirective implements OnChanges, OnDestroy {
  @Input('countUp') value: number | null = 0;
  @Input() countUpDuration = 1100;
  @Input() countUpDecimals = 0;
  @Input() countUpPrefix = '';
  @Input() countUpSuffix = '';

  private el = inject<ElementRef<HTMLElement>>(ElementRef);
  private zone = inject(NgZone);
  private rafId: number | null = null;

  ngOnChanges(): void {
    this.run();
  }

  ngOnDestroy(): void {
    this.stop();
  }

  private run(): void {
    this.stop();
    const target = Number(this.value);
    if (!Number.isFinite(target)) {
      this.write(0);
      return;
    }

    const reduceMotion =
      typeof window !== 'undefined' &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (reduceMotion || this.countUpDuration <= 0) {
      this.write(target);
      return;
    }

    const start = performance.now();
    const duration = this.countUpDuration;
    this.zone.runOutsideAngular(() => {
      const tick = (now: number) => {
        const t = Math.min(1, (now - start) / duration);
        // easeOutQuint for a quick, decelerating count.
        const eased = 1 - Math.pow(1 - t, 5);
        this.write(target * eased);
        if (t < 1) {
          this.rafId = requestAnimationFrame(tick);
        } else {
          this.rafId = null;
        }
      };
      this.rafId = requestAnimationFrame(tick);
    });
  }

  private write(n: number): void {
    const fixed = n.toFixed(this.countUpDecimals);
    this.el.nativeElement.textContent = `${this.countUpPrefix}${fixed}${this.countUpSuffix}`;
  }

  private stop(): void {
    if (this.rafId !== null) {
      cancelAnimationFrame(this.rafId);
      this.rafId = null;
    }
  }
}
