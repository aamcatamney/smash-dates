import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface VersionInfo {
  version: string;
}

@Injectable({ providedIn: 'root' })
export class VersionApi {
  private readonly http = inject(HttpClient);

  get(): Observable<VersionInfo> {
    return this.http.get<VersionInfo>('/api/version');
  }
}
