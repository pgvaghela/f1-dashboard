import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PredictableRace, RacePredictionResult } from '../models/prediction';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class PredictionService {
  private readonly apiUrl = `${environment.apiBaseUrl}/predictions`;

  constructor(private http: HttpClient) {}

  getRaces(season: number): Observable<PredictableRace[]> {
    return this.http.get<PredictableRace[]>(`${this.apiUrl}/races/${season}`);
  }

  getRacePrediction(raceId: number): Observable<RacePredictionResult> {
    return this.http.get<RacePredictionResult>(`${this.apiUrl}/race/${raceId}`);
  }
}
