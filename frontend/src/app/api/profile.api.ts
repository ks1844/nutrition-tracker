import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Profile, ProfileInput } from '../types/api';

/** API-04, API-05 */
@Injectable({ providedIn: 'root' })
export class ProfileApi {
  private readonly http = inject(HttpClient);

  get(): Promise<Profile> {
    return firstValueFrom(this.http.get<Profile>('/api/profile'));
  }

  save(input: ProfileInput): Promise<Profile> {
    return firstValueFrom(this.http.put<Profile>('/api/profile', input));
  }
}
