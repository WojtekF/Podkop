import { Component, computed, inject, signal } from '@angular/core';
import { DocumentsService } from '../documents.service';
import { asResult } from '../../shared/as-result';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { toSignal } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { TimeoutError } from 'rxjs/internal/operators/timeout';

@Component({
  selector: 'app-statute-page',
  imports: [MatProgressSpinner, DatePipe],
  templateUrl: './statute-page.html',
  styleUrl: './statute-page.scss',
})
export class StatutePage {
  protected readonly documents = inject(DocumentsService);

  protected statute = toSignal(asResult(this.documents.getCurrentStatute()), {
    initialValue: undefined,
  });

  protected loading = computed(() => this.statute() === undefined);

  protected error = computed(
    () => this.statute() instanceof HttpErrorResponse || this.statute() instanceof TimeoutError,
  );
}
