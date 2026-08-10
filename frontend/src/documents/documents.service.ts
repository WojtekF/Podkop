import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

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
 * HTTP client for the Statute slice (issue #30): the two public documents, each served as the
 * version currently in force.
 */
@Injectable({
  providedIn: 'root',
})
export class DocumentsService {
  private readonly http = inject(HttpClient);

  getCurrentStatute(): Observable<StatuteDto> {
    throw new Error('not implemented');
  }

  getCurrentPrivacyPolicy(): Observable<PrivacyPolicyDto> {
    throw new Error('not implemented');
  }
}
