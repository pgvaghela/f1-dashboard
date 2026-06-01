import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Driver } from '../../models/driver';
import { DriverService } from '../../services/driver.service';

@Component({
  selector: 'app-drivers-list',
  imports: [CommonModule],
  templateUrl: './drivers-list.component.html',
  styleUrl: './drivers-list.component.css'
})
export class DriversListComponent implements OnInit {
  private driverService = inject(DriverService);
  drivers: Driver[] = [];
  loading = true;
  error: string | null = null;

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