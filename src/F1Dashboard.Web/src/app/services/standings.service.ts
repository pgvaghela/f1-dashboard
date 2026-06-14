import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { DriverStanding, ConstructorStanding } from '../models/standing';

@Injectable({
  providedIn: 'root'
})
export class StandingsService {
  private readonly apiUrl = 'http://localhost:5197/api/standings';

  constructor(private http: HttpClient) {}

  getDriverStandings(season: number): Observable<DriverStanding[]> {
    return this.http.get<DriverStanding[]>(`${this.apiUrl}/drivers/${season}`);
  }

  getConstructorStandings(season: number): Observable<ConstructorStanding[]> {
    return this.http.get<ConstructorStanding[]>(`${this.apiUrl}/constructors/${season}`);
  }
}
