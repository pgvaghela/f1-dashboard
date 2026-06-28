import { Component, ElementRef, HostListener, NgZone, OnInit, inject } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { NavBarComponent } from './components/nav-bar/nav-bar.component';
import { filter } from 'rxjs';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, NavBarComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  title = 'F1Dashboard.Web';
  isHomeRoute = false;

  private zone = inject(NgZone);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);
  private router = inject(Router);
  private ticking = false;

  readonly reduceMotion =
    typeof window !== 'undefined' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /** Drive the ambient spotlight by writing CSS vars straight to the DOM
      (outside Angular) so it stays cheap and never triggers change detection. */
  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent): void {
    if (this.reduceMotion || this.ticking) {
      return;
    }
    this.ticking = true;
    const { clientX, clientY } = event;
    this.zone.runOutsideAngular(() => {
      requestAnimationFrame(() => {
        const style = this.host.nativeElement.style;
        style.setProperty('--mx', `${clientX}px`);
        style.setProperty('--my', `${clientY}px`);
        this.ticking = false;
      });
    });
  }

  ngOnInit(): void {
    this.updateHomeRoute(this.router.url);
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.updateHomeRoute(e.urlAfterRedirects));
  }

  private updateHomeRoute(url: string): void {
    this.isHomeRoute = (url.split('?')[0] || '/') === '/';
  }
}
