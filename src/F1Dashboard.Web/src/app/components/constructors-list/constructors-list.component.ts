import { Component, OnInit, inject } from '@angular/core';
import { Constructor } from '../../models/constructor';
import { ConstructorService } from '../../services/constructor.service';

@Component({
  selector: 'app-constructors-list',
  imports: [],
  templateUrl: './constructors-list.component.html'
})
export class ConstructorsListComponent implements OnInit {
  private constructorService = inject(ConstructorService);
  constructors: Constructor[] = [];
  loading = true;
  error: string | null = null;

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
