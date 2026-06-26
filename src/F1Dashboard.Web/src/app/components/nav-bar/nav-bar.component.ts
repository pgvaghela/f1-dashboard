import { Component, HostListener, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';

@Component({
  selector: 'app-nav-bar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './nav-bar.component.html',
  styleUrl: './nav-bar.component.css'
})
export class NavBarComponent implements OnInit {
  private router = inject(Router);

  isHome = false;
  scrolled = false;
  menuOpen = false;

  readonly navLinks = [
    { label: 'Drivers', href: '/drivers' },
    { label: 'Constructors', href: '/constructors' },
    { label: 'Standings', href: '/standings' },
    { label: 'Predictor', href: '/predictor' },
    { label: 'Lap Data', href: '/lap-data' }
  ];

  ngOnInit(): void {
    this.isHome = this.router.url === '/';
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => {
        this.isHome = e.urlAfterRedirects === '/';
        this.menuOpen = false;
      });
  }

  @HostListener('window:scroll')
  onScroll(): void {
    this.scrolled = window.scrollY > 12;
  }

  toggleMenu(): void {
    this.menuOpen = !this.menuOpen;
  }

  closeMenu(): void {
    this.menuOpen = false;
  }
}
