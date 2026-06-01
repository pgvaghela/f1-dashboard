import { Routes } from '@angular/router';
import { DriversListComponent } from './components/drivers-list/drivers-list.component';

export const routes: Routes = [
  { path: '', redirectTo: '/drivers', pathMatch: 'full' },
  { path: 'drivers', component: DriversListComponent }
];