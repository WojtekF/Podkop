import { Component, computed, inject } from '@angular/core';
import { DocumentsService, StatuteDto } from '../documents.service';
import { asResult } from '../../shared/as-result';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { toSignal } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { TimeoutError } from 'rxjs';
import { getCallState } from '../getCallState.helper';

@Component({
  selector: 'app-statute-page',
  imports: [MatProgressSpinner, DatePipe],
  templateUrl: './statute-page.html',
  styleUrl: './statute-page.scss',
})
export class StatutePage {
  protected readonly documents = inject(DocumentsService);

  protected state = getCallState<StatuteDto>(() => this.documents.getCurrentStatute());
}
