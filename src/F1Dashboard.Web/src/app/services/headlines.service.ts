import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Headline } from '../models/headline';

@Injectable({
  providedIn: 'root'
})
export class HeadlinesService {
  private readonly apiUrl = `${environment.apiBaseUrl}/headlines`;

  constructor(private readonly http: HttpClient) {}

  getLatest(limit = 6): Observable<Headline[]> {
    return this.http.get<Headline[]>(`${this.apiUrl}?limit=${limit}`);
  }
}
