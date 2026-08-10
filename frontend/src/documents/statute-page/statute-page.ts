import { Component, inject } from '@angular/core';
import { DocumentsService } from '../documents.service';

@Component({
  selector: 'app-statute-page',
  imports: [],
  templateUrl: './statute-page.html',
  styleUrl: './statute-page.scss',
})
export class StatutePage {
  protected readonly documents = inject(DocumentsService);
}
