import { PolicySectionDto } from './../documents.service';
import { Component, computed, inject } from '@angular/core';
import { DocumentsService } from '../documents.service';
import { toSignal } from '@angular/core/rxjs-interop';
import { asResult } from '../../shared/as-result';
import { HttpErrorResponse } from '@angular/common/http';
import { TimeoutError } from 'rxjs';
import { DatePipe } from '@angular/common';
import { MatProgressSpinner } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-privacy-policy-page',
  imports: [MatProgressSpinner, DatePipe],
  templateUrl: './privacy-policy-page.html',
  styleUrl: './privacy-policy-page.scss',
})
export class PrivacyPolicyPage {
  protected readonly documents = inject(DocumentsService);

  protected policy = toSignal(asResult(this.documents.getCurrentPrivacyPolicy()), {
    initialValue: undefined,
  });

  protected loading = computed(() => this.policy() === undefined);

  protected error = computed(
    () => this.policy() instanceof HttpErrorResponse || this.policy() instanceof TimeoutError,
  );
}
