import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/** Which content types a tag page lists. Full from day one, per the tag-page spec (issue #77). */
export type TagContentFilter = 'all' | 'findings' | 'entries';

/** The content types a reference can name. Entries arrive with the Microblog slice (issue #74). */
export type TaggedContentType = 'finding' | 'entry';

/**
 * One item of a tag page: a typed reference and nothing more (ADR 0011). Card data is not here
 * by design — it is hydrated per content type from the owning slice.
 */
export interface TaggedContentRefDto {
  type: TaggedContentType;
  id: string;
}

export interface TagPageDto {
  items: TaggedContentRefDto[];
  hasNextPage: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class TagsService {
  private readonly http = inject(HttpClient);

  /**
   * One page of a tag's stream. The name goes out exactly as the URL spelled it — folding it to
   * the canonical tag is the server's business, so any casing lands on the same page. A tag that
   * no content carries answers 404, which the caller turns into the page's not-found state.
   */
  getTagPage(name: string, filter: TagContentFilter, page: number): Observable<TagPageDto> {
    throw new Error('not implemented');
  }
}
