import { Component, computed, output, signal, inject, effect } from '@angular/core';
import { MatDialogTitle, MatDialogContent, MatDialogActions } from '@angular/material/dialog';
import { DocumentsService } from '../../documents/documents.service';
import { FileReportIntent } from '../finding-report.service';
import { TimeoutError } from 'rxjs';
import { asResult } from '../../shared/as-result';
import { HttpErrorResponse } from '@angular/common/http';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatButton } from '@angular/material/button';
import { MatSelectionList, MatListOption } from '@angular/material/list';
import { MatFormField, MatLabel, MatHint } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';

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
  imports: [
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatProgressSpinner,
    MatButton,
    MatSelectionList,
    MatListOption,
    MatFormField,
    MatInput,
    MatLabel,
    MatHint,
  ],
  templateUrl: './report-dialog.html',
  styleUrl: './report-dialog.scss',
})
export class ReportDialog {
  constructor() {
    this.retryStatute();
  }
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

  protected onOptionClicked = (id: string) => {
    this.selectedPointId.set(id);
  };

  /** Requests the current Statute again after a failed load. */
  protected retryStatute(): void {
    this.status.set('loading');
    this.points.set([]);
    this.selectedPointId.set(null);

    asResult(this.documentsService.getCurrentStatute()).subscribe({
      next: (response) => {
        if (response instanceof HttpErrorResponse || response instanceof TimeoutError) {
          this.status.set('error');
        } else {
          const points = response.sections.flatMap((section) =>
            section.points
              .filter((point) => point.isReportable)
              .map(
                (point) =>
                  ({
                    id: point.id,
                    text: point.text,
                    citation: `${section.number}.${point.number}`,
                  }) as ReportablePointOption,
              ),
          );
          this.points.set(points);
          this.status.set('loaded');
        }
      },
    });
  }

  /** Emits the `fileReport` intent for the picked point, the note normalized. */
  protected submitReport(): void {
    this.fileReport.emit({
      note: this.note().trim() == '' ? null : this.note().trim(),
      statutePointId: this.selectedPointId()!,
    });
  }
}
