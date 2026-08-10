import { Component, inject } from '@angular/core';
import { DocumentsService } from '../documents.service';

@Component({
  selector: 'app-privacy-policy-page',
  imports: [],
  templateUrl: './privacy-policy-page.html',
  styleUrl: './privacy-policy-page.scss',
})
export class PrivacyPolicyPage {
  protected readonly documents = inject(DocumentsService);
}
