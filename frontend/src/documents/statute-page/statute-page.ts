import { Component, inject } from '@angular/core';
import { DocumentsService, StatuteDto } from '../documents.service';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { DatePipe } from '@angular/common';
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
