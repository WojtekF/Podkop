import { Component, inject } from '@angular/core';
import { DocumentsService, PrivacyPolicyDto } from '../documents.service';
import { DatePipe } from '@angular/common';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { getCallState } from '../get-call-state.helper';

@Component({
  selector: 'app-privacy-policy-page',
  imports: [MatProgressSpinner, DatePipe],
  templateUrl: './privacy-policy-page.html',
  styleUrl: './privacy-policy-page.scss',
})
export class PrivacyPolicyPage {
  protected readonly documents = inject(DocumentsService);

  protected state = getCallState<PrivacyPolicyDto>(this.documents.getCurrentPrivacyPolicy());
}
