import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { FindingSummaryDto } from '../main-page/main-page-feed.service';

/**
 * Turns the finding-shaped references on a tag page into cards (ADR 0011): one batch call per
 * page, to the Findings slice's own batch-by-ids endpoint, answering the same card data its feed
 * serves. It lives in the tags feature because hydration is the tag page's concern — the Findings
 * slice just exposes the batch the tag namespace obliges it to.
 *
 * The Microblog slice will need a sibling of this for entries (issue #74); until then a tag page
 * hydrates findings alone.
 */
@Injectable({
  providedIn: 'root',
})
export class TagHydrationService {
  private readonly http = inject(HttpClient);

  /**
   * The cards for the given finding ids. Ids naming nothing come back absent rather than as an
   * error, so the answer may be shorter than the request; putting the cards back into the tag
   * page's order is the caller's job.
   */
  getFindingsByIds(ids: readonly string[]): Observable<FindingSummaryDto[]> {
    throw new Error('not implemented');
  }
}
