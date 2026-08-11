import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';

export interface StatutePointDto {
  id: string;
  number: number;
  text: string;
  isReportable: boolean;
}

export interface StatuteSectionDto {
  number: number;
  title: string;
  points: StatutePointDto[];
}

export interface StatuteDto {
  version: number;
  effectiveFrom: string;
  sections: StatuteSectionDto[];
}

export interface PolicySectionDto {
  number: number;
  title: string;
  paragraphs: string[];
}

export interface PrivacyPolicyDto {
  version: number;
  effectiveFrom: string;
  sections: PolicySectionDto[];
}

/**
 * HTTP client for the Documents slice (issue #30): the two public documents, each served as the
 * version currently in force.
 */
@Injectable({
  providedIn: 'root',
})
export class DocumentsService {
  private readonly http = inject(HttpClient);

  getCurrentStatute(): Observable<StatuteDto> {
    return this.http.get<StatuteDto>('/api/statute').pipe(timeout(5000));
  }

  getCurrentPrivacyPolicy(): Observable<PrivacyPolicyDto> {
    return this.http.get<PrivacyPolicyDto>('/api/privacy-policy').pipe(timeout(5000));
  }
}
