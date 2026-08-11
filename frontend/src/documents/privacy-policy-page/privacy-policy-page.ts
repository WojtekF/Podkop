import { Component, computed, inject } from '@angular/core';
import { DocumentsService, PrivacyPolicyDto } from '../documents.service';
import { toSignal } from '@angular/core/rxjs-interop';
import { asResult, LoadResult } from '../../shared/as-result';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, TimeoutError } from 'rxjs';
import { DatePipe } from '@angular/common';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { getCallState } from '../getCallState.helper';

@Component({
  selector: 'app-privacy-policy-page',
  imports: [MatProgressSpinner, DatePipe],
  templateUrl: './privacy-policy-page.html',
  styleUrl: './privacy-policy-page.scss',
})
export class PrivacyPolicyPage {
  protected readonly documents = inject(DocumentsService);

  protected state = getCallState<PrivacyPolicyDto>(() => this.documents.getCurrentPrivacyPolicy());
}
