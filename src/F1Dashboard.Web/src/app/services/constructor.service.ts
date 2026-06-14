import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Constructor } from '../models/constructor';

@Injectable({
  providedIn: 'root'
})
export class ConstructorService {
  private readonly apiUrl = 'http://localhost:5197/api/constructors';

  constructor(private http: HttpClient) {}

  getConstructors(): Observable<Constructor[]> {
    return this.http.get<Constructor[]>(this.apiUrl);
  }

  getConstructor(id: number): Observable<Constructor> {
    return this.http.get<Constructor>(`${this.apiUrl}/${id}`);
  }
}
