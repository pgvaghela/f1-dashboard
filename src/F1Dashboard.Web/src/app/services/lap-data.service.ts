import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { LapDetail, LapDriver, LapListItem, LapRace } from '../models/lap-data';

@Injectable({
  providedIn: 'root'
})
export class LapDataService {
  private readonly apiUrl = `${environment.apiBaseUrl}/lap-data`;

  constructor(private http: HttpClient) {}

  getSeasons(): Observable<number[]> {
    return this.http.get<number[]>(`${this.apiUrl}/seasons`);
  }

  getRaces(season: number): Observable<LapRace[]> {
    return this.http.get<LapRace[]>(`${this.apiUrl}/races/${season}`);
  }

  getDrivers(raceId: number): Observable<LapDriver[]> {
    return this.http.get<LapDriver[]>(`${this.apiUrl}/races/${raceId}/drivers`);
  }

  getLaps(raceId: number, driverId: number): Observable<LapListItem[]> {
    return this.http.get<LapListItem[]>(`${this.apiUrl}/races/${raceId}/drivers/${driverId}/laps`);
  }

  getLapDetail(lapId: number): Observable<LapDetail> {
    return this.http.get<LapDetail>(`${this.apiUrl}/laps/${lapId}`);
  }
}
