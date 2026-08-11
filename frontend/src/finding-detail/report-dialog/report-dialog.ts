import { Component, computed, output, signal, inject } from '@angular/core';
import { DocumentsService } from '../../documents/documents.service';
import { FileReportIntent } from '../finding-report.service';

/** The most text one report note may carry (issue #32); mirrors the backend's Report.MaxNoteLength. */
export const REPORT_NOTE_MAX_LENGTH = 500;

/** One offered point: its stable id, its citation (e.g. "2.1"), and its text. */
export interface ReportablePointOption {
  id: string;
  citation: string;
  text: string;
}

export type ReportDialogStatus = 'loading' | 'error' | 'loaded';

/**
 * The report dialog (issue #32): on creation it requests the current Statute through the
 * DocumentsService and offers only its reportable points — the member picks exactly one,
 * optionally adds a short note, and submits. Submitting emits the `fileReport` intent (the
 * note trimmed, or null when empty) and nothing else — filing itself is the store's business.
 */
@Component({
  selector: 'app-report-dialog',
  imports: [],
  templateUrl: './report-dialog.html',
  styleUrl: './report-dialog.scss',
})
export class ReportDialog {
  readonly fileReport = output<FileReportIntent>();
  readonly cancel = output<void>();

  private readonly documentsService = inject(DocumentsService);

  protected readonly status = signal<ReportDialogStatus>('loading');
  protected readonly points = signal<ReportablePointOption[]>([]);
  protected readonly selectedPointId = signal<string | null>(null);
  protected readonly note = signal('');

  protected readonly maxNoteLength = REPORT_NOTE_MAX_LENGTH;
  protected readonly isOverLimit = computed(() => this.note().length > REPORT_NOTE_MAX_LENGTH);
  protected readonly isSubmitDisabled = computed(
    () => this.selectedPointId() === null || this.isOverLimit(),
  );

  /** Requests the current Statute again after a failed load. */
  protected retryStatute(): void {
    throw new Error('not implemented');
  }

  /** Emits the `fileReport` intent for the picked point, the note normalized. */
  protected submitReport(): void {
    throw new Error('not implemented');
  }
}
