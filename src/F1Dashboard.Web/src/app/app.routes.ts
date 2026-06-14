import { Routes } from '@angular/router';
import { HomeComponent } from './components/home/home.component';
import { DriversListComponent } from './components/drivers-list/drivers-list.component';
import { ConstructorsListComponent } from './components/constructors-list/constructors-list.component';
import { StandingsComponent } from './components/standings/standings.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'drivers', component: DriversListComponent },
  { path: 'constructors', component: ConstructorsListComponent },
  { path: 'standings', component: StandingsComponent },
  { path: 'standings/:season', component: StandingsComponent }
];
