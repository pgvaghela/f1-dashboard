import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Driver } from '../../models/driver';
import { DriverService } from '../../services/driver.service';
import { driverHeadshot } from '../../models/standings-meta';
import { RevealDirective } from '../../shared/reveal.directive';

@Component({
  selector: 'app-drivers-list',
  imports: [CommonModule, RevealDirective],
  templateUrl: './drivers-list.component.html',
  styleUrl: './drivers-list.component.css'
})
export class DriversListComponent implements OnInit {
  private driverService = inject(DriverService);
  drivers: Driver[] = [];
  loading = true;
  error: string | null = null;

  readonly failedImages = new Set<string>();

  headshot(code: string): string | null {
    return driverHeadshot(code);
  }

  onImageError(url: string | null): void {
    if (url) {
      this.failedImages.add(url);
    }
  }

  ngOnInit(): void {
    this.driverService.getDrivers().subscribe({
      next: (data) => {
        this.drivers = data;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Failed to load drivers';
        this.loading = false;
        console.error(err);
      }
    });
  }
}